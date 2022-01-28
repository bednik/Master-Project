using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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

/// <summary>
/// Attach to volume object to be able to manipulate in real-time
/// </summary>
[ExecuteInEditMode]
public class VolumeRenderController : MonoBehaviour
{
    [SerializeField] private Shader m_shader;
    [SerializeField] private Texture3D m_volume;

    [SerializeField] private float[] thresholds = new float[2]{0.05f, 0.95f};
    [SerializeField] private Vector4 sliceMin = new Vector4(-0.5f, -0.5f, -0.5f, 1f);
    [SerializeField] private Vector4 sliceMax = new Vector4(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private float intensity = 1f;

    [SerializeField]
    private List<ColorTransferFunctionPoint> colorPoints = new List<ColorTransferFunctionPoint>
    {
        new ColorTransferFunctionPoint(204, 2, 2, 0),
        new ColorTransferFunctionPoint(204, 2, 2, 80),
        new ColorTransferFunctionPoint(204, 2, 204, 82),
        new ColorTransferFunctionPoint(204, 2, 204, 255)
    };

    private List<AlphaTransferFunctionPoint> grayscalePoints = new List<AlphaTransferFunctionPoint>
    {
        new AlphaTransferFunctionPoint(40, 0),
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
        new AlphaTransferFunctionPoint(1, 255)
    };

    private Material m_material;

    // Generate a 1D alpha texture thingy
    public void generateAlphaTable()
    {
        Texture2D alphaTransfer = new Texture2D(256, 1);
        
    }

    // Generate a 1D grayscale texture thingy
    public void generateGrayTable()
    {
        Texture2D grayTransfer = new Texture2D(256, 1);
    }
    // Generate a 1D color texture thingy
    public void generateColorTable()
    {
        Texture2D colorTransfer = new Texture2D(256, 1);
    }

    // Start is called before the first frame update
    private void Awake()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        Transform transform = GetComponent<Transform>();
        m_material = new Material(m_shader);
        m_material.SetTexture("_Volume", m_volume);
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
