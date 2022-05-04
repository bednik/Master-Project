using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MathNet.Numerics.Interpolation;
using System.Linq;
using Unity.Collections;

namespace VolumeRendering
{
    [System.Serializable]
    class ColorTransferFunctionPoint
    {
        public ColorTransferFunctionPoint(double _r, double _g, double _b, double _density)
        {
            r = _r;
            g = _g;
            b = _b;
            density = _density;
        }

        [SerializeField]
        public double r, g, b, density;
    }

    [System.Serializable]
    class AlphaTransferFunctionPoint
    {
        public AlphaTransferFunctionPoint(double _a, double _density)
        {
            a = _a;
            density = _density;
        }

        [SerializeField]
        public double a, density;
    }

    public enum EmptySpaceSkipMethod
    {
        UNIFORM,
        OCCUPANCY,
        CHEBYSHEV,
        SPARSELEAP
    }

    enum Occupancy
    {
        EMPTY,
        NONEMPTY,
        UNKNOWN
    }

    enum TransferFunctionType
    {
        LINEAR,
        RAMP,
        MRDEFAULT,
        CT_BONES_8,
        ULTRASOUND
    }

    public enum VolumeType
    {
        US,
        CT,
        MRI
    }
}