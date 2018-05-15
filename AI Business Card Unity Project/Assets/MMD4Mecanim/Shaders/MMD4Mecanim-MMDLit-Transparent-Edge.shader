Shader "MMD4Mecanim/MMDLit-Transparent-Edge"
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
		Blend SrcAlpha OneMinusSrcAlpha
		ColorMask RGB

		Pass {
			Name "FORWARD"
			Tags { "LightMode" = "ForwardBase" }

			CGPROGRAM
			#pragma target 2.0
			#pragma exclude_renderers flash
			#pragma vertex vert_surf
			#pragma fragment frag_surf
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_fwdbase
			#include "MMD4Mecanim-MMDLit-Surface-ForwardBase.cginc"
			ENDCG
		}

		Pass {
			Name "FORWARD"
			Tags { "LightMode" = "ForwardAdd" }

			ZWrite Off Blend One One Fog { Color (0,0,0,0) }
			CGPROGRAM
			#pragma target 2.0
			#pragma exclude_renderers flash
			#pragma vertex vert_surf
			#pragma fragment frag_surf
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_fwdadd
			#include "MMD4Mecanim-MMDLit-Surface-ForwardAdd.cginc"
			ENDCG
		}

		Blend Off
		ColorMask RGBA

		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }
			Fog {Mode Off}
			ZWrite On ZTest LEqual Cull Off
			Offset 1, 1
			CGPROGRAM
			#pragma target 2.0
			#pragma exclude_renderers flash
			#pragma vertex vert_surf
			#pragma fragment frag_surf
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_shadowcaster
			#include "MMD4Mecanim-MMDLit-Surface-ShadowCaster.cginc"
			ENDCG
		}

		Pass {
			Name "ShadowCollector"
			Tags { "LightMode" = "ShadowCollector" }
			Fog {Mode Off}
			ZWrite On ZTest LEqual
			CGPROGRAM
			#pragma target 2.0
			#pragma exclude_renderers flash
			#pragma vertex vert_surf
			#pragma fragment frag_surf
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_shadowcollector
			#include "MMD4Mecanim-MMDLit-Surface-ShadowCollector.cginc"
			ENDCG
		}
		
		Cull Front
		ZWrite On
		Blend SrcAlpha OneMinusSrcAlpha
		ColorMask RGB

		Pass {
			Name "FORWARD"
			Tags { "LightMode" = "ForwardBase" }

			CGPROGRAM
			#pragma target 2.0
			#pragma exclude_renderers flash
			#pragma vertex vert_surf
			#pragma fragment frag_surf
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_fwdbase
			#include "MMD4Mecanim-MMDLit-SurfaceEdge-ForwardBase.cginc"
			ENDCG
		}

		Pass {
			Name "FORWARD"
			Tags { "LightMode" = "ForwardAdd" }

			ZWrite Off Blend One One Fog { Color (0,0,0,0) }
			CGPROGRAM
			#pragma target 2.0
			#pragma exclude_renderers flash
			#pragma vertex vert_surf
			#pragma fragment frag_surf
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_fwdadd
			#include "MMD4Mecanim-MMDLit-SurfaceEdge-ForwardAdd.cginc"
			ENDCG
		}
	}

	Fallback Off
}
