// Deakin and Knackstead: https://link.springer.com/article/10.1007/s41095-019-0155-y
Shader "VolumeRendering/Optimized/Chebyshev"
{
	Properties
	{
		_Volume("Volume", 3D) = "" {}
		_Transfer("Transfer function", 2D) = "" {}
		_DistanceMap("Texture containing hints as to whether said space is empty", 3D) = "" {}
		_BlockSize("Dimension of each empty-space node", Int) = 8
		_ERT("Stop the ray after this amount of time", Range(0.0, 1.0)) = 0.95
		_VolumeDims("Dimensions of the volume", Vector) = (154, 154, 441)
        _OccupancyDims("Dimensions of the occupancy structure", Vector) = (10, 10, 28)
		_Quality("Quality factor for amount of sample points", Range(1.0, 5.0)) = 1.0
		_HighQuality("Hard-coded 256 points or based on dimension", Range(0, 1)) = 1
	}

		SubShader
		{
			// Setting the renderqueue to "Transparent" makes it prettier in the editor
			Tags { "Queue" = "Transparent" "DisableBatching" = "True"}
			Blend SrcAlpha OneMinusSrcAlpha
			LOD 0

			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_instancing
				#include "UnityCG.cginc"

				sampler3D _Volume;
				Texture3D _DistanceMap;
				sampler2D _Transfer;
				half _ERT;
				float3 _bbMin, _bbMax;
				half _BlockSize;
				int3 _VolumeDims, _OccupancyDims;
				half _Quality;
				int _HighQuality;

				struct Ray {
					float3 origin;
					float3 dir;
					float length;
				};

				struct vertexData
				{
					float4 pos : POSITION;
					float2 uv : TEXCOORD0;
					UNITY_VERTEX_INPUT_INSTANCE_ID
				};

				struct v2f
				{
					float4 vertex : SV_POSITION;
					float2 uv : TEXCOORD0;
					float3 world : TEXCOORD1;
					float3 local : TEXCOORD2;
					float3 t_0 : TEXCOORD3;
					UNITY_VERTEX_INPUT_INSTANCE_ID
					UNITY_VERTEX_OUTPUT_STEREO
				};

				// Adapted from https://stackoverflow.com/questions/28006184/get-component-wise-maximum-of-vector-in-glsl
				uint max3(uint3 v) {
					return max(max(v.x, v.y), v.z);
				}

				int min3(int3 v) {
					return min(min(v.x, v.y), v.z);
				}

				/*
					* Author: Lachlan Deakin
					* Date: Dec 9, 2021
					* File: shaders/volume_render.frag
					* Commit: 4b94c00
					* Type: Source code
					* Link: https://github.com/LDeakin/VkVolume/blob/master/shaders/volume_render.frag
				*/
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

				// Equation 14 (Deakin and Knackstead)
				int3 delta_i3(float3 delta_u, float3 u, float3 delta_u_inv, half dist) {
					return int3(ceil(((-delta_u > 0) + mad(sign(delta_u), dist, floor(u)) - u) * delta_u_inv));
				}

                // Equation 9 (Deakin and Knackstead)
                int delta_i(int3 delta_i3) {
                    return max(min3(delta_i3), 1);
                }

				// Vertex kernel //
				v2f vert(vertexData v)
				{
					v2f o;

					UNITY_SETUP_INSTANCE_ID(v);
					UNITY_TRANSFER_INSTANCE_ID(v, o);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

					o.vertex = UnityObjectToClipPos(v.pos);
					o.uv = v.uv;
					o.t_0 = v.pos.xyz + 0.5;
					o.world = mul(unity_ObjectToWorld, v.pos).xyz;
					o.local = v.pos.xyz;
					return o;
				}

				// Fragment kernel //
				// This fragment shader is heavily inspired by Lachlan Deakin's shader at https://github.com/LDeakin/VkVolume/blob/master/shaders/volume_render.frag
				// Modifications have been made to make it work with my software architecture, as well as follow my own style
				half4 frag(v2f vdata) : SV_Target
				{
					UNITY_SETUP_INSTANCE_ID(vdata);
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(vdata);

					// Determine ray direction and length
                    Ray ray;
					ray.origin = vdata.t_0;
					ray.dir = normalize(mul(unity_WorldToObject, vdata.world - _WorldSpaceCameraPos));
					float3 ray_exit = ray_caster_get_back(vdata.t_0, ray.dir);
  					ray.length = length(vdata.t_0 - ray_exit);
					

					// Calculate amount of sample points and step length (with direction)
					int n = (_HighQuality == 1) ? int(ceil(float(max3(_VolumeDims)) * ray.length * _Quality)) : 256;
					float3 step_volume = ray.dir * ray.length / (float(n) - 1.0f);

					// This piece of code from Deakin makes performance smoother in some cases.
					// Deakin's words:
						// This test fixes a performance regression if view is oriented with edge/s of the volume
  						// perhaps due to precision issues with the bounding box intersection
					float3 early_exit_test = ray.origin + step_volume;
					if (any(early_exit_test <= 0) || any(early_exit_test >= 1)) {
						return half4(0, 0, 0, 0);
					}

					// ESS values
					float3 volume_to_occupancy_u = _VolumeDims / _BlockSize;
					float3 step_occupancy = step_volume * volume_to_occupancy_u;
					float3 step_occupancy_inv = 1 / step_occupancy;
					int i_min = 0;
					int3 last_u_int = int3(0, 0, 0);
					int i_reverse = -int(ceil(_Quality));

					// Final setup
					float3 currentRayPos = ray.origin;
					float oneMinusAlpha = 1;
					float4 dst = float4(0, 0, 0, 0);

					bool empty = false;


                    // Test amount of samples
                    //int num_volume_samples = 0;
                    //int num_distance_samples = 0;
                    //

                    [loop]
                    for (int i = 0; i < n; i) {
						float3 u = volume_to_occupancy_u * currentRayPos;
						int3 u_int = int3(floor(u));
						
						[branch]
						if (empty && any(u_int != last_u_int)) {
                            //num_distance_samples++; // Test amount of samples
							float distance = _DistanceMap.Load(int4(u_int, 0));
							float distance_u = float(floor(distance * 255));
							empty = distance > 0;
							last_u_int = (empty) ? last_u_int : u_int;
							i = (empty) ? i + delta_i(delta_i3(step_occupancy, u, step_occupancy_inv, distance_u)) : int(max(i + i_reverse, i_min));
							currentRayPos = mad(i, step_volume, ray.origin);
						} else {
                            //num_volume_samples++; // Test amount of samples
							float density = tex3Dlod(_Volume, float4(currentRayPos, 0));
							float4 src = tex2Dlod(_Transfer, float4(density, 0, 0, 0));							
							empty = src.a <= 0;

							last_u_int = (empty) ? last_u_int : u_int;

							oneMinusAlpha = 1 - dst.a;
							dst.a = mad(src.a, oneMinusAlpha, dst.a);
							dst.rgb = mad(src.rgb * src.a, oneMinusAlpha, dst.rgb);

							if (dst.a >= _ERT) {
								dst.a = 1;
								break;
							}
							
							i++;
							i_min = i;
							currentRayPos += step_volume;
						}
					}

					dst = saturate(dst);

                    // Test amount of samples
                    //uint n_steps_max = uint(ceil(max3(_VolumeDims) * sqrt(3.0f)));
					//dst = float4(float(num_volume_samples) / float(n_steps_max), float(num_distance_samples) / float(n_steps_max), 0, 1); 
                    return dst;
				}
			ENDCG
			}
		}
}