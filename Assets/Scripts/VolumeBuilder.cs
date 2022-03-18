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
    private bool emptySpaceSkip;
    private EmptySpaceSkipMethod emptySpaceSkipMethod;
    [SerializeField] private GameObject render;

    [SerializeField] InteractableToggleCollection volumeList;
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

    private Color32[] GenerateColorTable(List<AlphaTransferFunctionPoint> alphaPoints, List<ColorTransferFunctionPoint> colorPoints)
    {
        CubicSpline[] m_colorTransferFunction = new CubicSpline[3];
        Color32[] colors = new Color32[256];
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

        for (i = 0; i < 256; i++)
        {
            colors[i] = new Color32(InterpolatedDoubleToByte(m_colorTransferFunction[0], i), InterpolatedDoubleToByte(m_colorTransferFunction[1], i), InterpolatedDoubleToByte(m_colorTransferFunction[2], i), InterpolatedDoubleToByte(m_opacityTransferFunction, i));
        }

        return colors;
    }

    private Texture2D GenerateTransferFunction(TransferFunctionType type)
    {
        List<ColorTransferFunctionPoint> colorTransferFunctionPoints;
        List<AlphaTransferFunctionPoint> opacityTransferFunctionPoints;
        Color32[] colors = new Color32[256];
                
        switch (type)
        {
            case TransferFunctionType.LINEAR:
                for (int i = 0; i < 256; i++)
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

                colors = GenerateColorTable(opacityTransferFunctionPoints, colorTransferFunctionPoints);
                
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

                colors = GenerateColorTable(opacityTransferFunctionPoints, colorTransferFunctionPoints);

                break;

        }

        Texture2D transferFunction = new Texture2D(256, 1, TextureFormat.RGBA32, false);
        transferFunction.wrapMode = TextureWrapMode.Clamp;
        transferFunction.filterMode = FilterMode.Bilinear;
        transferFunction.anisoLevel = 0;
        transferFunction.SetPixels32(colors, 0);
        transferFunction.Apply();

        return transferFunction;
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
                            byte alpha = transferFunction[elem + 3];
                            byte r, g, b;
                            r = transferFunction[elem];
                            g = transferFunction[elem + 1];
                            b = transferFunction[elem + 2];
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

    private Texture3D UniformSubdivision(Material material)
    {
        int[] dims = new int[3] { Mathf.CeilToInt((float)m_volume.width / m_blockSize), Mathf.CeilToInt((float)m_volume.height / m_blockSize), Mathf.CeilToInt((float)m_volume.depth / m_blockSize) };
        byte[] textureData = new byte[dims[0] * dims[1] * dims[2]];

        NativeArray<byte> volumeData = new NativeArray<byte>(m_volume.GetPixelData<byte>(0), Allocator.TempJob);
        NativeArray<byte> textureDataNative = new NativeArray<byte>(textureData, Allocator.TempJob);
        NativeArray<int> originalDims = new NativeArray<int>(new int[3] { m_volume.width, m_volume.height, m_volume.depth }, Allocator.TempJob);
        NativeArray<int> dimsNative = new NativeArray<int>(dims, Allocator.TempJob);
        NativeArray<byte> transferFunc = new NativeArray<byte>(((Texture2D)material.GetTexture("_Transfer")).GetRawTextureData(), Allocator.TempJob);

        var job = new UniformJob()
        {
            textureData = textureDataNative,
            volume = volumeData,
            originalDims = originalDims,
            dims = dimsNative,
            blockSize = m_blockSize,
            transferFunction = transferFunc,
        };

        JobHandle dep = new JobHandle();
        JobHandle jobHandle = job.ScheduleParallel(dims[0] * dims[1] * dims[2], 1, dep);

        jobHandle.Complete();

        volumeData.Dispose();
        originalDims.Dispose();
        dimsNative.Dispose();
        transferFunc.Dispose();

        Texture3D subdivision = new Texture3D(dims[0], dims[1], dims[2], TextureFormat.R8, false);
        subdivision.wrapMode = TextureWrapMode.Clamp;
        subdivision.filterMode = FilterMode.Point;
        subdivision.anisoLevel = 0;

        subdivision.SetPixelData(textureDataNative.ToArray(), 0);
        subdivision.Apply();
        textureDataNative.Dispose();

        return subdivision;
    }

    #endregion

    #region Event listeners

    public void Build()
    {
        GameObject gary = Instantiate(render, new Vector3(0, -0.2f, 1f), Quaternion.identity);
        Transform transform = gary.GetComponent<Transform>();
        Bounds localAABB = gary.GetComponent<MeshFilter>().sharedMesh.bounds;

        material.SetTexture("_Transfer", transferFunction);
        material.SetTexture("_Volume", m_volume);
        material.SetVector("_bbMin", localAABB.min/* + new Vector3(0.5f, 0.5f, 0.5f)*/);
        material.SetVector("_bbMax", localAABB.max/* + new Vector3(0.5f, 0.5f, 0.5f)*/);
        material.SetFloat("_ERT", m_ERT);

        if (emptySpaceSkip)
        {
            switch (emptySpaceSkipMethod)
            {
                case EmptySpaceSkipMethod.UNIFORM:
                    material.SetTexture("_EmptySpaceSkipStructure", UniformSubdivision(material));
                    material.SetInt("_BlockSize", m_blockSize);
                    material.SetVector("_VolumeDims", new Vector3(m_volume.width, m_volume.height, m_volume.depth));
                    material.SetVector("_OccupancyDims", new Vector3(Mathf.CeilToInt((float)m_volume.width / m_blockSize), Mathf.CeilToInt((float)m_volume.height / m_blockSize), Mathf.CeilToInt((float)m_volume.depth / m_blockSize)));
                    break;
                case EmptySpaceSkipMethod.OCCUPANCY:
                    material.SetTexture("_OccupancyMap", UniformSubdivision(material));
                    material.SetInt("_BlockSize", m_blockSize);
                    material.SetVector("_VolumeDims", new Vector3(m_volume.width, m_volume.height, m_volume.depth));
                    material.SetVector("_OccupancyDims", new Vector3(Mathf.CeilToInt((float)m_volume.width / m_blockSize), Mathf.CeilToInt((float)m_volume.height / m_blockSize), Mathf.CeilToInt((float)m_volume.depth / m_blockSize)));
                    break;
                case EmptySpaceSkipMethod.CHEBYSHEV:
                    break;
                case EmptySpaceSkipMethod.SPARSELEAP:
                    break;
                default:
                    break;
            }
        }

        gary.GetComponent<MeshRenderer>().material = material;
        transform.localScale = (new Vector3(m_volume.width, m_volume.height, m_volume.depth)) / 1000;
        Destroy(this.transform.parent.gameObject);
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
            emptySpaceSkipMethod = EmptySpaceSkipMethod.UNIFORM;
        }
        else if (shaderIndex == 4)
        {
            emptySpaceSkipMethod = EmptySpaceSkipMethod.OCCUPANCY;
        }
        else if (shaderIndex == 5)
        {
            emptySpaceSkipMethod = EmptySpaceSkipMethod.SPARSELEAP;
        }
        else if (shaderIndex == 6)
        {
            emptySpaceSkipMethod = EmptySpaceSkipMethod.CHEBYSHEV;
        }
    }

    public void UpdateERT()
    {
        m_ERT = ERTSlider.CurrentValue;
    }

    public void UpdateBlockSize()
    {
        m_blockSize = ESSSlider.CurrentValue;
    }

    public void PickTransferFunctionEvent()
    {
        Debug.Log(((TransferFunctionType)transferFunctionList.CurrentIndex).ToString());
        transferFunction = GenerateTransferFunction((TransferFunctionType)transferFunctionList.CurrentIndex);
    }

    public IEnumerator PickVolume()
    {
        ResourceRequest req;
        switch (volumeList.CurrentIndex)
        {
            case 0:
                volumeType = VolumeType.US;
                req = Resources.LoadAsync("VolumeTextures/US/A6/vol01");
                break;
            case 1:
                volumeType = VolumeType.CT;
                req = Resources.LoadAsync("VolumeTextures/CT/small_pig");
                break;
            case 2:
                volumeType = VolumeType.CT;
                req = Resources.LoadAsync("VolumeTextures/CT/medium_pig");
                break;
            case 3:
                volumeType = VolumeType.MRI;
                req = Resources.LoadAsync("VolumeTextures/MRI/knee");
                break;
            case 4:
                volumeType = VolumeType.MRI;
                req = Resources.LoadAsync("VolumeTextures/MRI/abdomen");
                break;
            default:
                volumeType = VolumeType.CT;
                req = Resources.LoadAsync("VolumeTextures/CT/small_pig");
                break;
        }

        if (!req.isDone)
        {
            yield return null;
        }

        m_volume = (Texture3D)req.asset;

        yield return null;
    }

    #endregion
}
