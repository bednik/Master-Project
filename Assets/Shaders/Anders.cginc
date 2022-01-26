#ifndef __VOLUME_RENDERING_INCLUDED__
#define __VOLUME_RENDERING_INCLUDED__

#include "UnityCG.cginc"

#ifndef ITERATIONS
#define ITERATIONS 50
#endif

half4 _Color;
sampler3D _Volume;
half _Intensity, _ThresholdMin, _ThresholdMax;
half3 _SliceMin, _SliceMax;
float4x4 _AxisRotationMatrix;

struct Ray {
	float3 origin;
	float3 dir;
};

struct AABB { //Axis-Aligned Bounding Box
	float3 min;
	float3 max;
};

bool intersect(Ray r, AABB aabb, out float t0, out float t1)
{
	float3 invR = 1.0 / r.dir;
	float3 tbot = invR * (aabb.min - r.origin);
	float3 ttop = invR * (aabb.max - r.origin);
	float3 tmin = min(ttop, tbot);
	float3 tmax = max(ttop, tbot);
	float2 t = max(tmin.xx, tmin.yz);
	t0 = max(t.x, t.y);
	t = min(tmax.xx, tmax.yz);
	t1 = min(t.x, t.y);
	return t0 <= t1;
}

float3 localize(float3 p) {
	return mul(unity_WorldToObject, float4(p, 1)).xyz;
}

float3 get_uv(float3 p) {
	return (p + 0.5);
}

float sample_volume(float3 uv, float3 p)
{
	float v = tex3D(_Volume, uv).r * _Intensity;

	float3 axis = mul(_AxisRotationMatrix, float4(p, 0)).xyz;
	axis = get_uv(axis);
	float min = step(_SliceMin.x, axis.x) * step(_SliceMin.y, axis.y) * step(_SliceMin.z, axis.z);
	float max = step(axis.x, _SliceMax.x) * step(axis.y, _SliceMax.y) * step(axis.z, _SliceMax.z);

	return v * min * max;
}

bool outside(float3 uv)
{
	const float EPSILON = 0.01;
	float lower = -EPSILON;
	float upper = 1 + EPSILON;
	return (
		uv.x < lower || uv.y < lower || uv.z < lower ||
		uv.x > upper || uv.y > upper || uv.z > upper
		);
}

struct appdata
{
	float4 vertex : POSITION;
	float2 uv : TEXCOORD0;
};

struct v2f
{
	float4 vertex : SV_POSITION;
	float2 uv : TEXCOORD0;
	float3 world : TEXCOORD1;
	float3 local : TEXCOORD2;
};


v2f vert(appdata v)
{
	v2f o;
	o.vertex = UnityObjectToClipPos(v.vertex);
	o.uv = v.uv;
	o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
	o.local = v.vertex.xyz;
	return o;
}

//SV_Target: output semantic - front primary color
fixed4 frag(v2f i) : SV_Target
{
  Ray ray;
  ray.origin = i.local;

  // World space direction to object space
  float3 dir = (i.world - _WorldSpaceCameraPos);
  float dist0 = length(dir);
  ray.dir = normalize(mul(unity_WorldToObject, dir));

  AABB aabb;
  aabb.min = float3(-0.5, -0.5, -0.5);
  aabb.max = float3(0.5, 0.5, 0.5);

  float tnear;
  float tfar;
  intersect(ray, aabb, tnear, tfar);

  tnear = max(0.0, tnear);


  float3 start = ray.origin;
  float3 end = ray.origin + ray.dir * tfar;
  float dist = abs(tfar - tnear);
  float step_size = dist / float(ITERATIONS);
  float3 ray_step = normalize(end - start) * step_size;

  float4 dst = float4(0, 0, 0, 0);
  float3 current_ray_pos = start;

  float alpha = 0;

  [unroll]
  for (int iter = 0; iter < ITERATIONS; iter++)
  {
	float3 uv = get_uv(current_ray_pos);
	float v = sample_volume(uv, current_ray_pos);
	float4 src = float4(v, v, v, v);

	//Filter out values outside of threshold
	if (src.a > _ThresholdMin && src.a < _ThresholdMax) {
		//Accumulate opacity value
		dst.a += src.a;
		//Accumulate and blend color value
		dst.rgb = dst.rgb * (1 - alpha) + src.a;
	}

	alpha = src.a;
	current_ray_pos += ray_step;
	if (dst.a > _ThresholdMax) break;
  }

  //Saturate makes sure each of the vector components are within [0,1]
  dst = saturate(dst);

  //if	  (dst.r > 0.8) dst.rbg = 1;
  //else if (dst.r < 0.2) dst.rgb = 0;

  return dst;

}

#endif 