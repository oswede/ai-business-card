// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

#include "HLSLSupport.cginc"
#include "UnityShaderVariables.cginc"
#define UNITY_PASS_PREPASSBASE
#include "UnityCG.cginc"
#include "Lighting.cginc"

#define INTERNAL_DATA
#define WorldReflectionVector(data,normal) data.worldRefl
#define WorldNormalVector(data,normal) normal

#include "MMD4Mecanim-MMDLit-Deferred-Surface-Lighting.cginc"

struct v2f_surf
{
	float4 pos : SV_POSITION;
	fixed3 normal : TEXCOORD0;
	float2 pack0 : TEXCOORD1;
	float4 screen : TEXCOORD2;
};

float4 _MainTex_ST;
v2f_surf vert_surf(appdata_full v)
{
	v2f_surf o;
	o.pos = UnityObjectToClipPos (v.vertex);
	o.normal = mul((float3x3)unity_ObjectToWorld, SCALED_NORMAL);
	o.pack0.xy = TRANSFORM_TEX(v.texcoord, _MainTex);
	o.screen = ComputeScreenPos(o.pos);
	return o;
}

fixed4 frag_surf(v2f_surf IN) : COLOR
{
	MMDLit_ClipSA2C(IN.pack0.xy, IN.screen);
	return fixed4(IN.normal * 0.5 + 0.5, 0.0); // No supported specular.
}
