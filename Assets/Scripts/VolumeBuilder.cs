using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VolumeRendering;
using VolumeRendering.UI;
using MathNet.Numerics.Interpolation;
using Microsoft.MixedReality.Toolkit.UI;

public class VolumeBuilder : MonoBehaviour
{
    private Shader m_shader;
    private Texture3D m_volume;
    private Texture2D transferFunction;
    private MeshRenderer m_renderer;
    private float m_ERT;
    private int m_blockSize;
    private Material material;
    private VolumeType volumeType;

    [SerializeField] InteractableToggleCollection volumeList;
    [SerializeField] InteractableToggleCollection transferFunctionList;
    [SerializeField] InteractableToggleCollection shaderList;
    [SerializeField] SimpleSliderBehaviour ERTSlider;
    [SerializeField] ClampedSlider ESSSlider;

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
                    new ColorTransferFunctionPoint(42, 0, 0, 20),
                    new ColorTransferFunctionPoint(101, 36, 19, 40),
                    new ColorTransferFunctionPoint(197, 153, 95, 40),
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

    private IEnumerator UniformSubdivision()
    {
        int[] dims = new int[3] { Mathf.CeilToInt((float)m_volume.width / m_blockSize), Mathf.CeilToInt((float)m_volume.height / m_blockSize), Mathf.CeilToInt((float)m_volume.depth / m_blockSize) };
        byte[] textureData = new byte[dims[0] * dims[1] * dims[2]];
        int idx = 0;

        for (int z = 0; z < m_volume.depth; z += m_blockSize)
        {
            for (int y = 0; y < m_volume.height; y += m_blockSize)
            {
                for (int x = 0; x < m_volume.width; x += m_blockSize)
                {
                    textureData[idx] = new OctreeNode((Texture2D)material.GetTexture("_Transfer"), m_volume, new Vector3(x, y, z), new Vector3(x + m_blockSize, y + m_blockSize, z + m_blockSize), 0).empty ? (byte)0 : (byte)255; idx++;
                    yield return new WaitForSecondsRealtime(0.001f);
                }

            }
            Debug.Log(string.Format("{0} out of {1} points done", idx, dims[0] * dims[1] * dims[2]));
        }

        Texture3D subdivision = new Texture3D(dims[0], dims[1], dims[2], TextureFormat.R8, false);
        subdivision.wrapMode = TextureWrapMode.Clamp;
        subdivision.filterMode = FilterMode.Bilinear;
        subdivision.anisoLevel = 0;

        subdivision.SetPixelData(textureData, 0);
        subdivision.Apply();

        m_renderer.sharedMaterial.SetTexture("_EmptySpaceSkipStructure", subdivision);
        yield return null;
    }

    #endregion

    #region Event listeners

    public void PickVolumeEvent()
    {
        StartCoroutine(PickVolume());
    }

    public void PickTransferFunctionEvent()
    {
        transferFunction = GenerateTransferFunction((TransferFunctionType)transferFunctionList.CurrentIndex);
    }

    public IEnumerator PickVolume()
    {
        ResourceRequest req;
        switch (volumeList.CurrentIndex)
        {
            case 0:
                volumeType = VolumeType.US;
                req = Resources.LoadAsync("VolumeTextures/US/us");
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
