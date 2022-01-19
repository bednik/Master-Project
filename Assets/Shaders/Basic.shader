Shader "VolumeRendering/Basic"
{
    Properties
    {
        _Volume("Volume", 3D) = "" {}
        _Intensity("Intensity", Range(1.0, 5.0)) = 1.2
        _ThresholdMax("ThresholdMax", Range(0.0, 1.0)) = 0.95
        _ThresholdMin("ThresholdMin", Range(0.0, 1.0)) = 0.05
		_SamplePoints("Amount of sample points", Range(1, 256))
        //_SliceMin("Slice min", Vector) = (0.0, 0.0, 0.0, -1.0)
        //_SliceMax("Slice max", Vector) = (1.0, 1.0, 1.0, -1.0)
    }

	SubShader
	{
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