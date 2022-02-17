using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MathNet.Numerics.Interpolation;
using System.Linq;

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

    class OctreeNode
    {
        /// <summary>
        /// Creates a node for an octree which represents a subvolume (Parallel-friendly)
        /// </summary>
        /// <param name="alphaTransferFunction">The alpha transfer function which will decide the opacity of the final volume</param>
        /// <param name="colorTransferFunction">The color transfer function which will decide the color of the final volume</param>
        /// <param name="vol">The full volume</param>
        /// <param name="_min">The minimum index of the subvolume</param>
        /// <param name="_max">The maximum index of the subvolume</param>
        /// <param name="_level">How deep in the tree this node is</param>
        public OctreeNode(CubicSpline alphaTransferFunction, CubicSpline[] colorTransferFunction, Unity.Collections.NativeArray<byte> vol_arr, Vector3 _min, Vector3 _max, byte _level, int[] dims)
        {
            min = _min;
            max = _max;
            level = _level;

            // Iterate through desired subsampled volume
            minVal = 255;
            maxVal = 0;
            empty = true;

            for (int z = (int)min.z; z < (int)max.z; z++)
            {
                if (z >= dims[2]) break;

                for (int y = (int)min.y; y < (int)max.y; y++)
                {
                    if (y >= dims[1]) break;

                    for (int x = (int)min.x; x < (int)max.x; x++)
                    {
                        if (x >= dims[0]) break;

                        byte elem = vol_arr[x + y * dims[0] + z * dims[0] * dims[1]];

                        minVal = (elem < minVal) ? elem : minVal;
                        maxVal = (elem > maxVal) ? elem : maxVal;

                        if (empty)
                        {
                            byte alpha = (byte)Mathf.Max(0, Mathf.Min((float)alphaTransferFunction.Interpolate(elem), 255));
                            byte[] colors = new byte[colorTransferFunction.Length];
                            for (int i = 0; i < colorTransferFunction.Length; i++)
                            {
                                colors[i] = (byte)Mathf.Max(0, Mathf.Min((float)colorTransferFunction[i].Interpolate(elem), 255));
                            }
                            // The subvolume is not empty if any of the color channels contain a non-zero value AND the alpha is not zero
                            empty = colors.All(color => color == 0) || alpha == 0;
                        }

                        // Break out of the loop if we reach minimum minVal AND maximum maxVal
                        if (minVal <= 0 && maxVal >= 255)
                        {
                            y = (int)max.y;
                            z = (int)max.z;
                            break;
                        }
                    }
                }
            }
        }

        // Sequential version
        public OctreeNode(CubicSpline alphaTransferFunction, CubicSpline[] colorTransferFunction, Texture3D vol, Vector3 _min, Vector3 _max, byte _level)
        {
            min = _min;
            max = _max;
            level = _level;

            // Iterate through desired subsampled volume
            var vol_arr = vol.GetPixelData<byte>(0);
            minVal = 255;
            maxVal = 0;
            empty = true;

            for (int z = (int)min.z; z < (int)max.z; z++)
            {
                if (z >= vol.depth) break;

                for (int y = (int)min.y; y < (int)max.y; y++)
                {
                    if (y >= vol.height) break;

                    for (int x = (int)min.x; x < (int)max.x; x++)
                    {
                        if (x >= vol.width) break;

                        byte elem = vol_arr[x + y * vol.width + z * vol.width * vol.height];

                        minVal = (elem < minVal) ? elem : minVal;
                        maxVal = (elem > maxVal) ? elem : maxVal;

                        if (empty)
                        {
                            byte alpha = (byte)Mathf.Max(0, Mathf.Min((float)alphaTransferFunction.Interpolate(elem), 255));
                            byte[] colors = new byte[colorTransferFunction.Length];
                            for (int i = 0; i < colorTransferFunction.Length; i++)
                            {
                                colors[i] = (byte)Mathf.Max(0, Mathf.Min((float)colorTransferFunction[i].Interpolate(elem), 255));
                            }
                            // The subvolume is not empty if any of the color channels contain a non-zero value AND the alpha is not zero
                            empty = colors.All(color => color == 0) || alpha == 0;
                        }

                        // Break out of the loop if we reach minimum minVal AND maximum maxVal
                        if (minVal <= 0 && maxVal >= 255)
                        {
                            y = (int)max.y;
                            z = (int)max.z;
                            break;
                        }
                    }
                }
            }
        }

        public Vector3 min, max;

        // The min and max density values
        public byte minVal, maxVal;
        public bool empty;
        public byte level;
        List<OctreeNode> children { get; set; }
        OctreeNode parent { get; set; }
    }

    class OccupancyNode
    {
        public OccupancyNode(Vector3 _min, Vector3 _max, OccupancyNode _parent)
        {
            empty = 0;
            nonEmpty = 0;
            unknown = 0;
            occupancyClass = Occupancy.UNKNOWN;
            parent = _parent;
        }

        public int empty, nonEmpty, unknown;
        Occupancy occupancyClass;
        List<OccupancyNode> children { get; set; }
        OccupancyNode parent { get; set; }
    }

    enum EmptySpaceSkipMethod
    {
        UNIFORM,
        CHEBYSHEV,
        SPARSELEAP
    }

    enum Occupancy
    {
        EMPTY,
        NONEMPTY,
        UNKNOWN
    }
}