#define FIXED_EDGESIZE

half4 _EdgeColor;
float _EdgeSize;

#define EDGE_BASESIZE 0.001

inline float MMDLit_GetEdgeSize()
{
	return _EdgeSize * EDGE_BASESIZE;
}

inline float4 MMDLit_GetEdgeVertex(float4 vertex, float3 normal)
{
#ifdef FIXED_EDGESIZE
	// Fixed size as MMD
	float edge_size = MMDLit_GetEdgeSize();
#else
	// Adjust edge_size by distance & fovY
	float4 world_pos = mul(UNITY_MATRIX_MV, vertex);
	float r_proj_near = (-UNITY_MATRIX_P[3][2] - UNITY_MATRIX_P[2][2]) / UNITY_MATRIX_P[2][3];
	float r_proj_y = UNITY_MATRIX_P[1][1] * r_proj_near * 0.5f;
	float edge_size = abs(MMDLit_GetEdgeSize() / r_proj_y * world_pos.z * r_proj_near);
#endif
	return vertex + float4(normal.xyz * edge_size,0.0);
}

inline half3 MMDLit_GetAlbedo(out half alpha)
{
	alpha = _EdgeColor.a;
	return (half3)_EdgeColor;
}

inline half3 MMDLit_Lighting(half3 albedo, half atten)
{
	return albedo * (half3)_LightColor0 * atten * 2.0;
}
