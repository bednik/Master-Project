#include "UnityCG.cginc"

sampler3D _Volume;


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