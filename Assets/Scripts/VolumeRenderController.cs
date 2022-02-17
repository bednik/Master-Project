using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MathNet.Numerics.Interpolation;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using VolumeRendering;

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
    CubicSpline[] colorTransferFunction;

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
        new ColorTransferFunctionPoint(232, 179, 156, 0),
        new ColorTransferFunctionPoint(232, 179, 156, 40),
        new ColorTransferFunctionPoint(232, 179, 156, 80),
        new ColorTransferFunctionPoint(255, 255, 217, 82),
        new ColorTransferFunctionPoint(255, 255, 217, 255)
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

    // Warning: Slow
    // Fix: Parallelism?
        // Tried it, didn't work. I probably did it wrong, though...
    private IEnumerator UniformSubdivision()
    {
        UnityEngine.Debug.Log("STARTED COROUTINE!");
        int[] dims = new int[3] { Mathf.CeilToInt((float)m_volume.width / blockSize), Mathf.CeilToInt((float)m_volume.height / blockSize), Mathf.CeilToInt((float)m_volume.depth / blockSize) };
        byte[] textureData = new byte[dims[0] * dims[1] * dims[2]];
        int idx = 0;

        // Parallel
        /*int[] originalDims = new int[3] { m_volume.width, m_volume.height, m_volume.depth };
        var z_indices = new List<int>();
        for (int i = 0; i < originalDims[2]; i += blockSize)
        {
            z_indices.Add(i);
        }
        var vol_arr = m_volume.GetPixelData<byte>(0);

        Parallel.ForEach(z_indices, z =>
        {
            for (int y = 0; y < originalDims[1]; y += blockSize)
            {
                for (int x = 0; x < originalDims[0]; x += blockSize)
                {
                    textureData[x/blockSize + (y/blockSize)*dims[0] + (z/blockSize)*dims[0]*dims[1]] = (new OctreeNode(alphaTransferFunction, new CubicSpline[] { grayTransferFunction }, vol_arr, new Vector3(x, y, z), new Vector3(x + blockSize, y + blockSize, z + blockSize), 0, originalDims)).empty ? (byte)0 : (byte)255;
                }
            }
        });*/
        // Parallel end
        
        for (int z = 0; z < m_volume.depth; z += blockSize)
        {
            for (int y = 0; y < m_volume.height; y += blockSize)
            {
                for (int x = 0; x < m_volume.width; x += blockSize)
                {
                    textureData[idx] = (new OctreeNode(alphaTransferFunction, new CubicSpline[] { grayTransferFunction }, m_volume, new Vector3(x, y, z), new Vector3(x + blockSize, y + blockSize, z + blockSize), 0)).empty ? (byte)0 : (byte)255;
                    idx++;
                    yield return new WaitForSecondsRealtime(0.001f);
                }
                
            }
            UnityEngine.Debug.Log(string.Format("{0} out of {1} points done", idx, dims[0] * dims[1] * dims[2]));
        }

        Texture3D subdivision = new Texture3D(dims[0], dims[1], dims[2], TextureFormat.R8, false);
        subdivision.wrapMode = TextureWrapMode.Clamp;
        subdivision.filterMode = FilterMode.Bilinear;
        subdivision.anisoLevel = 0;

        subdivision.SetPixelData(textureData, 0);
        subdivision.Apply();

        m_renderer.sharedMaterial.SetTexture("_EmptySpaceSkipStructure", subdivision);
        completeSound.Play(0);
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

        if (emptySpaceSkip)
        {
            switch (emptySpaceSkipMethod)
            {
                case EmptySpaceSkipMethod.UNIFORM: // Very slow!
                    int[] dims = new int[3] { Mathf.CeilToInt((float)m_volume.width / blockSize), Mathf.CeilToInt((float)m_volume.height / blockSize), Mathf.CeilToInt((float)m_volume.depth / blockSize) };
                    m_material.SetTexture("_EmptySpaceSkipStructure", RedTex(dims));
                    m_material.SetInt("_BlockSize", blockSize);
                    break;
                case EmptySpaceSkipMethod.CHEBYSHEV:
                    break;
                case EmptySpaceSkipMethod.SPARSELEAP:
                    break;
                default:
                    break;
            }
        }
        
        m_material.SetFloat("_ThresholdMin", thresholds[0]);
        m_material.SetFloat("_ThresholdMax", thresholds[1]);
        m_material.SetVector("_SliceMin", sliceMin);
        m_material.SetVector("_SliceMax", sliceMax);
        m_renderer.material = m_material;

        transform.localScale = (new Vector3(m_volume.width, m_volume.height, m_volume.depth))/1000;
    }

    private void Start()
    {
        if (emptySpaceSkip)
        {
            switch (emptySpaceSkipMethod)
            {
                case EmptySpaceSkipMethod.UNIFORM:
                    StartCoroutine(UniformSubdivision());
                    break;
                case EmptySpaceSkipMethod.CHEBYSHEV:
                    break;
                case EmptySpaceSkipMethod.SPARSELEAP:
                    break;
                default:
                    break;
            }
        }

    }
}
