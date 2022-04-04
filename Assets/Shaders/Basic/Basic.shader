Shader "VolumeRendering/Basic/Basic"
{
    Properties
    {
        _Volume("Volume", 3D) = "" {}
        _Intensity("Intensity", Range(1.0, 5.0)) = 1.2
        _ThresholdMax("Max accepted texture value", Range(0.0, 1.0)) = 0.95
        _ThresholdMin("Minumum accepted texture value", Range(0.0, 1.0)) = 0.05
        _SliceMin("Slice min", Vector) = (-0.5, -0.5, -0.5, 1.0)
        _SliceMax("Slice max", Vector) = (0.5, 0.5, 0.5, 1.0)
		_bbMin("Volume's minimum coord of bounding box", Vector) = (-0.5, -0.5, -0.5, 1.0)
		_bbMax("Volume's maximum coord of bounding box", Vector) = (0.5, 0.5, 0.5, 1.0)
    }

	SubShader
	{
		Tags { "Queue" = "Transparent"}
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#include "./Basic.cginc"
			#pragma vertex vert
			#pragma fragment frag
			ENDCG
		}
	}
}