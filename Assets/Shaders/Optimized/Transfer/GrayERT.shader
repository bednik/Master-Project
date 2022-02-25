Shader "VolumeRendering/Optimized/ERT"
{
	Properties
	{
		_Volume("Volume", 3D) = "" {}
		_Transfer("Transfer function", 2D) = "" {}
		_Intensity("Intensity", Range(1.0, 5.0)) = 1.2
		_ERT("Stop the ray after this amount of time", Range(0.0, 1.0)) = 0.95
		_SliceMin("Slice min", Vector) = (-0.5, -0.5, -0.5, 1.0)
		_SliceMax("Slice max", Vector) = (0.5, 0.5, 0.5, 1.0)
		_bbMin("Volume's minimum coord of bounding box", Vector) = (-0.5, -0.5, -0.5, 1.0)
		_bbMax("Volume's maximum coord of bounding box", Vector) = (0.5, 0.5, 0.5, 1.0)
	}

		SubShader
		{
			// Setting the renderqueue to "Transparent" makes it prettier in the editor
			//Tags { "Queue" = "Transparent" "DisableBatching" = "True"}
			Blend SrcAlpha OneMinusSrcAlpha

			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				sampler3D _Volume;
				sampler2D _Transfer;
				half _Intensity, _ThresholdMin, _ThresholdMax, _ERT;
				half3 _SliceMin, _SliceMax;
				float3 _bbMin, _bbMax;
				#define SAMPLEPOINTS 256

				struct Ray {
					float3 origin;
					float3 dir;
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
				};

				// Returns 1 if point p is within the specified slice boundaries, 1 otherwise
				float InsideSlice(float3 p) {
					return step(_SliceMin.x, p.x) * step(_SliceMin.y, p.y) * step(_SliceMin.z, p.z) * step(p.x, _SliceMax.x) * step(p.y, _SliceMax.y) * step(p.z, _SliceMax.z);
				}

				// Returns 1 if texture value is within the specified thresholds, 0 otherwise
				float InsideThreshold(float val) {
					return step(_ThresholdMin, val) * step(val, _ThresholdMax);
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
					o.world = mul(unity_ObjectToWorld, v.pos).xyz;
					o.local = v.pos.xyz;
					return o;
				}

				// Fragment kernel //
				fixed4 frag(v2f i) : SV_Target
				{
					Ray ray;
					ray.origin = i.local;

					float3 dir = (i.world - _WorldSpaceCameraPos);
					ray.dir = normalize(mul(unity_WorldToObject, dir));

					float2 boundingBoxIntersections = intersectAABB(ray.origin, ray.dir);
					float3 ray_step = calculateStep(boundingBoxIntersections, ray);

					float4 dst = float4(0, 0, 0, 0);
					float3 current_ray_pos = ray.origin;

					float oneMinusAlpha = 0;

					[loop]
					for (int iter = 0; iter < SAMPLEPOINTS; iter++)
					{
						// Sample the texture and set the value to 0 if it is outside the slice or not within the value thresholds
						float density = tex3D(_Volume, current_ray_pos + 0.5f) * InsideSlice(current_ray_pos);

						// Two extra texture memory accesses. Can be merged by using a 16-bit 2-channel texture (or 32 bit for color)
						float4 src = tex2D(_Transfer, density);

						oneMinusAlpha = 1 - dst.a;
						dst.a = mad(src.a, oneMinusAlpha, dst.a);
						dst.rgb = mad(src.rgb * src.a, oneMinusAlpha, dst.rgb);

						if (dst.a >= _ERT) {
							dst.a = 1;
							break;
						}

						current_ray_pos += ray_step;

						if (InsideSlice(current_ray_pos) == 0) {
							break;
						}
					}

					dst = saturate(dst);

					return dst;
				}
			ENDCG
			}
		}
}