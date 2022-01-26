// Shader made by Anders Tasken, Erlend Barstad, and Sondre Tagestad
Shader "VolumeRendering/Anders"
{
	Properties
	{
		_Color("Color", Color) = (1, 1, 1, 1)
		_Volume("Volume", 3D) = "" {}
		_Intensity("Intensity", Range(1.0, 5.0)) = 1.2
		_ThresholdMax("ThresholdMax", Range(0.0, 1.0)) = 0.95
		_ThresholdMin("ThresholdMin", Range(0.0, 1.0)) = 0.05
		_SliceMin("Slice min", Vector) = (0.0, 0.0, 0.0, -1.0)
		_SliceMax("Slice max", Vector) = (1.0, 1.0, 1.0, -1.0)
	}

		CGINCLUDE

			ENDCG

			SubShader{
				Cull Back
				Blend SrcAlpha OneMinusSrcAlpha
			// ZTest Always

			Pass
			{
				CGPROGRAM

		  #define ITERATIONS 256
				#include "./Anders.cginc"
				#pragma vertex vert
				#pragma fragment frag

				ENDCG
			}
		}
}