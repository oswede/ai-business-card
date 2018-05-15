#include "MMD4Mecanim-MMDLit-Deferred-Surface-PrePassFinal-Core.cginc"

fixed4 frag_surf(v2f_surf IN) : COLOR
{
	half3 albedo = MMDLit_GetAlbedo(IN.pack0.xy, IN.mmd_uvSphere);
	half3 c = frag_core(IN, albedo);
	return fixed4(c, 1.0);
}
