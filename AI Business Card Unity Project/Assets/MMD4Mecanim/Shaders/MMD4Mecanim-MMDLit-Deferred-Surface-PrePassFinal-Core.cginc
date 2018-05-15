// Upgrade NOTE: commented out 'float4 unity_LightmapST', a built-in variable
// Upgrade NOTE: commented out 'sampler2D unity_Lightmap', a built-in variable
// Upgrade NOTE: commented out 'sampler2D unity_LightmapInd', a built-in variable
// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'
// Upgrade NOTE: replaced tex2D unity_Lightmap with UNITY_SAMPLE_TEX2D
// Upgrade NOTE: replaced tex2D unity_LightmapInd with UNITY_SAMPLE_TEX2D_SAMPLER

#include "HLSLSupport.cginc"
#include "UnityShaderVariables.cginc"
#define UNITY_PASS_PREPASSFINAL
#include "UnityCG.cginc"
#include "Lighting.cginc"

#define INTERNAL_DATA
#define WorldReflectionVector(data,normal) data.worldRefl
#define WorldNormalVector(data,normal) normal

#include "MMD4Mecanim-MMDLit-Deferred-Surface-Lighting.cginc"

struct v2f_surf
{
	float4 pos : SV_POSITION;
	float2 pack0 : TEXCOORD0;
	float4 screen : TEXCOORD1;
	half3 normal : TEXCOORD2;
	half2 mmd_uvSphere : TEXCOORD3;
	#ifdef LIGHTMAP_OFF
	half3 vlight : TEXCOORD4;
	half3 viewDir : TEXCOORD5;
	#else
	float2 lmap : TEXCOORD4;
	#ifdef DIRLIGHTMAP_OFF
	float4 lmapFadePos : TEXCOORD5;
	#else
	half3 viewDir : TEXCOORD5;
	#endif
	#endif
};

#ifndef LIGHTMAP_OFF
// float4 unity_LightmapST;
#endif
float4 _MainTex_ST;

v2f_surf vert_surf(appdata_full v)
{
	v2f_surf o;
	o.pos = UnityObjectToClipPos(v.vertex);
	float3 worldN = mul((float3x3)unity_ObjectToWorld, SCALED_NORMAL);
	o.normal = worldN;
	o.pack0.xy = TRANSFORM_TEX(v.texcoord, _MainTex);
	half3 norm = normalize(mul((float3x3)UNITY_MATRIX_MV, v.normal));
	half3 eye = normalize(mul(UNITY_MATRIX_MV, v.vertex).xyz);
	half3 r = reflect(eye, norm);
	half m = 2.0 * sqrt(r.x * r.x + r.y * r.y + (r.z + 1.0) * (r.z + 1.0));
	o.mmd_uvSphere = r.xy / m + 0.5;
	o.screen = ComputeScreenPos(o.pos);

	#ifndef LIGHTMAP_OFF
	o.lmap.xy = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
	#ifdef DIRLIGHTMAP_OFF
	o.lmapFadePos.xyz = (mul(unity_ObjectToWorld, v.vertex).xyz - unity_ShadowFadeCenterAndType.xyz) * unity_ShadowFadeCenterAndType.w;
	o.lmapFadePos.w = (-mul(UNITY_MATRIX_MV, v.vertex).z) * (1.0 - unity_ShadowFadeCenterAndType.w);
	#endif
	#else
	o.vlight = ShadeSH9(float4(worldN, 1.0));
	#endif

	#ifndef DIRLIGHTMAP_OFF
	TANGENT_SPACE_ROTATION;
	o.viewDir = mul(rotation, ObjSpaceViewDir(v.vertex));
	#else
	#ifdef LIGHTMAP_OFF
	o.viewDir = (half3)WorldSpaceViewDir(v.vertex);
	#endif
	#endif
	return o;
}

sampler2D _LightBuffer;
#if defined (SHADER_API_XBOX360) && defined (HDR_LIGHT_PREPASS_ON)
sampler2D _LightSpecBuffer;
#endif
#ifndef LIGHTMAP_OFF
// sampler2D unity_Lightmap;
// sampler2D unity_LightmapInd;
float4 unity_LightmapFade;
#endif

inline half3 frag_core(v2f_surf IN, half3 albedo)
{
	half4 light = tex2Dproj(_LightBuffer, UNITY_PROJ_COORD(IN.screen));
	#if defined (SHADER_API_GLES) || defined (SHADER_API_GLES3)
	light = max(light, half4(0.001));
	#endif
	#ifndef HDR_LIGHT_PREPASS_ON
	light = -log2(light);
	#endif
	#if defined (SHADER_API_XBOX360) && defined (HDR_LIGHT_PREPASS_ON)
	//light.w = tex2Dproj(_LightSpecBuffer, UNITY_PROJ_COORD(IN.screen)).r;
	#endif

	#ifndef LIGHTMAP_OFF
	#ifdef DIRLIGHTMAP_OFF
	half4 lmtex = UNITY_SAMPLE_TEX2D(unity_Lightmap, IN.lmap.xy);
	half4 lmtex2 = UNITY_SAMPLE_TEX2D_SAMPLER(unity_LightmapInd,unity_Lightmap, IN.lmap.xy);
	half lmFade = length(IN.lmapFadePos) * unity_LightmapFade.z + unity_LightmapFade.w;
	half3 lmFull = MMDLit_DecodeLightmap(lmtex);
	half3 lmIndirect = MMDLit_DecodeLightmap(lmtex2);
	half3 lm = lerp (lmIndirect, lmFull, saturate(lmFade));
	light.rgb += lm;
	half3 c = MMDLit_Lightmap(
		albedo,
		(half3)light);
	#else
	half4 lmtex = UNITY_SAMPLE_TEX2D(unity_Lightmap, IN.lmap.xy);
	half4 lmIndTex = UNITY_SAMPLE_TEX2D_SAMPLER(unity_LightmapInd,unity_Lightmap, IN.lmap.xy);
	//half4 lm = MMDLit_DirLightmap(lmtex, lmIndTex, IN.normal, 0);
	//light += lm;
	half3 c = MMDLit_DirLightmap(
		albedo,
		IN.normal,
		lmtex,
		lmIndTex,
		normalize(IN.viewDir),
		(half3)light,
		0);
	#endif
	#else
	light.rgb += IN.vlight;
	half3 c = MMDLit_Lighting(
		albedo,
		IN.normal,
		_DefLightColor0,
		_DefLightDir,
		IN.viewDir,
		_DefLightAtten,
		light);
	#endif

	return c;
}
