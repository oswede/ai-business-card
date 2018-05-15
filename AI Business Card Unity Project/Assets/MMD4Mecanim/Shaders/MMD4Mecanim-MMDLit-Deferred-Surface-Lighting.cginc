
#include "MMD4Mecanim-MMDLit-Lighting.cginc"

#define SUPPORT_SELFSHADOWSTR
#define SUPPORT_LAMBERTSTR

#define mmd_centerAmbient (0.5)
#define mmd_globalLighting (0.6)

half4 _Color;
half4 _Ambient;
half4 _Specular;
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

half4 _DefLightDir;
half _DefLightAtten;
half4 _DefLightColor0;

sampler2D _DefSA2CTex;
float _DefSA2CSize;
half4 _DefClearColor;

#ifdef UNITY_PASS_PREPASSFINAL
half4 unity_Ambient;
#endif

inline half3 MMDLit_GetAmbient()
{
#ifdef UNITY_PASS_PREPASSFINAL
	return (half3)unity_Ambient;
#else
	return 0;
#endif
}

//------------------------------------------------------------------------------------------------------------------------

inline half3 MMDLit_GetTempAmbientL()
{
	return max((half3)mmd_centerAmbient - (half3)_Ambient, (half3)0) / (half3)mmd_centerAmbient;
}

inline half3 MMDLit_GetTempAmbient()
{
	half3 globalAmbient = MMDLit_GetAmbient();
	return globalAmbient * (1.0 - MMDLit_GetTempAmbientL());
}

inline half3 MMDLit_GetTempDiffuse( half3 tempAmbient )
{
	half3 mmdLighting = min( (half3)_Ambient + (half3)_Color * mmd_globalLighting, 1.0 );
	return max(mmdLighting - tempAmbient, 0.0);
}

//------------------------------------------------------------------------------------------------------------------------

inline half MMDLit_GetAlpha(float2 uv_MainTex)
{
	return (half)tex2D(_MainTex, uv_MainTex).a * _Color.a;
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

inline void MMDLit_ClipSA2C(float2 uv_MainTex, float4 screen)
{
	half alpha = MMDLit_GetAlpha(uv_MainTex);
	float2 screenPos = screen.xy / screen.w; // 0.0 - 1.0
	float2 screenScale = _ScreenParams.xy / _DefSA2CSize;
	clip(alpha - max((half)tex2D(_DefSA2CTex, screenPos * screenScale).a, 1.1 / 255.0));
}

inline half3 MMDLit_GetAlbedoClipSA2C(float2 uv_MainTex, half2 uv_Sphere, float4 screen, out half alpha)
{
	half3 albedo = MMDLit_GetAlbedo(uv_MainTex, uv_Sphere, alpha);
	float2 screenPos = screen.xy / screen.w; // 0.0 - 1.0
	float2 screenScale = _ScreenParams.xy / _DefSA2CSize;
	clip(alpha - max((half)tex2D(_DefSA2CTex, screenPos * screenScale).a, 1.1 / 255.0));
	return albedo;
}

inline half3 MMDLit_GetAlbedoClipSA2C(float2 uv_MainTex, half2 uv_Sphere, float4 screen)
{
	half alpha;
	return MMDLit_GetAlbedoClipSA2C(uv_MainTex, uv_Sphere, screen, alpha);
}

inline half MMDLit_GetToonShadow(half toonRefl)
{
	half toonShadow = toonRefl * 2.0;
	return (half)saturate(toonShadow * toonShadow - 1.0);
}

// for Lightmap / DirLightmap
inline half3 MMDLit_GetRamp_Lightmap()
{
	half3 ramp = (half3)tex2D(_ToonTex, half2(1.0, 1.0));
	ramp = saturate(1.0 - (1.0 - ramp) * _ShadowLum);
#ifdef SUPPORT_SELFSHADOWSTR
	ramp = ramp * (1.0 - _SelfShadowStr) + _SelfShadowStr; // _SelfShadowStr = 1.0 as White
#endif
	// No shadowStr, because included lightColor.
	return ramp;
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
	half3 rampSS = (1.0 - lambertStr) * ramp + lambertStr;
	ramp = rampSS * _SelfShadowStr + ramp * selfShadowStrInv;
#endif
	ramp = saturate(1.0 - (1.0 - ramp) * _ShadowLum);
	// No shadowStr, because included lightColor.
	return ramp;
}

inline half3 MMDLit_Lighting(
	half3 albedo,
	half3 normal,
	half3 lightColor0,
	half3 lightDir,
	half3 viewDir,
	half atten,
	half3 light)
{
	half3 lightColor = lightColor0 * atten * 2.0;
	half3 globalAmbient = MMDLit_GetAmbient();
	half3 globalLight = min(globalAmbient + lightColor, 1.0);

	half NdotL = dot(normal, lightDir);
	half lambertStr = max(NdotL, 0.0);
	half3 lambertLight = globalAmbient + lightColor * lambertStr;
	half3 additionalLight = max(light - lambertLight, 0.0);

	half shadowBias = 2.0; // 1.0 -
	half3 lightShadow = (light.rgb - globalAmbient) * shadowBias / max(globalLight - globalAmbient, 0.0001);
	half refl = MMDLit_Luminance(min(lightShadow, half3(1,1,1))); // SelfShadow
	half refl2 = NdotL * 0.5 + 0.5; // Lambert
	refl = min(refl, refl2); // = Lambert * shadowAtten
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
	half3 diffuseLight = min(light - globalAmbient, lightColor);
	diffuseLight = diffuseLight * _LambertStr + lightColor * (1.0 - _LambertStr);
#else
	half3 diffuseLight = lightColor;
#endif

	half3 tempAmbient = MMDLit_GetTempAmbient();
	half3 tempDiffuse = MMDLit_GetTempDiffuse( tempAmbient );
	half3 c = tempDiffuse * diffuseLight * ramp + tempAmbient;
	c *= albedo;
	c += additionalLight;

	refl = MMDLit_SpecularRefl(normal, lightDir, viewDir, _Shininess);
	c += (half3)_Specular * mmd_globalLighting * refl * lightColor;
	return c;
}

inline half3 MMDLit_Lightmap(
	half3 albedo,
	half3 light)
{
	half3 ramp = MMDLit_GetRamp_Lightmap();

	half3 tempAmbient = MMDLit_GetTempAmbient();
	half3 tempDiffuse = MMDLit_GetTempDiffuse( tempAmbient );
	half3 c = tempDiffuse * light * ramp + tempAmbient;
	c *= albedo;
	return c;
}

inline half3 MMDLit_DirLightmap(
	half3 albedo,
	half3 normal,
	half4 color,
	half4 scale,
	half3 viewDir,
	half3 light,
	bool surfFuncWritesNormal)
{
	UNITY_DIRBASIS
	half3 scalePerBasisVector;
	half3 lm = MMDLit_DirLightmapDiffuse (unity_DirBasis, color, scale, normal, surfFuncWritesNormal, scalePerBasisVector);
	half3 lightDir = normalize(scalePerBasisVector.x * unity_DirBasis[0] + scalePerBasisVector.y * unity_DirBasis[1] + scalePerBasisVector.z * unity_DirBasis[2]);

	light += lm;

	half NdotL = dot(normal, lightDir);
	half lambertStr = max(NdotL, 0.0);
	half3 ramp = MMDLit_GetRamp_DirLightmap(NdotL, lambertStr);
	
	half3 tempAmbient = MMDLit_GetTempAmbient();
	half3 tempDiffuse = MMDLit_GetTempDiffuse( tempAmbient );
	half3 c = tempDiffuse * light * ramp + tempAmbient;
	c *= albedo;

	half refl = MMDLit_SpecularRefl(normal, lightDir, viewDir, _Shininess);
	c += (half3)_Specular * mmd_globalLighting * refl * light;
	return c;
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
#undef mmd_centerAmbient
