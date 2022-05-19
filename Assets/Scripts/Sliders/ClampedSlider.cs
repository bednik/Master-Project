using UnityEngine;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine.UI;

// Adapted from Maria's slider script
namespace VolumeRendering.UI
{
    public class ClampedSlider : MonoBehaviour
    {
        public delegate void OnSliderEvent();
        public OnSliderEvent valueUpdate;
        [SerializeField] private Text _currentValue;
        [SerializeField] private Text _minValue;
        [SerializeField] private Text _maxValue;
        private PinchSlider _pinchSlider;

        [SerializeField] int[] values = new int[4] { 8, 16, 32, 64 };

        public int CurrentValue { get; private set; }

        void Awake()
        {
            Debug.Assert(values != null, "Values textMesh is not set up in SimpleSliderBehaviour on " + gameObject.name);
            _pinchSlider = GetComponentInParent<PinchSlider>();

            if (_pinchSlider == null)
            {
                throw new MissingComponentException($"Parent of {gameObject.name} is missing PinchSlider component");
            }

            ChangeCurrentValueText(values[0]);
            _pinchSlider.SliderValue = 0;
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
            int newValue = values[Mathf.RoundToInt(
                Mathf.Lerp(
                    0, values.Length - 1, data.NewValue)
                )];
            ChangeCurrentValueText(newValue);
        }

        public void ChangeCurrentValueText(int value)
        {
            CurrentValue = value;
            _currentValue.text = $"{value}";
        }

        public void ChangeMinMaxValueText()
        {
            _minValue.text = values[0].ToString();
            _maxValue.text = values[values.Length - 1].ToString();
        }


        /// <summary>
        /// expects a value between 0 and 1
        /// </summary>
        internal void SetNormalisedValue(float newValue)
        {
            _pinchSlider.SliderValue = newValue;
        }
    }
}
