
#include "MMD4Mecanim-MMDLit-Lighting.cginc"

#define mmd_globalLighting (0.6)

#define FIXED_EDGESIZE

half4 _EdgeColor;
half _EdgeSize;

sampler2D _DefSA2CTex;
half _DefSA2CSize;
half4 _DefClearColor;

inline float4 MMDLit_GetEdgeVertex(float4 vertex, float3 normal)
{
#ifdef FIXED_EDGESIZE
	// Fixed size as MMD
	float edge_size = _EdgeSize * 0.002;
#else
	// Adjust edge_size by distance & fovY
	float4 world_pos = mul(UNITY_MATRIX_MV, vertex);
	float r_proj_near = (-UNITY_MATRIX_P[3][2] - UNITY_MATRIX_P[2][2]) / UNITY_MATRIX_P[2][3];
	float r_proj_y = UNITY_MATRIX_P[1][1] * r_proj_near * 0.5f;
	float edge_size = abs(0.002 * _EdgeSize / r_proj_y * world_pos.z * r_proj_near);
#endif
	return vertex + float4(normal.xyz * edge_size,0.0);
}

inline void MMDLit_ClipSA2C(float4 screen)
{
	half alpha = _EdgeColor.a;
	float2 screenPos = screen.xy / screen.w; // 0.0 - 1.0
	float2 screenScale = _ScreenParams.xy / _DefSA2CSize;
	clip(alpha - max((half)tex2D(_DefSA2CTex, screenPos * screenScale).a, 1.1 / 255.0));
}

inline half MMDLit_GetAlpha()
{
	return _EdgeColor.a;
}

inline half3 MMDLit_GetAlbedo(out half alpha)
{
	alpha = _EdgeColor.a;
	return _EdgeColor.rgb;
}

inline half3 MMDLit_GetAlbedoClipSA2C(float4 screen)
{
	half alpha = _EdgeColor.a;
	half3 albedo = _EdgeColor.rgb;
	float2 screenPos = screen.xy / screen.w; // 0.0 - 1.0
	float2 screenScale = _ScreenParams.xy / _DefSA2CSize;
	clip(alpha - max((half)tex2D(_DefSA2CTex, screenPos * screenScale).a, 1.1 / 255.0));
	return albedo;
}

inline half4 MMDLit_DirLightmap(
	half4 color,
	half4 scale,
	half3 normal,
	bool surfFuncWritesNormal)
{
	UNITY_DIRBASIS
	half3 scalePerBasisVector;
	half3 lm = MMDLit_DirLightmapDiffuse(unity_DirBasis, color, scale, normal, surfFuncWritesNormal, scalePerBasisVector);
	return half4(lm, 0);
}

#undef mmd_globalLighting
