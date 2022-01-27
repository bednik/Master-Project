using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to volume object to be able to manipulate in real-time
/// </summary>
[ExecuteInEditMode]
public class VolumeRenderController : MonoBehaviour
{
    [SerializeField] private Shader m_shader;
    [SerializeField] private Texture3D m_volume;

    [SerializeField] private float[] thresholds { get; set; } = new float[2]{0.05f, 0.95f};
    [SerializeField] private Vector4 sliceMin { get; set; } = new Vector4(-0.5f, -0.5f, -0.5f, 1f);
    [SerializeField] private Vector4 sliceMax { get; set; } = new Vector4(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private float intensity { get; set; } = 1f;

    private Material m_material;

    // Start is called before the first frame update
    private void Awake()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        Transform transform = GetComponent<Transform>();
        m_material = new Material(m_shader);
        if (m_shader.name == "VolumeRendering/Basic" || m_shader.name == "VolumeRendering/Anders")
        {
            m_material.SetTexture("_Volume", m_volume);
            m_material.SetFloat("_ThresholdMin", thresholds[0]);
            m_material.SetFloat("_ThresholdMax", thresholds[1]);
            m_material.SetVector("_SliceMin", sliceMin);
            m_material.SetVector("_SliceMax", sliceMax);
            m_material.SetFloat("_Intensity", intensity);
        }
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
