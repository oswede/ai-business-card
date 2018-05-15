Shader "MMD4Mecanim/Deferred/MMDLit-Transparent"
{
	Properties
	{
		_Color("Diffuse", Color) = (1,1,1,1)
		_Specular("Specular", Color) = (1,1,1)
		_Ambient("Ambient", Color) = (1,1,1)
		_Shininess("Shininess", Float) = 0
		_ShadowLum("ShadowLum", Range(0,10)) = 1.5
		_SelfShadowStr("SelfShadowStr", Range(0,1)) = 1.0
		_LambertStr("LambertStr", Range(0,1)) = 0.0
		_AddLambertStr("AddLambertStr", Range(0,1)) = 0.0
		_EdgeColor("EdgeColor", Color) = (0,0,0,1)
		_EdgeSize("EdgeSize", Range(0,2)) = 0.0
		_MainTex("MainTex", 2D) = "white" {}
		_ToonTex("ToonTex", 2D) = "white" {}
		_SphereAddMul("SphereAddMul", Range(0,1)) = 1.0
		_SphereMulMul("SphereMulMul", Range(0,1)) = 1.0
		_SphereAddTex("SphereAddTex", 2D) = "black" {}
		_SphereMulTex("SphereMulTex", 2D) = "white" {}

		_DefLightDir("DefLightDir",Vector) = (0,0,1,1)
		_DefLightAtten("DefLightAtten",Float) = 0.5
		_DefLightColor0("DefLightColor0", Color) = (1,1,1,1)

		_DefSA2CTex("DefSA2CTex", 2D) = "black" {}
		_DefSA2CSize("DefSA2CSize", float) = 1.0
		_DefClearColor("DefClearColor", Color) = (0,0,0,0)
	}

	SubShader
	{
		Tags { "Queue" = "Geometry+999" "RenderType" = "Transparent" }
		LOD 200

		Cull Back
		ZWrite On
		Blend Off

		Pass {
			Name "PREPASS"
			Tags { "LightMode" = "PrePassBase" }
			Fog {Mode Off}
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert_surf
			#pragma fragment frag_surf
			#pragma fragmentoption ARB_precision_hint_fastest
			#include "MMD4Mecanim-MMDLit-Deferred-Surface-SA2C-PrePassBase.cginc"
			ENDCG
		}

		Pass {
			Name "PREPASS"
			Tags { "LightMode" = "PrePassFinal" }
			ZWrite Off
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert_surf
			#pragma fragment frag_surf
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_prepassfinal
			#include "MMD4Mecanim-MMDLit-Deferred-Surface-SA2C-PrePassFinal.cginc"
			ENDCG
		}

		GrabPass {
			Tags { "LightMode" = "PrePassFinal" }
			Name "PREPASS"
		}

		Pass {
			Name "PREPASS"
			Tags { "LightMode" = "PrePassFinal" }
			ZTest Equal
			ZWrite Off
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert_surf
			#pragma fragment frag_surf
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_prepassfinal
			#include "MMD4Mecanim-MMDLit-Deferred-Surface-SA2C-PrePassFinal2.cginc"
			ENDCG
		}

		Pass {
			Name "PREPASS"
			Tags { "LightMode" = "PrePassFinal" }
			ZTest Less
			ZWrite On
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert_surf
			#pragma fragment frag_surf
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_prepassfinal
			#include "MMD4Mecanim-MMDLit-Deferred-Surface-SA2C-PrePassFinal3.cginc"
			ENDCG
		}
	}

	Fallback "MMD4Mecanim/MMDLit-Transparent"
}
