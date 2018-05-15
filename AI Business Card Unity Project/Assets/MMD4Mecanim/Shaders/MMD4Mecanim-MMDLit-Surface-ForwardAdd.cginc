// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

#include "HLSLSupport.cginc"
#include "UnityShaderVariables.cginc"
#define UNITY_PASS_FORWARDADD
#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "AutoLight.cginc"

#define INTERNAL_DATA
#define WorldReflectionVector(data,normal) data.worldRefl
#define WorldNormalVector(data,normal) normal

#include "MMD4Mecanim-MMDLit-Surface-Lighting.cginc"

struct v2f_surf
{
	float4 pos : SV_POSITION;
	float2 pack0 : TEXCOORD0;
	half3 normal : TEXCOORD1;
	half4 lightDir : TEXCOORD2;
	half4 viewDir : TEXCOORD3;
	LIGHTING_COORDS(4,5)
	half4 mmd_uvSpherePack : TEXCOORD6;
	half4 mmd_tempDiffusePack : TEXCOORD7;
};

float4 _MainTex_ST;

v2f_surf vert_surf(appdata_full v)
{
	v2f_surf o;
	o.pos = UnityObjectToClipPos(v.vertex);
	o.pack0.xy = TRANSFORM_TEX(v.texcoord, _MainTex);
	o.normal = mul((float3x3)unity_ObjectToWorld, SCALED_NORMAL);
	half3 norm = normalize(mul((float3x3)UNITY_MATRIX_MV, v.normal));
	half3 eye = normalize(mul(UNITY_MATRIX_MV, v.vertex).xyz);
	half3 r = reflect(eye, norm);
	half m = 2.0 * sqrt(r.x * r.x + r.y * r.y + (r.z + 1.0) * (r.z + 1.0));
	o.mmd_uvSpherePack.xy = r.xy / m + 0.5;
	o.mmd_tempDiffusePack.xyz = MMDLit_GetTempDiffuse();
	o.lightDir.xyz = WorldSpaceLightDir(v.vertex);
	o.viewDir.xyz = WorldSpaceViewDir(v.vertex);
	half NdotL = dot(o.normal, o.lightDir);
	half toonRefl = MMDLit_GetToolRefl(NdotL);
	half lambertStr = max(NdotL, 0.0);
	o.mmd_uvSpherePack.z = toonRefl;
	o.mmd_uvSpherePack.w = lambertStr;
	o.mmd_tempDiffusePack.w = 1.0 - (1.0 - lambertStr) * _AddLambertStr; // addLambertStr
	o.lightDir.w = MMDLit_GetLambertAtten(lambertStr);
	o.viewDir.w = MMDLit_GetToonShadow(toonRefl);
	TRANSFER_VERTEX_TO_FRAGMENT(o);
	return o;
}

inline half3 frag_core(in v2f_surf IN, half3 albedo)
{
	half atten = LIGHT_ATTENUATION(IN);
	#ifndef USING_DIRECTIONAL_LIGHT
	half3 lightDir = normalize((half3)IN.lightDir);
	#else
	half3 lightDir = (half3)IN.lightDir;
	#endif

	half toonRefl = IN.mmd_uvSpherePack.z;
	half toonShadow = IN.viewDir.w;
	half lambertStr = IN.mmd_uvSpherePack.w;
	half lambertAtten = IN.lightDir.w;
	half3 c = MMDLit_Lighting_Add(albedo,
		(half3)IN.mmd_tempDiffusePack,
		toonRefl,
		toonShadow,
		lambertStr,
		lambertAtten,
		IN.normal,
		(half3)lightDir,
		normalize((half3)IN.viewDir),
		atten);
	#ifdef SUPPORT_ADDLAMBERTSTR
	half addLambertStr = IN.mmd_tempDiffusePack.w;
	c *= addLambertStr;
	#endif
	return c;
}

fixed4 frag_surf(v2f_surf IN) : COLOR
{
	half alpha;
	half3 albedo = MMDLit_GetAlbedo(IN.pack0.xy, (half2)IN.mmd_uvSpherePack, alpha);
	#if (defined(SHADER_API_GLES) && !defined(SHADER_API_GLES3)) && defined(SHADER_API_MOBILE)
	// Fix: GPU Adreno 205(OpenGL ES 2.0) discard crash
	#else
	clip(alpha - (1.1 / 255.0)); // Simulate MMD
	#endif

	half3 c = frag_core(IN, albedo);
	c = min(c, 1.0);
	c *= alpha;
	return fixed4(c, 0.0);
}

fixed4 frag_fast(v2f_surf IN) : COLOR
{
	half3 c = frag_core(IN, MMDLit_GetAlbedo(IN.pack0.xy, (half2)IN.mmd_uvSpherePack));
	return fixed4(c, 0.0);
}
