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
        public ColorTransferFunctionPoint(byte _r, byte _g, byte _b, byte _density)
        {
            r = _r;
            g = _g;
            b = _b;
            density = _density;
        }

        [SerializeField]
        public byte r, g, b, density;
    }

    [System.Serializable]
    class AlphaTransferFunctionPoint
    {
        public AlphaTransferFunctionPoint(byte _a, byte _density)
        {
            a = _a;
            density = _density;
        }

        [SerializeField]
        public byte a, density;
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
        CT_BONES_8
    }

    public enum VolumeType
    {
        US,
        CT,
        MRI
    }
}