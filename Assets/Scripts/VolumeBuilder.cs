using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VolumeRendering;
using VolumeRendering.UI;
using MathNet.Numerics.Interpolation;
using Microsoft.MixedReality.Toolkit.UI;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

public class VolumeBuilder : MonoBehaviour
{
    #region Volume Builder fields

    private Shader m_shader;
    private Texture3D m_volume;
    public Texture2D transferFunction;
    private MeshRenderer m_renderer;
    private float m_ERT;
    private int m_blockSize, shaderIndex;
    private Material material;
    private VolumeType volumeType;
    public bool emptySpaceSkip, highPrecision;
    private EmptySpaceSkipMethod emptySpaceSkipMethod;
    private Vector3 scale;
    [SerializeField] private GameObject render;
    public GameObject speedUI, qualityUI, ambientUI;
    public Reset panic;
    private bool doneOcc = false;
    private bool doneCheb = false;
    private bool doneNormal = false;
    private int highQuality = 0;
    private string USVol = "A6";
    private bool shaded = false;

    [SerializeField] InteractableToggleCollection volumeList;
    [SerializeField] InteractableToggleCollection qualityToggle;
    [SerializeField] InteractableToggleCollection transferFunctionList;
    [SerializeField] InteractableToggleCollection shaderList;
    [SerializeField] SimpleSliderBehaviour ERTSlider;
    [SerializeField] ClampedSlider ESSSlider;
    [SerializeField] Shader[] shaders = new Shader[3];

    #endregion

    #region Transfer functions

    /// <summary>
    /// Convert interpolated value from double to a byte, clamped between 0 and 255
    /// </summary>
    private byte InterpolatedDoubleToByte(CubicSpline spline, double x)
    {
        return (byte)Mathf.Max(0, Mathf.Min((float)spline.Interpolate(x), 255));
    }

    private byte InterpolatedDoubleToByte(LinearSpline spline, double x)
    {
        return (byte)Mathf.Max(0, Mathf.Min((float)spline.Interpolate(x), 255));
    }

    private Color32[] GenerateColorTable(List<AlphaTransferFunctionPoint> alphaPoints, List<ColorTransferFunctionPoint> colorPoints, bool highPrecision)
    {
        CubicSpline[] m_colorTransferFunction = new CubicSpline[3];
        Color32[] colors = (highPrecision) ? new Color32[4096] : new Color32[256];
        double[] fx = new double[alphaPoints.Count];
        double[] x = new double[alphaPoints.Count];

        int i = 0;
        foreach (AlphaTransferFunctionPoint point in alphaPoints)
        {
            x[i] = point.density;
            fx[i] = point.a;
            i++;
        }
        CubicSpline m_opacityTransferFunction = CubicSpline.InterpolateAkimaSorted(x, fx);

        double[] d = new double[colorPoints.Count];
        double[] r = new double[colorPoints.Count];
        double[] g = new double[colorPoints.Count];
        double[] b = new double[colorPoints.Count];

        i = 0;
        foreach (ColorTransferFunctionPoint point in colorPoints)
        {
            d[i] = point.density;
            r[i] = point.r;
            g[i] = point.g;
            b[i] = point.b;
            i++;
        }

        m_colorTransferFunction[0] = CubicSpline.InterpolateAkimaSorted(d, r);
        m_colorTransferFunction[1] = CubicSpline.InterpolateAkimaSorted(d, g);
        m_colorTransferFunction[2] = CubicSpline.InterpolateAkimaSorted(d, b);

        int n = (highPrecision) ? 4096 : 256;
        for (i = 0; i < n; i++)
        {
            colors[i] = new Color32(InterpolatedDoubleToByte(m_colorTransferFunction[0], i), InterpolatedDoubleToByte(m_colorTransferFunction[1], i), InterpolatedDoubleToByte(m_colorTransferFunction[2], i), InterpolatedDoubleToByte(m_opacityTransferFunction, i));
        }

        return colors;
    }

    private Color32[] GenerateColorTableLinear(List<AlphaTransferFunctionPoint> alphaPoints, List<ColorTransferFunctionPoint> colorPoints, bool highPrecision)
    {
        LinearSpline[] m_colorTransferFunction = new LinearSpline[3];
        Color32[] colors = (highPrecision) ? new Color32[4096] : new Color32[256];
        double[] fx = new double[alphaPoints.Count];
        double[] x = new double[alphaPoints.Count];

        int i = 0;
        foreach (AlphaTransferFunctionPoint point in alphaPoints)
        {
            x[i] = point.density;
            fx[i] = point.a;
            i++;
        }
        LinearSpline m_opacityTransferFunction = LinearSpline.InterpolateSorted(x, fx);

        double[] d = new double[colorPoints.Count];
        double[] r = new double[colorPoints.Count];
        double[] g = new double[colorPoints.Count];
        double[] b = new double[colorPoints.Count];

        i = 0;
        foreach (ColorTransferFunctionPoint point in colorPoints)
        {
            d[i] = point.density;
            r[i] = point.r;
            g[i] = point.g;
            b[i] = point.b;
            i++;
        }

        m_colorTransferFunction[0] = LinearSpline.InterpolateSorted(d, r);
        m_colorTransferFunction[1] = LinearSpline.InterpolateSorted(d, g);
        m_colorTransferFunction[2] = LinearSpline.InterpolateSorted(d, b);

        int n = (highPrecision) ? 4096 : 256;
        for (i = 0; i < n; i++)
        {
            colors[i] = new Color32(InterpolatedDoubleToByte(m_colorTransferFunction[0], i), InterpolatedDoubleToByte(m_colorTransferFunction[1], i), InterpolatedDoubleToByte(m_colorTransferFunction[2], i), InterpolatedDoubleToByte(m_opacityTransferFunction, i));
        }

        return colors;
    }

    private Texture2D GenerateTransferFunction(TransferFunctionType type)
    {
        List<ColorTransferFunctionPoint> colorTransferFunctionPoints;
        List<AlphaTransferFunctionPoint> opacityTransferFunctionPoints;
        int size = (highPrecision) ? 4096 : 256;
        Color32[] colors = new Color32[size];
                
        switch (type)
        {
            case TransferFunctionType.LINEAR:
                for (int i = 0; i < size; i++)
                {
                    colors[i] = new Color32((byte)i, (byte)i, (byte)i, (byte)i);
                }
                break;

            case TransferFunctionType.RAMP:
                colorTransferFunctionPoints = new List<ColorTransferFunctionPoint>
                {
                    new ColorTransferFunctionPoint(40, 40, 40, 0),
                    new ColorTransferFunctionPoint(40, 40, 40, 40),
                    new ColorTransferFunctionPoint(40, 40, 40, 80),
                    new ColorTransferFunctionPoint(255, 255, 255, 82),
                    new ColorTransferFunctionPoint(255, 255, 255, 255)
                };

                opacityTransferFunctionPoints = new List<AlphaTransferFunctionPoint>
                {
                    new AlphaTransferFunctionPoint(0, 0),
                    new AlphaTransferFunctionPoint(0, 40),
                    new AlphaTransferFunctionPoint(51, 60),
                    new AlphaTransferFunctionPoint(13, 63),
                    new AlphaTransferFunctionPoint(0, 80),
                    new AlphaTransferFunctionPoint(230, 82),
                    new AlphaTransferFunctionPoint(255, 255)
                };

                colors = GenerateColorTable(opacityTransferFunctionPoints, colorTransferFunctionPoints, highPrecision);
                
                break;

            case TransferFunctionType.MRDEFAULT:
                colorTransferFunctionPoints = new List<ColorTransferFunctionPoint>
                {
                    new ColorTransferFunctionPoint(0, 0, 0, 0),
                    new ColorTransferFunctionPoint(43, 0, 0, 20),
                    new ColorTransferFunctionPoint(103, 37, 20, 40),
                    new ColorTransferFunctionPoint(199, 155, 97, 120),
                    new ColorTransferFunctionPoint(216, 213, 201, 220),
                    new ColorTransferFunctionPoint(255, 255, 255, 255),
                };

                opacityTransferFunctionPoints = new List<AlphaTransferFunctionPoint>
                {
                    new AlphaTransferFunctionPoint(0, 0),
                    new AlphaTransferFunctionPoint(0, 20),
                    new AlphaTransferFunctionPoint(38, 40),
                    new AlphaTransferFunctionPoint(77, 120),
                    new AlphaTransferFunctionPoint(97, 220),
                    new AlphaTransferFunctionPoint(128, 255)
                };

                colors = GenerateColorTableLinear(opacityTransferFunctionPoints, colorTransferFunctionPoints, highPrecision);

                break;

            case TransferFunctionType.CT_BONES_8:
                if (highPrecision)
                {
                    colorTransferFunctionPoints = new List<ColorTransferFunctionPoint>
                    {
                        new ColorTransferFunctionPoint(77, 77, 255, 0),
                        new ColorTransferFunctionPoint(77, 255, 77, 512),
                        new ColorTransferFunctionPoint(255, 0, 0, 1463.28),
                        new ColorTransferFunctionPoint(255, 233, 10, 1659.15),
                        new ColorTransferFunctionPoint(255, 77, 77, 1953),
                        new ColorTransferFunctionPoint(255, 77, 77, 4095),
                    };

                    opacityTransferFunctionPoints = new List<AlphaTransferFunctionPoint>
                    {
                        new AlphaTransferFunctionPoint(0, 0),
                        new AlphaTransferFunctionPoint(0, 1152.19),
                        new AlphaTransferFunctionPoint(48, 1278.93),
                        new AlphaTransferFunctionPoint(51, 1952),
                        new AlphaTransferFunctionPoint(51, 4096)
                    };
                }
                else
                {
                    colorTransferFunctionPoints = new List<ColorTransferFunctionPoint>
                    {
                        new ColorTransferFunctionPoint(77, 77, 255, 0),
                        new ColorTransferFunctionPoint(77, 255, 77, 32),
                        new ColorTransferFunctionPoint(255, 0, 0, 91),
                        new ColorTransferFunctionPoint(255, 233, 10, 103),
                        new ColorTransferFunctionPoint(255, 77, 77, 122),
                        new ColorTransferFunctionPoint(255, 77, 77, 255),
                    };

                    opacityTransferFunctionPoints = new List<AlphaTransferFunctionPoint>
                    {
                        new AlphaTransferFunctionPoint(0, 0),
                        new AlphaTransferFunctionPoint(0, 72),
                        new AlphaTransferFunctionPoint(48, 79),
                        new AlphaTransferFunctionPoint(51, 122),
                        new AlphaTransferFunctionPoint(51, 255)
                    };
                }
                

                colors = GenerateColorTableLinear(opacityTransferFunctionPoints, colorTransferFunctionPoints, highPrecision);

                break;

            case TransferFunctionType.ULTRASOUND:

                colorTransferFunctionPoints = new List<ColorTransferFunctionPoint>
                {
                    new ColorTransferFunctionPoint(0, 0, 0, -3024),
                    new ColorTransferFunctionPoint(140.0001, 63.9999, 38.0001, 12.8256530761719),
                    new ColorTransferFunctionPoint(225.00001500000002, 154.00010999999998, 73.99998000000001, 251.105),
                    new ColorTransferFunctionPoint(255, 238.943415, 243.405405, 439.291),
                    new ColorTransferFunctionPoint(211.00000500000002, 168.00011999999998, 255, 3071)
                };

                opacityTransferFunctionPoints = new List<AlphaTransferFunctionPoint>
                {
                    new AlphaTransferFunctionPoint(0, 0),
                    new AlphaTransferFunctionPoint(0, 67),
                    new AlphaTransferFunctionPoint(258.333526611328, 92.39130511879917),
                    new AlphaTransferFunctionPoint(162.60870248079294, 535.864135742188),
                    new AlphaTransferFunctionPoint(160.68696320056911, 2745.43505859375),
                    new AlphaTransferFunctionPoint(157.098101377487085, 3129.70825195312)
                };

                colors = GenerateColorTableLinear(opacityTransferFunctionPoints, colorTransferFunctionPoints, highPrecision);

                break;
        }

        Texture2D transferFunction = new Texture2D(size, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 0
        };
        transferFunction.SetPixels32(colors, 0);
        transferFunction.Apply();

        return transferFunction;
    }

    #endregion

    #region Preprocessing

    private IEnumerator CalculateNormals()
    {
        while (!doneOcc || !doneCheb)
        {
            yield return null;
        }

        ComputeShader cs = (ComputeShader)Resources.Load("ComputeShaders/GetNormals");
        RenderTexture res = new RenderTexture(m_volume.width, m_volume.height, 0, RenderTextureFormat.ARGB32)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = m_volume.depth,
            enableRandomWrite = true,
            filterMode = FilterMode.Bilinear
        };
        res.Create();

        int kernelHandle = cs.FindKernel("CSMain");
        cs.SetTexture(kernelHandle, "Result", res, 0);
        cs.SetTexture(kernelHandle, "Volume", m_volume, 0);
        cs.Dispatch(kernelHandle, Mathf.CeilToInt((float)m_volume.width / 8), Mathf.CeilToInt((float)m_volume.height / 8), m_volume.depth);
        yield return null;

        Texture3D newMainTexture = new Texture3D(m_volume.width, m_volume.height, m_volume.depth, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 0
        };
        Graphics.CopyTexture(res, newMainTexture);
        yield return null;

        material.SetTexture("_Volume", newMainTexture);
        m_volume = newMainTexture;
        res.Release();
        doneNormal = true;
    }

    #endregion

    #region Empty space skipping

    [BurstCompile(CompileSynchronously = true)]
    struct UniformJob : IJobFor
    {
        [WriteOnly] public NativeArray<byte> textureData;

        [ReadOnly] public NativeArray<byte> volume;
        [ReadOnly] public NativeArray<int> originalDims;
        [ReadOnly] public NativeArray<int> dims;
        [ReadOnly] public int blockSize;
        [ReadOnly] public NativeArray<byte> transferFunction;

        public void Execute(int index)
        {
            int temp = index;

            int minZ = temp / (dims[0] * dims[1]);
            temp -= minZ * dims[0] * dims[1];

            int minY = temp / dims[0];
            temp -= minY * dims[0];

            int minX = temp;

            Vector3 min = new Vector3(minX * blockSize, minY * blockSize, minZ * blockSize);
            Vector3 max = min + new Vector3(blockSize, blockSize, blockSize);

            //byte minVal = 255;
            //byte maxVal = 0;
            bool empty = true;

            for (int z = (int)min.z; z < (int)max.z; z++)
            {
                if (z >= originalDims[2]) break;

                for (int y = (int)min.y; y < (int)max.y; y++)
                {
                    if (y >= originalDims[1]) break;

                    for (int x = (int)min.x; x < (int)max.x; x++)
                    {
                        if (x >= originalDims[0]) break;

                        byte elem = volume[x + y * originalDims[0] + z * originalDims[0] * originalDims[1]];

                        //minVal = (elem < minVal) ? elem : minVal;
                        //maxVal = (elem > maxVal) ? elem : maxVal;

                        if (empty)
                        {
                            byte alpha = transferFunction[elem*4 + 3];
                            // The subvolume is empty the alpha is zero
                            empty = alpha == 0;
                        }

                        // Break out of the loop if we reach minimum minVal AND maximum maxVal
                        if (!empty)
                        {
                            y = (int)max.y;
                            z = (int)max.z;
                            break;
                        }
                    }
                }
            }

            textureData[index] = (empty) ? (byte)0 : (byte)255;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct OccupancyHigh : IJobFor
    {
        [WriteOnly] public NativeArray<byte> textureData;

        [ReadOnly] public NativeArray<ushort> volume;
        [ReadOnly] public NativeArray<int> originalDims;
        [ReadOnly] public NativeArray<int> dims;
        [ReadOnly] public int blockSize;
        [ReadOnly] public NativeArray<byte> transferFunction;

        public void Execute(int index)
        {
            int temp = index;

            int minZ = temp / (dims[0] * dims[1]);
            temp -= minZ * dims[0] * dims[1];

            int minY = temp / dims[0];
            temp -= minY * dims[0];

            int minX = temp;

            Vector3 min = new Vector3(minX * blockSize, minY * blockSize, minZ * blockSize);
            Vector3 max = min + new Vector3(blockSize, blockSize, blockSize);

            //byte minVal = 255;
            //byte maxVal = 0;
            bool empty = true;

            for (int z = (int)min.z; z < (int)max.z; z++)
            {
                if (z >= originalDims[2]) break;

                for (int y = (int)min.y; y < (int)max.y; y++)
                {
                    if (y >= originalDims[1]) break;

                    for (int x = (int)min.x; x < (int)max.x; x++)
                    {
                        if (x >= originalDims[0]) break;

                        ushort elem = volume[x + y * originalDims[0] + z * originalDims[0] * originalDims[1]];

                        //minVal = (elem < minVal) ? elem : minVal;
                        //maxVal = (elem > maxVal) ? elem : maxVal;

                        if (empty)
                        {
                            byte alpha = transferFunction[(elem+1023) * 4 + 3];
                            // The subvolume is empty the alpha is zero
                            empty = alpha == 0;
                        }

                        // Break out of the loop if we reach minimum minVal AND maximum maxVal
                        if (!empty)
                        {
                            y = (int)max.y;
                            z = (int)max.z;
                            break;
                        }
                    }
                }
            }

            textureData[index] = (empty) ? (byte)0 : (byte)255;
        }
    }

    IEnumerator UniformSubdivision(Material material)
    {
        ComputeShader cs = (ComputeShader)Resources.Load("ComputeShaders/UniformSubdivision");
        int[] dims = new int[3] { Mathf.CeilToInt((float)m_volume.width / m_blockSize), Mathf.CeilToInt((float)m_volume.height / m_blockSize), Mathf.CeilToInt((float)m_volume.depth / m_blockSize) };
        RenderTexture res = new RenderTexture(dims[0], dims[1], 0, RenderTextureFormat.R8)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = dims[2],
            enableRandomWrite = true,
            filterMode = FilterMode.Point
        };
        res.Create();

        Texture3D subdivision = new Texture3D(dims[0], dims[1], dims[2], TextureFormat.R8, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point,
            anisoLevel = 0
        };
        subdivision.Apply();

        int kernelHandle = cs.FindKernel("CSMain");
        cs.SetTexture(kernelHandle, "transferFunction", transferFunction, 0);
        cs.SetTexture(kernelHandle, "volume", m_volume, 0);
        cs.SetTexture(kernelHandle, "Result", res, 0);
        cs.SetInt("blockSize", m_blockSize);

        cs.Dispatch(kernelHandle, Mathf.CeilToInt((float)dims[0] / 8), Mathf.CeilToInt((float)dims[1] / 8), dims[2]);
        yield return null;
        
        GL.Flush();
        
        // Copy to normal texture, release rendertexture
        Graphics.CopyTexture(res, subdivision);
        yield return null;
        res.Release();

        if (emptySpaceSkipMethod != EmptySpaceSkipMethod.CHEBYSHEV)
        {
            material.SetTexture("_OccupancyMap", subdivision);
        }
        else
        {
            material.SetTexture("_DistanceMap", subdivision);
        }
        
        doneOcc = true;

        yield return null;
    }

    IEnumerator Chebyshev()
    {
        while (!doneOcc)
        {
            yield return null;
        }

        Texture2D byteToFloat = new Texture2D(256, 1, TextureFormat.R8, false) 
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point,
            anisoLevel = 0
        };

        byte[] bytes = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            bytes[i] = (byte)i;
        }
        byteToFloat.SetPixelData<byte>(bytes, 0);
        byteToFloat.Apply();

        ComputeShader cs = (ComputeShader)Resources.Load("ComputeShaders/Chebyshev");
        int[] dims = new int[3] { Mathf.CeilToInt((float)m_volume.width / m_blockSize), Mathf.CeilToInt((float)m_volume.height / m_blockSize), Mathf.CeilToInt((float)m_volume.depth / m_blockSize) };
        RenderTexture outMap = new RenderTexture(dims[0], dims[1], 0, RenderTextureFormat.R8)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = dims[2],
            enableRandomWrite = true,
            filterMode = FilterMode.Point
        };
        outMap.Create();

        Texture3D storeTex = new Texture3D(dims[0], dims[1], dims[2], TextureFormat.R8, false) 
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point,
            anisoLevel = 0
        };
        storeTex.Apply();

        //////// TRANSFORM 1 ////////

        int kernelHandle = cs.FindKernel("Trans1");
        cs.SetTexture(kernelHandle, "InMap", (Texture3D)material.GetTexture("_DistanceMap"), 0);
        cs.SetTexture(kernelHandle, "OutMap", outMap, 0);
        cs.SetTexture(kernelHandle, "ByteToFloat", byteToFloat, 0);
        cs.Dispatch(kernelHandle, 1, Mathf.CeilToInt((float)dims[1] / 8), Mathf.CeilToInt((float)dims[2] / 8));
        yield return null;

        GL.Flush();

        // Copy to normal texture, release rendertexture
        Graphics.CopyTexture(outMap, storeTex);
        yield return null;
        material.SetTexture("_DistanceMap", storeTex);

        //////// TRANSFORM 2 ////////
        kernelHandle = cs.FindKernel("Trans2");
        cs.SetTexture(kernelHandle, "InMap", (Texture3D)material.GetTexture("_DistanceMap"));
        cs.SetTexture(kernelHandle, "OutMap", outMap, 0);
        cs.SetTexture(kernelHandle, "ByteToFloat", byteToFloat, 0);
        cs.Dispatch(kernelHandle, Mathf.CeilToInt((float)dims[0] / 8), 1, Mathf.CeilToInt((float)dims[2] / 8));
        yield return null;

        GL.Flush();

        // Copy to normal texture, release rendertexture
        Graphics.CopyTexture(outMap, storeTex);
        yield return null;

        material.SetTexture("_DistanceMap", storeTex);

        //////// TRANSFORM 3 ////////

        kernelHandle = cs.FindKernel("Trans3");
        cs.SetTexture(kernelHandle, "InMap", (Texture3D)material.GetTexture("_DistanceMap"));
        cs.SetTexture(kernelHandle, "OutMap", outMap, 0);
        cs.SetTexture(kernelHandle, "ByteToFloat", byteToFloat, 0);
        cs.Dispatch(kernelHandle, Mathf.CeilToInt((float)dims[0] / 8), Mathf.CeilToInt((float)dims[1] / 8), 1);
        yield return null;

        GL.Flush();

        // Copy to normal texture, release rendertexture
        Graphics.CopyTexture(outMap, storeTex);
        yield return null;
        outMap.Release();


        material.SetTexture("_DistanceMap", storeTex);
        doneCheb = true;

        yield return null;
    }

    #endregion

    #region Event listeners

    private IEnumerator TimedDestruction()
    {
        if (shaded)
        {
            while (!doneNormal)
            {
                yield return null;
            }
        }
        else
        {
            while (!doneCheb || !doneOcc)
            {
                yield return null;
            }
        }
        
        Reset btn = Instantiate(panic, new Vector3(0, -0.62f, 2f), Quaternion.identity);
        GameObject gary = Instantiate(render, new Vector3(0, -0.2f, 2.5f), Quaternion.identity);
        if (volumeType == VolumeType.US)
        {
            GameObject speed = Instantiate(speedUI, new Vector3(0, 0f, 3f), Quaternion.identity);
            btn.speedUI = speed;
        }

        if (emptySpaceSkip)
        {
            GameObject quality = Instantiate(qualityUI, new Vector3(0.529f, 0f, 3f), Quaternion.Euler(new Vector3(0, 32.89f, 0)));
            btn.qualityUI = quality;
        }

        if (shaded)
        {
            GameObject ambient = Instantiate(ambientUI, new Vector3(-0.529f, 0f, 3f), Quaternion.Euler(new Vector3(0, -32.89f, 0)));
            btn.ambientUI = ambient;
        }
        
        btn.render = gary;
        Transform transform_g = gary.GetComponent<Transform>();
        gary.GetComponent<MeshRenderer>().sharedMaterial = material;

        VolumeRenderController controller = gary.GetComponent<VolumeRenderController>();
        controller.USVol = USVol;
        controller.volumeType = volumeType;
        controller.emptySpaceSkipMethod = emptySpaceSkipMethod;
        controller.m_volume = m_volume;
        controller.emptySpaceSkip = emptySpaceSkip;
        controller.transferFunction = transferFunction;
        controller.m_blockSize = m_blockSize;
        controller.shaded = shaded;

        transform_g.localScale = (volumeType == VolumeType.CT) ? scale : new Vector3(m_volume.width, m_volume.height, m_volume.depth) / 1000;
        Destroy(transform.parent.gameObject);
    }

    public void Build()
    {
        material.SetTexture("_Transfer", transferFunction);
        material.SetTexture("_Volume", m_volume);
        material.SetFloat("_ERT", m_ERT);
        material.SetInt("_HighQuality", highQuality);
        material.SetInt("_BlockSize", m_blockSize);

        if (emptySpaceSkip)
        {
            switch (emptySpaceSkipMethod)
            {
                case EmptySpaceSkipMethod.UNIFORM:
                    StartCoroutine(UniformSubdivision(material));
                    doneCheb = true;
                    material.SetVector("_OccupancyDims", new Vector3(Mathf.CeilToInt((float)m_volume.width / m_blockSize), Mathf.CeilToInt((float)m_volume.height / m_blockSize), Mathf.CeilToInt((float)m_volume.depth / m_blockSize)));
                    break;
                case EmptySpaceSkipMethod.OCCUPANCY:
                    StartCoroutine(UniformSubdivision(material));
                    doneCheb = true;
                    material.SetVector("_OccupancyDims", new Vector3(Mathf.CeilToInt((float)m_volume.width / m_blockSize), Mathf.CeilToInt((float)m_volume.height / m_blockSize), Mathf.CeilToInt((float)m_volume.depth / m_blockSize)));
                    break;
                case EmptySpaceSkipMethod.CHEBYSHEV:
                    StartCoroutine(UniformSubdivision(material));
                    StartCoroutine(Chebyshev());
                    break;
                case EmptySpaceSkipMethod.SPARSELEAP:
                    break;
                default:
                    break;
            }
        }
        else
        {
            doneOcc = true;
            doneCheb = true;
        }

        material.SetVector("_VolumeDims", new Vector3(m_volume.width, m_volume.height, m_volume.depth));

        if (shaded)
        {
            StartCoroutine(CalculateNormals());
        }

        StartCoroutine(TimedDestruction());
    }

    public void PickVolumeEvent()
    {
        StartCoroutine(PickVolume());
    }

    public void PickShaderEvent()
    {
        shaderIndex = shaderList.CurrentIndex;
        m_shader = shaders[shaderIndex];
        material = new Material(m_shader);

        emptySpaceSkip = shaderIndex > 2;
        
        if (shaderIndex == 3)
        {
            emptySpaceSkipMethod = EmptySpaceSkipMethod.OCCUPANCY;
        }
        else if (shaderIndex >= 4)
        {
            emptySpaceSkipMethod = EmptySpaceSkipMethod.CHEBYSHEV;
        }

        shaded = shaderIndex >= 10;
        material.enableInstancing = true;
    }

    public void UpdateERT()
    {
        m_ERT = ERTSlider.CurrentValue;
    }

    public void UpdateQuality()
    {
        highQuality = qualityToggle.CurrentIndex;
    }

    public void UpdateBlockSize()
    {
        m_blockSize = ESSSlider.CurrentValue;
    }

    public void PickTransferFunctionEvent()
    {
        transferFunction = GenerateTransferFunction((TransferFunctionType)transferFunctionList.CurrentIndex);
    }

    public IEnumerator PickVolume()
    {
        ResourceRequest req;
        bool highPrecision_prev = highPrecision;
        switch (volumeList.CurrentIndex)
        {
            case 0:
                volumeType = VolumeType.US;
                USVol = "A6";
                highPrecision = false;
                req = Resources.LoadAsync("VolumeTextures/US/A6/vol01");
                break;
            case 1:
                volumeType = VolumeType.US;
                USVol = "A8";
                highPrecision = false;
                req = Resources.LoadAsync("VolumeTextures/US/A8/vol01");
                break;
            case 2:
                volumeType = VolumeType.US;
                USVol = "1I";
                highPrecision = false;
                req = Resources.LoadAsync("VolumeTextures/US/1I/vol01");
                break;
            case 3:
                volumeType = VolumeType.CT;
                scale = new Vector3(512, 512, 1469) / 1000;
                highPrecision = false;
                req = Resources.LoadAsync("VolumeTextures/CT/small_pig");
                break;
            case 4:
                volumeType = VolumeType.CT;
                scale = new Vector3(512, 512, 1469) / 1000;
                highPrecision = false;
                req = Resources.LoadAsync("VolumeTextures/CT/medium_pig");
                break;
            case 5:
                volumeType = VolumeType.CT;
                scale = new Vector3(0.286f, 0.286f, 0.620f);
                highPrecision = false;
                req = Resources.LoadAsync("VolumeTextures/CT/thorax");
                break;
            case 6:
                volumeType = VolumeType.MRI;
                highPrecision = false;
                req = Resources.LoadAsync("VolumeTextures/MRI/knee");
                break;
            case 7:
                volumeType = VolumeType.MRI;
                highPrecision = false;
                req = Resources.LoadAsync("VolumeTextures/MRI/abdomen");
                break;
            case 8:
                volumeType = VolumeType.CT;
                scale = new Vector3(0.286f, 0.286f, 0.620f);
                highPrecision = false;
                req = Resources.LoadAsync("VolumeTextures/CT/thoraxtest");
                break;
            default:
                volumeType = VolumeType.CT;
                scale = new Vector3(512, 512, 1469) / 1000;
                highPrecision = false;
                req = Resources.LoadAsync("VolumeTextures/CT/small_pig");
                break;
        }

        if (!req.isDone)
        {
            yield return null;
        }

        m_volume = (Texture3D)req.asset;

        // Update transfer function if the precision changes
        if (highPrecision_prev != highPrecision) PickTransferFunctionEvent();

        yield return null;
    }

    #endregion
}
