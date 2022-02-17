using UnityEngine;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine.UI;

// Adapted from Maria's slider script
public class ClampedSlider : MonoBehaviour
{
    public delegate void OnSliderEvent();
    public OnSliderEvent valueUpdate;
    private Text _currentValue;
    private Text _minValue;
    private Text _maxValue;
    private PinchSlider _pinchSlider;

    [SerializeField] int[] values = new int[4] { 8, 16, 32, 64 };

    public float CurrentValue { get; private set; }

    [SerializeField]
    private string floatAccuracy = "F0";

    void Awake()
    {
        Debug.Assert(_currentValue != null, "CurrentValue textMesh is not set up in SimpleSliderBehaviour on " + gameObject.name);
        Debug.Assert(values != null, "Values textMesh is not set up in SimpleSliderBehaviour on " + gameObject.name);

        _pinchSlider = GetComponentInParent<PinchSlider>();
        if (_pinchSlider == null)
        {
            throw new MissingComponentException($"Parent of {gameObject.name} is missing PinchSlider component");
        }

        ChangeMinMaxValueText();
        _pinchSlider.OnValueUpdated.AddListener(OnSliderChange);
        _pinchSlider.OnInteractionEnded.AddListener((SliderEventData eventData) => valueUpdate?.Invoke());
    }

    private void OnDestroy()
    {
        _pinchSlider.OnValueUpdated.RemoveListener(OnSliderChange);
    }

    public void OnSliderChange(SliderEventData data)
    {
        float newValue = values[Mathf.RoundToInt(
            Mathf.Lerp(
                0, values.Length - 1, data.NewValue)
            )];
        ChangeCurrentValueText(newValue);
    }

    public void ChangeCurrentValueText(float value)
    {
        CurrentValue = value;
        _currentValue.text = $"{value.ToString(floatAccuracy)}";
    }

    public void ChangeMinMaxValueText()
    {
        _minValue.text = values[0].ToString(floatAccuracy);
        _maxValue.text = values[values.Length-1].ToString(floatAccuracy);
    }


    /// <summary>
    /// expects a value between 0 and 1
    /// </summary>
    internal void SetNormalisedValue(float newValue)
    {
        _pinchSlider.SliderValue = newValue;
    }
}