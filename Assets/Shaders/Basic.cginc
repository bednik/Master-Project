sampler3D _Volume;
half _Intensity, _ThresholdMin, _ThresholdMax;
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

// Fragmen kernel //
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

  float prev_alpha = 0;
  float oneMinusAlpha = 0;

  [unroll]
  for (int iter = 0; iter < SAMPLEPOINTS; iter++)
  {
	// Sample the texture and set the value to 0 if it is outside the slice or not within the value thresholds
	float textureVal = tex3D(_Volume, current_ray_pos + 0.5f).r * InsideSlice(current_ray_pos);
	float src = textureVal * InsideThreshold(textureVal);

	// Get the alpha directly from the texture, set the color by blending
	oneMinusAlpha = 1 - prev_alpha;
	dst.a += src;
	dst.rgb = mad(dst.rgb, oneMinusAlpha, src); // dst.rgb * (1 - prev_alpha) + src

	prev_alpha = src;
	current_ray_pos += ray_step;
  }

  dst = saturate(dst);

  return dst;
}