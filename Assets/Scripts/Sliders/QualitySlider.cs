using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Microsoft.MixedReality.Toolkit.UI;
using VolumeRendering.UI;

public class QualitySlider : MonoBehaviour
{
    VolumeRenderController volumeRenderer;

    // Start is called before the first frame update
    void Start()
    {
        volumeRenderer = GameObject.Find("Gary(Clone)").GetComponent<VolumeRenderController>();
        PinchSlider slider = gameObject.GetComponent<PinchSlider>();
        SimpleSliderBehaviour sliderClamped = gameObject.GetComponent<SimpleSliderBehaviour>();

        slider.OnValueUpdated.AddListener((eventdata) => { volumeRenderer.UpdateQuality(sliderClamped.CurrentValue); });
    }
}
