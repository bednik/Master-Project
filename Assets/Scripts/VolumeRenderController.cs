using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MathNet.Numerics.Interpolation;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using VolumeRendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

/// <summary>
/// Attach to volume object to be able to manipulate in real-time
/// </summary>
[ExecuteInEditMode]
public class VolumeRenderController : MonoBehaviour
{
    [SerializeField] private Shader m_shader;
    [SerializeField] private Texture3D m_volume;
    private MeshRenderer m_renderer;

    [SerializeField] AudioSource completeSound;

    [SerializeField] private bool color = false;

    // Transfer functions
    CubicSpline alphaTransferFunction;
    CubicSpline grayTransferFunction;
    CubicSpline[] colorTransferFunction = new CubicSpline[3];

    // Empty-space skipping
    [SerializeField] private bool emptySpaceSkip = false;
    [SerializeField] private EmptySpaceSkipMethod emptySpaceSkipMethod = EmptySpaceSkipMethod.UNIFORM;
    [SerializeField] private int blockSize = 32;
    [SerializeField] private int octreeLevels = 2;

    // Shader properties
    [SerializeField] private float[] thresholds = new float[2]{0.05f, 0.95f};
    [SerializeField] private Vector4 sliceMin = new Vector4(-0.5f, -0.5f, -0.5f, 1f);
    [SerializeField] private Vector4 sliceMax = new Vector4(0.5f, 0.5f, 0.5f, 1f);

    /// Transfer function sample points
    [SerializeField]
    private List<ColorTransferFunctionPoint> colorPoints = new List<ColorTransferFunctionPoint>
    {
        new ColorTransferFunctionPoint(40, 40, 40, 0),
        new ColorTransferFunctionPoint(40, 40, 40, 40),
        new ColorTransferFunctionPoint(40, 40, 40, 80),
        new ColorTransferFunctionPoint(255, 255, 255, 82),
        new ColorTransferFunctionPoint(255, 255, 255, 255)
    };

    [SerializeField]
    private List<AlphaTransferFunctionPoint> grayscalePoints = new List<AlphaTransferFunctionPoint>
    {
        new AlphaTransferFunctionPoint(40, 0),
        new AlphaTransferFunctionPoint(40, 40),
        new AlphaTransferFunctionPoint(40, 80),
        new AlphaTransferFunctionPoint(255, 82),
        new AlphaTransferFunctionPoint(255, 255)
    };

    [SerializeField]
    private List<AlphaTransferFunctionPoint> alphaPoints = new List<AlphaTransferFunctionPoint>
    {
        new AlphaTransferFunctionPoint(0, 0),
        new AlphaTransferFunctionPoint(0, 40),
        new AlphaTransferFunctionPoint(51, 60),
        new AlphaTransferFunctionPoint(13, 63),
        new AlphaTransferFunctionPoint(0, 80),
        new AlphaTransferFunctionPoint(230, 82),
        new AlphaTransferFunctionPoint(255, 255)
    };
    /// Transfer function sample points END

    private Material m_material;

    /// <summary>
    /// Convert interpolated value from double to a byte, clamped between 0 and 255
    /// </summary>
    private byte InterpolatedDoubleToByte(CubicSpline spline, double x)
    {
        return (byte)Mathf.Max(0, Mathf.Min((float)spline.Interpolate(x), 255));
    }

    /// <summary>
    /// Generate a 1D alpha transfer function texture (8-bit, 1 channel)
    /// </summary>
    private Texture2D generateAlphaTable()
    {
        Texture2D alphaTransfer = new Texture2D(256, 1, TextureFormat.R8, false);
        alphaTransfer.wrapMode = TextureWrapMode.Clamp;
        alphaTransfer.filterMode = FilterMode.Bilinear;
        alphaTransfer.anisoLevel = 0;

        byte[] alphas = new byte[256];
        double[] fx = new double[alphaPoints.Count];
        double[] x = new double[alphaPoints.Count];

        int i = 0;
        foreach (AlphaTransferFunctionPoint point in alphaPoints)
        {
            x[i] = point.density;
            fx[i] = point.a;
            i++;
        }

        alphaTransferFunction = CubicSpline.InterpolateAkimaSorted(x, fx);
        
        for (i = 0; i < 256; i++)
        {
            alphas[i] = InterpolatedDoubleToByte(alphaTransferFunction, i);
        }

        alphaTransfer.SetPixelData(alphas, 0);
        alphaTransfer.Apply();
        return alphaTransfer;
    }

    /// <summary>
    /// Generate a 1D grayscale transfer function texture (8-bit, 1 channel)
    /// </summary>
    private Texture2D generateGrayTable()
    {
        Texture2D grayTransfer = new Texture2D(256, 1, TextureFormat.R8, false);
        grayTransfer.wrapMode = TextureWrapMode.Clamp;
        grayTransfer.filterMode = FilterMode.Bilinear;
        grayTransfer.anisoLevel = 0;

        byte[] grays = new byte[256];
        double[] fx = new double[grayscalePoints.Count];
        double[] x = new double[grayscalePoints.Count];

        int i = 0;
        foreach (AlphaTransferFunctionPoint point in grayscalePoints)
        {
            x[i] = point.density;
            fx[i] = point.a;
            i++;
        }

        grayTransferFunction = CubicSpline.InterpolateAkimaSorted(x, fx);

        for (i = 0; i < 256; i++)
        {
            grays[i] = InterpolatedDoubleToByte(grayTransferFunction, i);
        }

        grayTransfer.SetPixelData(grays, 0);
        grayTransfer.Apply();
        return grayTransfer;
    }

    /// <summary>
    /// Generate a 1D color transfer function texture (8-bit, 3 channels)
    /// </summary>
    private Texture2D generateColorTable()
    {
        Texture2D colorTransfer = new Texture2D(256, 1, TextureFormat.RGBA32, false);
        colorTransfer.wrapMode = TextureWrapMode.Clamp;
        colorTransfer.filterMode = FilterMode.Bilinear;
        colorTransfer.anisoLevel = 0;

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
        CubicSpline alphaTransferFunction = CubicSpline.InterpolateAkimaSorted(x, fx);

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

        colorTransferFunction[0] = CubicSpline.InterpolateAkimaSorted(d, r);
        colorTransferFunction[1] = CubicSpline.InterpolateAkimaSorted(d, g);
        colorTransferFunction[2] = CubicSpline.InterpolateAkimaSorted(d, b);

        for (i = 0; i < 256; i++)
        {
            colors[i] = new Color32(InterpolatedDoubleToByte(colorTransferFunction[0], i), InterpolatedDoubleToByte(colorTransferFunction[1], i), InterpolatedDoubleToByte(colorTransferFunction[2], i), InterpolatedDoubleToByte(alphaTransferFunction, i));
        }

        colorTransfer.SetPixels32(colors, 0);
        colorTransfer.Apply();
        return colorTransfer;
    }

    // Returns a completely red 3D texture
    private Texture3D RedTex(int[] dims)
    {
        Texture3D redTex = new Texture3D(dims[0], dims[1], dims[2], TextureFormat.R8, false);
        redTex.wrapMode = TextureWrapMode.Clamp;
        redTex.filterMode = FilterMode.Bilinear;
        redTex.anisoLevel = 0;
        byte[] color = new byte[dims[0] * dims[1] * dims[2]];

        for (int i = 0; i < color.Length; i++)
        {
            color[i] = 255;
        }

        redTex.SetPixelData(color, 0);
        redTex.Apply();
        return redTex;
    }

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
            
            byte minVal = 255;
            byte maxVal = 0;
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

                        minVal = (elem < minVal) ? elem : minVal;
                        maxVal = (elem > maxVal) ? elem : maxVal;

                        if (empty)
                        {
                            byte alpha = transferFunction[elem + 3];
                            byte r, g, b;
                            r = transferFunction[elem];
                            g = transferFunction[elem + 1];
                            b = transferFunction[elem + 2];
                            // The subvolume is not empty if any of the color channels contain a non-zero value AND the alpha is not zero
                            empty = (r == 0 && g == 0 && b == 0) || alpha == 0;
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

            textureData[index] = (empty) ? (byte)0 : (byte)255;
        }
    }

    // Warning: Slow
    private IEnumerator UniformSubdivision(Material material)
    {
        int[] dims = new int[3] { Mathf.CeilToInt((float)m_volume.width / blockSize), Mathf.CeilToInt((float)m_volume.height / blockSize), Mathf.CeilToInt((float)m_volume.depth / blockSize) };
        byte[] textureData = new byte[dims[0] * dims[1] * dims[2]];
        int idx = 0;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        NativeArray<byte> volumeData = new NativeArray<byte>(m_volume.GetPixelData<byte>(0), Allocator.Persistent);
        NativeArray<byte> textureDataNative = new NativeArray<byte>(textureData, Allocator.Persistent);
        NativeArray<int> originalDims = new NativeArray<int>(new int[3] { m_volume.width, m_volume.height, m_volume.depth }, Allocator.Persistent);
        NativeArray<int> dimsNative = new NativeArray<int>(dims, Allocator.Persistent);
        NativeArray<byte> transferFunc = new NativeArray<byte>(((Texture2D)material.GetTexture("_Transfer")).GetRawTextureData(), Allocator.Persistent);

        var job = new UniformJob()
        {
            textureData = textureDataNative,
            volume = volumeData,
            originalDims = originalDims,
            dims = dimsNative,
            blockSize = blockSize,
            transferFunction = transferFunc,
        };

        JobHandle dep = new JobHandle();
        JobHandle jobHandle = job.ScheduleParallel(dims[0]*dims[1]*dims[2], 1, dep);

        jobHandle.Complete();
        stopwatch.Stop();
        UnityEngine.Debug.Log("Elapsed time for parallel: " + stopwatch.ElapsedMilliseconds);

        volumeData.Dispose();
        originalDims.Dispose();
        dimsNative.Dispose();
        transferFunc.Dispose();

        stopwatch.Reset();
        stopwatch.Start();
        
        for (int z = 0; z < m_volume.depth; z += blockSize)
        {
            for (int y = 0; y < m_volume.height; y += blockSize)
            {
                for (int x = 0; x < m_volume.width; x += blockSize)
                {
                    textureData[idx] = new OctreeNode((Texture2D)material.GetTexture("_Transfer"), m_volume, new Vector3(x, y, z), new Vector3(x + blockSize, y + blockSize, z + blockSize), 0).empty ? (byte)0 : (byte)255;
                    idx++;
                }
            }
            //UnityEngine.Debug.Log(string.Format("{0} out of {1} points done", idx, dims[0] * dims[1] * dims[2]));
        }
        stopwatch.Stop();
        UnityEngine.Debug.Log("Elapsed time for serial: " + stopwatch.ElapsedMilliseconds);


        Texture3D subdivision = new Texture3D(dims[0], dims[1], dims[2], TextureFormat.R8, false);
        subdivision.wrapMode = TextureWrapMode.Clamp;
        subdivision.filterMode = FilterMode.Bilinear;
        subdivision.anisoLevel = 0;

        //subdivision.SetPixelData(textureData, 0);
        subdivision.SetPixelData(textureDataNative.ToArray(), 0);
        subdivision.Apply();
        textureDataNative.Dispose();

        m_renderer.sharedMaterial.SetTexture("_EmptySpaceSkipStructure", subdivision);
        completeSound.Play(0);
        UnityEngine.Debug.Log("Job's done!");
        yield return null;
    }

    // Occupancy histogram tree (SparseLeap)
    private OccupancyNode GenerateOHT(bool delayed)
    {
        OccupancyNode root = new OccupancyNode(new Vector3(0, 0, 0), new Vector3(m_volume.width - 1, m_volume.height - 1, m_volume.depth - 1), null);
        if (delayed) // Set all to "unknown"
        {

        } 
        else
        {

        }

        return root;
    }

    // Occupancy geometry generation (SparseLeap)
    private void GenerateOccupancyGeometry()
    {

    }


    private void Awake()
    {
        m_renderer = GetComponent<MeshRenderer>();
        Transform transform = GetComponent<Transform>();
        Bounds localAABB = GetComponent<MeshFilter>().sharedMesh.bounds;
        m_material = new Material(m_shader);
        m_material.SetTexture("_Volume", m_volume);
        m_material.SetVector("_bbMin", localAABB.min);
        m_material.SetVector("_bbMax", localAABB.max);

        if (color)
        {
            m_material.SetTexture("_Transfer", generateColorTable());
        }
        else
        {
            m_material.SetTexture("_GrayTransfer", generateGrayTable());
            m_material.SetTexture("_AlphaTransfer", generateAlphaTable());
        }
        
        m_material.SetFloat("_ThresholdMin", thresholds[0]);
        m_material.SetFloat("_ThresholdMax", thresholds[1]);
        m_material.SetVector("_SliceMin", sliceMin);
        m_material.SetVector("_SliceMax", sliceMax);
        m_renderer.material = m_material;

        transform.localScale = (new Vector3(m_volume.width, m_volume.height, m_volume.depth))/1000;
    }
}
