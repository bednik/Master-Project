Shader "VolumeRendering/Transfer/Color"
{
	Properties
	{
		_Volume("Volume", 3D) = "" {}
		_Transfer("Transfer function", 2D) = "" {}
		_Intensity("Intensity", Range(1.0, 5.0)) = 1.2
		_ThresholdMax("Max accepted texture value", Range(0.0, 1.0)) = 0.95
		_ThresholdMin("Minumum accepted texture value", Range(0.0, 1.0)) = 0.05
		_SliceMin("Slice min", Vector) = (-0.5, -0.5, -0.5, 1.0)
		_SliceMax("Slice max", Vector) = (0.5, 0.5, 0.5, 1.0)
		_bbMin("Volume's minimum coord of bounding box", Vector) = (-0.5, -0.5, -0.5, 1.0)
		_bbMax("Volume's maximum coord of bounding box", Vector) = (0.5, 0.5, 0.5, 1.0)
		_VolumeDims("Dimensions of the volume", Vector) = (154, 154, 441)
		_Quality("Quality factor for amount of sample points", Range(1.0, 5.0)) = 1.0
	}

		SubShader
		{
			// Setting the renderqueue to "Transparent" makes it prettier in the editor
			Tags { "Queue" = "Transparent"}
			Blend SrcAlpha OneMinusSrcAlpha

			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				sampler3D _Volume;
				sampler2D _Transfer;
				half _Intensity, _ThresholdMin, _ThresholdMax, _Quality;
				half3 _SliceMin, _SliceMax;
				float3 _bbMin, _bbMax;
				int3 _VolumeDims;
				#define SAMPLEPOINTS 256

				struct Ray {
					float3 origin;
					float3 dir;
					float length;
				};

				struct vertexData
				{
					float4 pos : POSITION;
					float2 uv : TEXCOORD0;
				};

				struct v2f
				{
					float4 vertex : SV_POSITION;
					float2 uv : TEXCOORD0;
					float3 world : TEXCOORD1;
					float3 local : TEXCOORD2;
					float3 t_0 : TEXCOORD3;
				};

				// Returns 1 if point p is within the specified slice boundaries, 1 otherwise
				float InsideSlice(float3 p) {
					return step(_SliceMin.x, p.x) * step(_SliceMin.y, p.y) * step(_SliceMin.z, p.z) * step(p.x, _SliceMax.x) * step(p.y, _SliceMax.y) * step(p.z, _SliceMax.z);
				}

				// Returns 1 if texture value is within the specified thresholds, 0 otherwise
				float InsideThreshold(float val) {
					return step(_ThresholdMin, val) * step(val, _ThresholdMax);
				}

				// Adapted from https://stackoverflow.com/questions/28006184/get-component-wise-maximum-of-vector-in-glsl
				float max3(float3 v) {
					return max(max(v.x, v.y), v.z);
				}

				float min3(float3 v) {
					return min(min(v.x, v.y), v.z);
				}

				// From Lachlan Deakin's code (https://github.com/LDeakin/VkVolume/blob/master/shaders/volume_render.frag)
				float3 ray_caster_get_back(float3 front_intersection, float3 dir) {
					// Use AABB ray-box intersection (simplified due to unit cube [0-1]) to get intersection with back
					float3 dir_inv = 1.0f / dir;
					float3 tMin = -front_intersection * dir_inv;
					float3 tMax = (1.0f - front_intersection) * dir_inv;
					float3 t1 = min(tMin, tMax);
					float3 t2 = max(tMin, tMax);
					float tNear = max(max(t1.x, t1.y), t1.z);
					float tFar = min(min(t2.x, t2.y), t2.z);

					// Return the back intersection
					return tFar * dir + front_intersection;
				}

				// Adapted from intersectAABB in https://gist.github.com/DomNomNom/46bb1ce47f68d255fd5d
				float2 intersectAABB(float3 origin, float3 rayDir) {
					float3 invRayDir = 1 / rayDir;
					float3 intersectionMin = (_bbMin - origin) * invRayDir;
					float3 intersectionMax = (_bbMax - origin) * invRayDir;

					float3 intersection1 = min(intersectionMin, intersectionMax);
					float3 intersection2 = max(intersectionMin, intersectionMax);

					float entrance = max(max(intersection1.x, intersection1.y), intersection1.z);
					float exit = min(min(intersection2.x, intersection2.y), intersection2.z);

					return float2(entrance, exit);
				};

				// Adapted from Tasken, Barstad, and Tagestad's shader
				float3 calculateStep(float2 intersections, Ray ray) {
					float3 end = ray.origin + ray.dir * intersections.y;
					float step_size = abs(intersections.y - intersections.x) / SAMPLEPOINTS;
					return normalize(end - ray.origin) * step_size;
				}

				// Vertex kernel //
				v2f vert(vertexData v)
				{
					v2f o;
					o.vertex = UnityObjectToClipPos(v.pos);
					o.uv = v.uv;
					o.t_0 = v.pos.xyz + 0.5;
					o.world = mul(unity_ObjectToWorld, v.pos).xyz;
					o.local = v.pos.xyz;
					return o;
				}

				// Fragment kernel //
				fixed4 frag(v2f vdata) : SV_Target
				{
					// Determine ray direction and length
                    Ray ray;
					ray.origin = vdata.t_0;
					ray.dir = normalize(mul(unity_WorldToObject, vdata.world - _WorldSpaceCameraPos));
					float3 ray_exit = ray_caster_get_back(vdata.t_0, ray.dir);
  					ray.length = length(vdata.t_0 - ray_exit);

					// Calculate amount of sample points and step length (with direction)
					//int n = int(ceil(float(max3(_VolumeDims)) * ray.length * _Quality));
					int n = 256;
					float3 step_volume = ray.dir * ray.length / (float(n) - 1.0f);

					// This piece of code from Deakin makes performance smoother in some cases.
					// Deakin's words:
						// This test fixes a performance regression if view is oriented with edge/s of the volume
  						// perhaps due to precision issues with the bounding box intersection
					float3 early_exit_test = ray.origin + step_volume;
					if (any(early_exit_test <= 0) || any(early_exit_test >= 1)) {
						return fixed4(0, 0, 0, 0);
					}

					// Final setup
					float3 currentRayPos = ray.origin;
					half oneMinusAlpha = 1;
					fixed4 dst = fixed4(0, 0, 0, 0);

					bool empty = false;

					[loop]
					for (int iter = 0; iter < n; iter++)
					{
						// Sample the texture and set the value to 0 if it is outside the slice or not within the value thresholds
						float density = tex3D(_Volume, currentRayPos);
						float4 src = tex2D(_Transfer, density);

						oneMinusAlpha = 1 - dst.a;
						src.rgb *= src.a;
						dst = mad(src, oneMinusAlpha, dst);

						currentRayPos += step_volume;
					}

					dst = saturate(dst);

					return dst;
				}
			ENDCG
			}
		}
}
