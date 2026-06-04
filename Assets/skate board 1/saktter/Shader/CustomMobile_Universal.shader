Shader "CustomMobile/Universal" {
	Properties {
		_Color ("Color", Vector) = (1,1,1,1)
		_ReflectionColor ("Reflection Color", Vector) = (1,1,1,1)
		_MainTex ("Base (RGB) Gloss (A)", 2D) = "white" {}
		_BumpMap ("Normalmap", 2D) = "bump" {}
		_BumpScale ("Scale", Float) = 1
		_Cube ("Reflection Cubemap", Cube) = "" {}
		_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
		_Shininess ("Shininess", Range(0.03, 1)) = 0.078125
		_Roughness ("Roughness", Range(0.03, 1)) = 0.078125
		[HideInInspector] _LightMode ("_LightMode", Float) = 2
		[HideInInspector] _ColorMode ("_ColorMode", Float) = 0
		[HideInInspector] _ReflMode ("_ReflMode", Float) = 0
		[HideInInspector] _LodLimit ("_LodLimit", Float) = 500
		[HideInInspector] _FeatureMask ("_FeatureMask", Float) = 2
		[HideInInspector] _BlendMode ("_BlendMode", Float) = 0
		[HideInInspector] _SrcBlend ("_SrcBlend", Float) = 1
		[HideInInspector] _DstBlend ("_DstBlend", Float) = 0
		[HideInInspector] _ZWrite ("_ZWrite", Float) = 1
		[HideInInspector] _FogDisabled ("_FogDisabled", Float) = 0
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200
		CGPROGRAM
#pragma surface surf Standard
#pragma target 3.0

		sampler2D _MainTex;
		fixed4 _Color;
		struct Input
		{
			float2 uv_MainTex;
		};
		
		void surf(Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;
			o.Alpha = c.a;
		}
		ENDCG
	}
	Fallback "Mobile/VertexLit"
	//CustomEditor "MobileUniversalShaderInspector"
}