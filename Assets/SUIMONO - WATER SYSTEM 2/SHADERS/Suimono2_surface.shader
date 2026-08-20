Shader "Suimono2/surface" {


Properties {
	//_MainTex ("Particle Texture", 2D) = "white" {}
	_shallowColor ("Tint Color 1", Color) = (0.5,0.5,0.5,0.5)
	_depthColor ("Tint Color 2", Color) = (0.5,0.5,0.5,1.0)
	_BlendColor ("Blend Color", Color) = (0.0,0.0,0.0,0.0)
	_OverlayColor ("Overlay Color", Color) = (0.0,0.0,0.0,0.0)
	_ReflectionColor ("Reflection Color", Color) = (1.0,1.0,1.0,1.0)
	_SpecularColor ("Specular Color", Color) = (1.0,1.0,1.0,1.0)
	_SSSColor ("SubSurface Color", Color) = (1.0,1.0,1.0,1.0)
	_FoamColor ("Foam Color", Color) = (1.0,1.0,1.0,1.0)
	_CausticsColor ("Caustics Color", Color) = (1.0,1.0,1.0,1.0)
	_UnderwaterColor ("Caustics Color", Color) = (1.0,1.0,1.0,1.0)

	_NormalTexS ("Shallow Wave Normal Texture", 2D) = "white" {}
	_NormalTexD ("Deep Wave Normal Texture Large", 2D) = "white" {}
	_NormalTexR ("Rolling Wave Normal Texture Large", 2D) = "white" {}
	_ReflectionTex ("reflections", 2D) = "white" {}
	_CubeTex ("Cubemap reflections", CUBE) = "white" {}
	_FoamTex ("Foam Texture", 2D) = "white" {}
	_MaskTex ("Mask Texture", 2D) = "white" {}

	_NormalStrength ("Normal Strength", Range(0.0,1.0)) = 0.5
	_heightScale ("Wave Height", Float) = 0.12
	_lgWaveHeight ("Large Wave Height", Float) = 0.055
	_CompatWaveAmplitude ("Visible Wave Amplitude", Float) = 0.028
	_AnimSpeed ("Animation Speed", Float) = 1.0
	_turbulenceFactor ("Turbulence", Range(0.0,1.0)) = 0.08
	_specularPower ("Specular Power", Range(0.0,2.0)) = 0.42
	_roughness ("Roughness", Range(0.0,1.0)) = 0.62
	_overallBrightness ("Overall Brightness", Range(0.0,2.0)) = 0.9
	_Level01ColorBlend ("Level01 Environment Blend", Range(0.0,1.0)) = 0.0
	_Level01ReflectionTint ("Level01 Reflection Tint", Color) = (1.0,1.0,1.0,1.0)
	_CinematicOcean ("Cinematic Ocean Rendering", Range(0.0,1.0)) = 0.0
	_CinematicReflection ("Cinematic Reflection", Range(0.0,1.5)) = 0.82
	_CinematicSunGlint ("Cinematic Sun Glint", Range(0.0,1.5)) = 0.38
	_CinematicHorizonBlend ("Cinematic Horizon Blend", Range(0.0,1.0)) = 0.68
	_CinematicHorizonColor ("Cinematic Horizon Color", Color) = (0.78,0.52,0.32,1.0)
	_CinematicMicroRipple ("Cinematic Micro Ripple", Range(0.0,1.0)) = 0.46
	_OpenOceanBlend ("Open Ocean Detail", Range(0.0,1.0)) = 0.0
	_OpenOceanChop ("Open Ocean Choppiness", Range(0.0,1.5)) = 0.0
	_OpenOceanFoam ("Open Ocean Crest Foam", Range(0.0,1.0)) = 0.0
	_suimono_Dir ("Flow Direction", Vector) = (0.42,1,-0.91,0)
	_RefractStrength ("Refraction Strength", Range(0.0,1.0)) = 0.5
	_EdgeFade ("Edge Fade", Range(0.01,500.0)) = 1.0
	_EdgeFoamFade ("Edge Foam Amt", Range(5.0,500.0)) = 1.0
	_foamScale ("Foam Scale", Range(5.0,500.0)) = 1.0
	_ShallowFoamAmt ("Shallow Foam Amt", Range(5.0,500.0)) = 1.0
	_HeightFoamAmt ("Edge Foam Amt", Range(5.0,500.0)) = 1.0
	_HeightFoamHeight ("Height Foam Height", Range(5.0,500.0)) = 1.0
	_HeightFoamSpread ("Height Foam Spread", Range(5.0,500.0)) = 1.0
	_ShorelineLevel ("Island Shoreline Level", Float) = 150.0
	_ShorelineWidth ("Island Shoreline Width", Float) = 14.0
	_ShorelineFoam ("Island Shoreline Foam", Range(0.0,1.0)) = 0.0
	_TideAmount ("Tide Reach", Float) = 0.3
	_TideSpread ("Tide Spread", Float) = 0.5

	_DepthFade ("Depth Fade", Range(0.01,5.0)) = 1.0
	_ShallowFade ("Shallow Fade", Range(0.01,5.0)) = 1.0
	_RefractFade ("Refract Fade", Range(0.01,500.0)) = 1.0
	_CausticsFade ("Caustics Fade", Range(0.01,500.0)) = 1.0

	_WaveTex ("Wave Texture", 2D) = "white" {}
	_shorelineScale ("Shoreline scale", Float) = 1.0
	_shorelineFrequency ("Shoreline Frequency", Float) = 1.0
	_shorelineSpeed  ("Shoreline Speed", Float) = 1.0
	_shorelineHeight  ("Shoreline Height", Float) = 1.0

	_Tess ("Tessellation", Float) = 4.0
    _minDist ("TessMin", Range(-180.0, 0.0)) = 10.0
    _maxDist ("TessMax", Range(20.0, 500.0)) = 25.0

    _suimono_uvx ("uvx1", Float) = 0.0
    _suimono_uvy ("uvy1", Float) = 0.0
}



// Unity 2022 compatibility path. It retains SUIMONO geometry, normal maps
// and runtime parameters without depending on legacy screen-color buffers.
SubShader {
	Tags { "Queue"="Geometry" "IgnoreProjector"="True" "RenderType"="SuimonoWater" }
	Cull Back
	ZWrite On

	Pass {
		Tags { "LightMode"="ForwardBase" }
		CGPROGRAM
		#pragma target 3.0
		#pragma vertex SuimonoCompatVert
		#pragma fragment SuimonoCompatFrag
		#pragma multi_compile_fog
		#include "SuimonoLevel02Compat.cginc"
		ENDCG
	}
}

//SURFACE DX11
SubShader {
	Tags { "Queue"="Geometry" "IgnoreProjector"="True" "RenderType"="SuimonoWater"}
	Cull Back Lighting On ZWrite On

	CGPROGRAM
		#include "UnityCG.cginc"
		#include "SuimonoFunctionLibrary.cginc"

		#pragma target 5.0
		#pragma surface SuimonoSurf SuimonoLight addshadow vertex:SuimonoVert tessellate:SuimonoTess

	ENDCG

}





//SURFACE DX9
SubShader {
	Tags { "Queue"="Geometry" "IgnoreProjector"="True" "RenderType"="SuimonoWater" }
	Cull Back Lighting On ZWrite On

	CGPROGRAM
		#include "UnityCG.cginc"
		#include "SuimonoFunctionLibrary.cginc"

		#pragma target 3.0
		#pragma surface SuimonoSurf SuimonoLight addshadow vertex:SuimonoVert
	ENDCG

}



fallback "Diffuse"

}
