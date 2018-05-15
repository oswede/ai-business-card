// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

#include "HLSLSupport.cginc"
#include "UnityShaderVariables.cginc"
#define UNITY_PASS_PREPASSFINAL
#include "UnityCG.cginc"
#include "Lighting.cginc"

#define INTERNAL_DATA
#define WorldReflectionVector(data,normal) data.worldRefl
#define WorldNormalVector(data,normal) normal

#include "MMD4Mecanim-MMDLit-Deferred-SurfaceEdge-Lighting.cginc"

struct v2f_surf
{
	float4 pos : SV_POSITION;
	float4 screen : TEXCOORD0;
};

float4 _MainTex_ST;
v2f_surf vert_surf(appdata_full v)
{
	v2f_surf o;
	v.vertex = MMDLit_GetEdgeVertex(v.vertex, v.normal);
	o.pos = UnityObjectToClipPos(v.vertex);
	o.screen = ComputeScreenPos(o.pos);
	return o;
}

sampler2D _GrabTexture;

inline void grab_add(fixed2 uv, inout half3 totalColor, inout half totalCount, half bias)
{
	half3 c = (half3)tex2D(_GrabTexture, uv);
	half3 c2 = max(abs(c - (half3)_DefClearColor) - 0.01, 0);
	half r = any(c2) * bias;
	totalColor += c * r;
	totalCount += r;
}

fixed4 frag_surf(v2f_surf IN) : COLOR
{
	half alpha = MMDLit_GetAlpha();
	clip(-max(alpha - (1.0 - 1.1 / 255.0), 0.0) + min(alpha - (1.1 / 255.0), 0.0));

	fixed2 pos = IN.screen.xy / IN.screen.w;
	fixed2 px = _ScreenParams.zw - 1.0;
	fixed2 px2 = px * 2.0;

	half3 totalColor = (half3)tex2D(_GrabTexture, pos);
	half totalCount = 1;

	half globalBias = 1.0 - alpha;

	half bias0 = 1.0 * globalBias;
	half bias1 = 0.7 * globalBias;
	half bias2 = 0.5 * globalBias;

	grab_add(pos + fixed2( -px.x,-px2.y), totalColor, totalCount, bias2);
	grab_add(pos + fixed2(     0,-px2.y), totalColor, totalCount, bias2);
	grab_add(pos + fixed2(  px.x,-px2.y), totalColor, totalCount, bias2);

	grab_add(pos + fixed2(-px2.x, -px.y), totalColor, totalCount, bias2);
	grab_add(pos + fixed2( -px.x, -px.y), totalColor, totalCount, bias1);
	grab_add(pos + fixed2(     0, -px.y), totalColor, totalCount, bias0);
	grab_add(pos + fixed2(  px.x, -px.y), totalColor, totalCount, bias1);
	grab_add(pos + fixed2( px2.x, -px.y), totalColor, totalCount, bias2);
																					
	grab_add(pos + fixed2(-px2.x,     0), totalColor, totalCount, bias2);
	grab_add(pos + fixed2( -px.x,     0), totalColor, totalCount, bias0);
	grab_add(pos + fixed2(  px.x,     0), totalColor, totalCount, bias0);
	grab_add(pos + fixed2( px2.x,     0), totalColor, totalCount, bias2);

	grab_add(pos + fixed2(-px2.x,  px.y), totalColor, totalCount, bias2);
	grab_add(pos + fixed2( -px.x,  px.y), totalColor, totalCount, bias1);
	grab_add(pos + fixed2(     0,  px.y), totalColor, totalCount, bias0);
	grab_add(pos + fixed2(  px.x,  px.y), totalColor, totalCount, bias1);
	grab_add(pos + fixed2( px2.x,  px.y), totalColor, totalCount, bias2);

	grab_add(pos + fixed2( -px.x, px2.y), totalColor, totalCount, bias2);
	grab_add(pos + fixed2(     0, px2.y), totalColor, totalCount, bias2);
	grab_add(pos + fixed2(  px.x, px2.y), totalColor, totalCount, bias2);

	half3 c = totalColor / totalCount;
	return fixed4(c, 1.0);
}
