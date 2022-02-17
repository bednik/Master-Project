using UnityEngine;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine.UI;

// Adapted from Maria Nylund's slider script
public class SimpleSliderBehaviour : MonoBehaviour
{
    public delegate void OnSliderEvent();
    public OnSliderEvent valueUpdate;
    [SerializeField]
    private Vector2 minMaxValue = Vector2.up;
    [SerializeField]
    private Text _currentValue;
    [SerializeField]
    private Text _minValue;
    [SerializeField]
    private Text _maxValue;
    private PinchSlider _pinchSlider;

    [SerializeField]
    private float startPos = 0.5f;
    
    public float CurrentValue {  get; private set; }
    private int accuracy = 2;
    [SerializeField]
    private string floatAccuracy = "F0";
    // Start is called before the first frame update
    void Awake()
    {
        Debug.Assert(_currentValue != null, "CurrentValue textMesh is not set up in SimpleSliderBehaviour on " + gameObject.name);
        Debug.Assert(_minValue != null, "MinValue textMesh is not set up in SimpleSliderBehaviour on " + gameObject.name);
        Debug.Assert(_maxValue != null, "MaxValue textMesh is not set up in SimpleSliderBehaviour on " + gameObject.name);

        _pinchSlider = GetComponentInParent<PinchSlider>();
        if(_pinchSlider == null)
        {
            throw new MissingComponentException($"Parent of {gameObject.name} is missing PinchSlider component");
        }

        ChangeCurrentValueText(startPos);
        _pinchSlider.SliderValue = startPos;

        ChangeMinMaxValueText(minMaxValue.x, minMaxValue.y);
        Debug.Log("Current value of " + name + " " + CurrentValue);
        _pinchSlider.OnValueUpdated.AddListener(OnSliderChange);
        _pinchSlider.OnInteractionEnded.AddListener((SliderEventData eventData) => valueUpdate?.Invoke());
    }

    private void OnDestroy()
    {
        _pinchSlider.OnValueUpdated.RemoveListener(OnSliderChange);
    }

    public void OnSliderChange(SliderEventData data)
    {
        float newValue = (float)System.Math.Round((double)Mathf.Lerp(
            minMaxValue.x, minMaxValue.y, data.NewValue)
            , accuracy);
        ChangeCurrentValueText(newValue);
    }

    public void ChangeCurrentValueText(float value)
    {
        CurrentValue = value;
        _currentValue.text = $"{value.ToString(floatAccuracy)}";
    }

    public void ChangeMinMaxValueText(float minValue, float maxValue)
    {
        _minValue.text = minValue.ToString(floatAccuracy);
        _maxValue.text = maxValue.ToString(floatAccuracy);
    }


    /// <summary>
    /// expects a value between 0 and 1
    /// </summary>
    internal void SetNormalisedValue(float newValue)
    {
        _pinchSlider.SliderValue = newValue;
    }
}