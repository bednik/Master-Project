using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MathNet.Numerics.Interpolation;

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

class OccupancyNode
{
    public OccupancyNode(CubicSpline transferFunction, Texture3D vol, Vector3 _min, Vector3 _max)
    {
        min = _min;
        max = _max;
        //vol.
    }

    public Vector3 min, max;
    public int minVal, maxVal;
    public int empty, nonEmpty, unknown;
    Occupancy occupancyClass;

    List<OccupancyNode> children { get; set; }
}

enum EmptySpaceSkipMethod
{
    OCTREE,
    CHEBYSHEV,
    SPARSELEAP
}

enum Occupancy
{
    EMPTY,
    NONEMPTY,
    UNKNOWN
}

/// <summary>
/// Attach to volume object to be able to manipulate in real-time
/// </summary>
[ExecuteInEditMode]
public class VolumeRenderController : MonoBehaviour
{
    [SerializeField] private Shader m_shader;
    [SerializeField] private Texture3D m_volume;

    [SerializeField] private bool color = false;

    // Transfer functions
    CubicSpline alphaTransferFunction;
    CubicSpline grayTransferFunction;
    CubicSpline[] colorTransferFunction;

    // Empty-space skipping
    [SerializeField] private bool emptySpaceSkip = false;
    [SerializeField] private EmptySpaceSkipMethod emptySpaceSkipMethod = EmptySpaceSkipMethod.OCTREE;
    [SerializeField] private int blockSize = 16;
    [SerializeField] private int octreeLevels = 2;

    // Shader properties
    [SerializeField] private float[] thresholds = new float[2]{0.05f, 0.95f};
    [SerializeField] private Vector4 sliceMin = new Vector4(-0.5f, -0.5f, -0.5f, 1f);
    [SerializeField] private Vector4 sliceMax = new Vector4(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private float intensity = 1f;

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

    /// <summary>
    /// Generate an octree structure representing sub-volumes and whether they will contribute to the final color
    /// </summary>
    /// <returns>An RGBA32 3D texture. R and G represent the min and max values of</returns>
    /*private Texture3D generateOctree()
    {
        Texture3D octree = new Texture3D(m_volume.width / blockSize, m_volume.height / blockSize, m_volume.depth / blockSize, TextureFormat.RGBA32, false);
        octree.wrapMode = TextureWrapMode.Clamp;
        octree.filterMode = FilterMode.Bilinear;
        octree.anisoLevel = 0;
    }*/

    private void Awake()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        Transform transform = GetComponent<Transform>();
        m_material = new Material(m_shader);
        m_material.SetTexture("_Volume", m_volume);

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
                case EmptySpaceSkipMethod.OCTREE:
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
        m_material.SetFloat("_Intensity", intensity);
        renderer.material = m_material;

        transform.localScale = (new Vector3(m_volume.width, m_volume.height, m_volume.depth))/1000;
    }

    /*private void Update()
    {
        if (m_shader.name == "VolumeRendering/Basic" || m_shader.name == "VolumeRendering/Anders")
        {
            m_material.SetTexture("_Volume", m_volume);
            m_material.SetFloat("_ThresholdMin", thresholds[0]);
            m_material.SetFloat("_ThresholdMax", thresholds[1]);
            m_material.SetVector("_SliceMin", sliceMin);
            m_material.SetVector("_SliceMax", sliceMax);
            m_material.SetFloat("_Intensity", intensity);
        }
    }*/
}
