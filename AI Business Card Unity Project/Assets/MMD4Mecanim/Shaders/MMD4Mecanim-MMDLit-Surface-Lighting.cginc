
#include "MMD4Mecanim-MMDLit-Lighting.cginc"

#define SUPPORT_SELFSHADOWSTR
#define SUPPORT_LAMBERTSTR
#define SUPPORT_ADDLAMBERTSTR

half4 _Color;
half4 _Specular;
half4 _Ambient;
half _Shininess;
half _ShadowLum;
half _SelfShadowStr;
half _LambertStr;
half _AddLambertStr;
half _SphereAddMul;
half _SphereMulMul;
sampler2D _MainTex;
sampler2D _ToonTex;
sampler2D _SphereAddTex;
sampler2D _SphereMulTex;

#define mmd_centerAmbient (0.5)
#define mmd_globalLighting (0.6)

inline half3 MMDLit_GetTempAmbientL()
{
	return max((half3)mmd_centerAmbient - (half3)_Ambient, (half3)0) / (half3)mmd_centerAmbient;
}

inline half3 MMDLit_GetTempAmbient()
{
	half3 globalAmbient = (half3)UNITY_LIGHTMODEL_AMBIENT * 2.0;
	return globalAmbient * (1.0 - MMDLit_GetTempAmbientL());
}

inline half3 MMDLit_GetTempDiffuse()
{
	half3 globalAmbient = (half3)UNITY_LIGHTMODEL_AMBIENT * 2.0;
	half3 mmdLighting = min( (half3)_Ambient + (half3)_Color * mmd_globalLighting, 1.0 );
	return max(mmdLighting - MMDLit_GetTempAmbient(), 0.0);
}

inline half3 MMDLit_GetAlbedo(float2 uv_MainTex, half2 uv_Sphere)
{
	half3 c = (half3)tex2D(_MainTex, uv_MainTex);
	c += (half3)tex2D(_SphereAddTex, uv_Sphere) * _SphereAddMul;
	c *= (half3)tex2D(_SphereMulTex, uv_Sphere) * _SphereMulMul + (1.0 - _SphereMulMul);
	return c;
}

inline half3 MMDLit_GetAlbedo(float2 uv_MainTex, half2 uv_Sphere, out half alpha)
{
	half4 c = tex2D(_MainTex, uv_MainTex);
	half3 r = (half3)c;
	r += (half3)tex2D(_SphereAddTex, uv_Sphere) * _SphereAddMul;
	r *= (half3)tex2D(_SphereMulTex, uv_Sphere) * _SphereMulMul + (1.0 - _SphereMulMul);
	alpha = c.a * _Color.a;
	return r;
}

inline half MMDLit_GetToolRefl(half NdotL)
{
	return NdotL * 0.5 + 0.5;
}

inline half MMDLit_GetToonShadow(half toonRefl)
{
	half toonShadow = toonRefl * 2.0;
	return (half)saturate(toonShadow * toonShadow - 1.0);
}

// for ForwardAdd
inline half MMDLit_GetLambertAtten(half lambertStr)
{
	return lambertStr * _LambertStr + (1.0 - _LambertStr);
}

// for ForwardBase
inline half3 MMDLit_GetRamp(half NdotL, half lambertStr, half shadowAtten)
{
	half refl = (NdotL * 0.5 + 0.5) * shadowAtten;
	half toonRefl = refl;
#ifdef SUPPORT_SELFSHADOWSTR
	half selfShadowStrInv = 1.0 - _SelfShadowStr;
	refl = refl * selfShadowStrInv; // _SelfShadowStr = 1.0 as 0
#endif
	half3 ramp = (half3)tex2D(_ToonTex, half2(refl, refl));
#ifdef SUPPORT_SELFSHADOWSTR
	half toonShadow = MMDLit_GetToonShadow(toonRefl);
	half3 rampSS = (1.0 - toonShadow) * ramp + toonShadow;
	ramp = rampSS * _SelfShadowStr + ramp * selfShadowStrInv;
#endif
	ramp = saturate(1.0 - (1.0 - ramp) * _ShadowLum);
#ifdef SUPPORT_LAMBERTSTR
	ramp *= MMDLit_GetLambertAtten(lambertStr * shadowAtten);
#endif
	return ramp;
}

// for ForwardAdd
inline half3 MMDLit_GetRamp_Add(half toonRefl, half toonShadow, half lambertStr, half lambertAtten)
{
	half refl = toonRefl;
#ifdef SUPPORT_SELFSHADOWSTR
	half selfShadowStrInv = 1.0 - _SelfShadowStr;
	refl = refl * selfShadowStrInv; // _SelfShadowStr = 1.0 as 0
#endif
	half3 ramp = (half3)tex2D(_ToonTex, half2(refl, refl));
#ifdef SUPPORT_SELFSHADOWSTR
	half3 rampSS = (1.0 - toonShadow) * ramp + toonShadow;
	ramp = rampSS * _SelfShadowStr + ramp * selfShadowStrInv;
#endif
	ramp = saturate(1.0 - (1.0 - ramp) * _ShadowLum);
#ifdef SUPPORT_LAMBERTSTR
	ramp *= lambertAtten;
#endif
	return ramp;
}

// for Lightmap, DirLightmap
inline half3 MMDLit_GetRamp_Lightmap()
{
	half3 ramp = tex2D(_ToonTex, float2(1.0, 1.0));
	ramp = saturate(1.0 - (1.0 - ramp) * _ShadowLum);
#ifdef SUPPORT_SELFSHADOWSTR
	ramp = ramp * (1.0 - _SelfShadowStr) + _SelfShadowStr; // _SelfShadowStr = 1.0 as White
#endif
	// No shadowStr, because included lightColor.
	return (half3)ramp;
}

// DirLightmap
inline half3 MMDLit_GetRamp_DirLightmap(half NdotL, half lambertStr)
{
	half refl = (NdotL * 0.5 + 0.5);
#ifdef SUPPORT_SELFSHADOWSTR
	half selfShadowStrInv = 1.0 - _SelfShadowStr;
	refl = refl * selfShadowStrInv; // _SelfShadowStr = 1.0 as 0
#endif
	half3 ramp = (half3)tex2D(_ToonTex, half2(refl, refl));
#ifdef SUPPORT_SELFSHADOWSTR
	half3 rampSS = (1.0 - lambertStr) * ramp + lambertStr; // memo: Not use toonShadow.
	ramp = rampSS * _SelfShadowStr + ramp * selfShadowStrInv;
#endif
	ramp = saturate(1.0 - (1.0 - ramp) * _ShadowLum);
	// No shadowStr, because included lightColor.
	return ramp;
}

// for FORWARD_BASE
inline half3 MMDLit_Lighting(
	half3 albedo,
	half3 tempDiffuse,
	half NdotL,
	half lambertStr,
	half3 normal,
	half3 lightDir,
	half3 viewDir,
	half atten,
	half shadowAtten)
{
	half3 ramp = MMDLit_GetRamp(NdotL, lambertStr, shadowAtten);
	half3 lightColor = (half3)_LightColor0 * atten * 2.0;

	half3 c = tempDiffuse * lightColor * ramp;
	c *= albedo;

	half refl = MMDLit_SpecularRefl(normal, lightDir, viewDir, _Shininess);
	c += (half3)_Specular * mmd_globalLighting * lightColor * refl;
	return c;
}

// for FORWARD_ADD
inline half3 MMDLit_Lighting_Add(
	half3 albedo,
	half3 tempDiffuse,
	half toonRefl,
	half toonShadow,
	half lambertStr,
	half lambertAtten,
	half3 normal,
	half3 lightDir,
	half3 viewDir,
	half atten)
{
	half3 ramp = MMDLit_GetRamp_Add(toonRefl, toonShadow, lambertStr, lambertAtten);
	half3 lightColor = (half3)_LightColor0 * atten * 2.0;

	half c = tempDiffuse * lightColor * ramp;
	c *= albedo;

	half refl = MMDLit_SpecularRefl(normal, lightDir, viewDir, _Shininess);
	c += (half3)_Specular * mmd_globalLighting * lightColor * refl;
	return c;
}

inline half MMDLit_MulAtten(half atten, half shadowAtten)
{
	return atten * shadowAtten;
}

inline half3 MMDLit_Lightmap(
	half3 tempDiffuse,
	half4 lmtex)
{
	half3 lm = MMDLit_DecodeLightmap(lmtex);
	// lm = lightColor = _LightColor0.rgb * atten * 2.0
	half3 ramp = MMDLit_GetRamp_Lightmap();

	return tempDiffuse * lm * ramp + MMDLit_GetTempAmbient();
}

inline half3 MMDLit_DirLightmap(
	half3 tempDiffuse,
	half3 normal,
	half4 color,
	half4 scale,
	half3 viewDir,
	bool surfFuncWritesNormal,
	out half3 specColor)
{
	UNITY_DIRBASIS
	half3 scalePerBasisVector;
	half3 lm = MMDLit_DirLightmapDiffuse (unity_DirBasis, color, scale, normal, surfFuncWritesNormal, scalePerBasisVector);
	half3 lightDir = normalize(scalePerBasisVector.x * unity_DirBasis[0] + scalePerBasisVector.y * unity_DirBasis[1] + scalePerBasisVector.z * unity_DirBasis[2]);
	// lm = lightColor = _LightColor0.rgb * atten * 2.0

	half NdotL = dot(normal, lightDir);
	half lambertStr = max(NdotL, 0.0);
	half3 ramp = MMDLit_GetRamp_DirLightmap(NdotL, lambertStr);

	half3 c = tempDiffuse * lm * ramp + MMDLit_GetTempAmbient();

	half refl = MMDLit_SpecularRefl(normal, lightDir, viewDir, _Shininess);
	specColor = (half3)_Specular * mmd_globalLighting * lm * refl;
	return c;
}

#undef mmd_globalLighting
#undef mmd_centerAmbient
