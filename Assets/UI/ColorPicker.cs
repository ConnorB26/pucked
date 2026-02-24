using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    /// <summary>HSV color picker using a 2D palette texture. X axis = hue, Y axis = saturation. Also supports hex input.</summary>
    public class ColorPicker : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [Serializable]
        public class ColorChangedEvent : UnityEvent<Color>
        {
        }

        [Header("UI References")] [SerializeField]
        private RawImage paletteImage;

        [SerializeField] private RectTransform handle;
        [SerializeField] private TMP_InputField hexInput;

        [Header("Palette Settings")] [SerializeField]
        private int textureSize = 256;

        [Header("Events")] [SerializeField] private ColorChangedEvent onColorChanged;

        private RectTransform _paletteRect;
        private Texture2D _paletteTexture;
        private Color _currentColor = Color.white;

        private bool _suppressHexCallback;

        #region Unity Lifecycle

        private void Awake()
        {
            if (paletteImage != null)
                _paletteRect = paletteImage.rectTransform;

            GeneratePaletteTexture();
            WireHexInput();
        }

        private void Start()
        {
            SetColor(_currentColor, notify: false);
        }

        #endregion

        #region Public API

        public void SetColor(Color color, bool notify = true)
        {
            _currentColor = color;
            Color.RGBToHSV(color, out var h, out var s, out _);

            UpdateHandlePosition(h, s);
            UpdateHexField(color);

            if (notify)
                onColorChanged?.Invoke(color);
        }

        public Color GetColor() => _currentColor;

        public ColorChangedEvent OnColorChanged => onColorChanged;

        #endregion

        #region Pointer Handling

        public void OnPointerDown(PointerEventData eventData)
        {
            UpdateColorFromPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateColorFromPointer(eventData);
        }

        private void UpdateColorFromPointer(PointerEventData eventData)
        {
            if (_paletteRect == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _paletteRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint))
                return;

            var rect = _paletteRect.rect;

            // Clamp inside palette rect
            localPoint.x = Mathf.Clamp(localPoint.x, rect.xMin, rect.xMax);
            localPoint.y = Mathf.Clamp(localPoint.y, rect.yMin, rect.yMax);

            // Convert local point (rect space) -> normalized [0..1]
            var xNorm = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
            var yNorm = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

            var h = Mathf.Clamp01(xNorm);
            var s = Mathf.Clamp01(yNorm);
            const float v = 1f;

            var newColor = Color.HSVToRGB(h, s, v);
            _currentColor = newColor;

            // Now place handle exactly at the pointer position in palette space
            if (handle != null)
                handle.anchoredPosition = localPoint;

            UpdateHexField(newColor);
            onColorChanged?.Invoke(newColor);
        }

        #endregion

        #region Palette Generation

        private void GeneratePaletteTexture()
        {
            if (paletteImage == null || textureSize <= 0)
                return;

            _paletteTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (var y = 0; y < textureSize; y++)
            {
                var s = (float)y / (textureSize - 1);

                for (var x = 0; x < textureSize; x++)
                {
                    var h = (float)x / (textureSize - 1);
                    var c = Color.HSVToRGB(h, s, 1f);
                    _paletteTexture.SetPixel(x, y, c);
                }
            }

            _paletteTexture.Apply();
            paletteImage.texture = _paletteTexture;
        }

        #endregion

        #region Handle & Hex UI

        private void UpdateHandlePosition(float h, float s)
        {
            if (_paletteRect == null || handle == null)
                return;

            var rect = _paletteRect.rect;

            // Recreate the same local point we’d get from a pointer
            var x = Mathf.Lerp(rect.xMin, rect.xMax, h);
            var y = Mathf.Lerp(rect.yMin, rect.yMax, s);

            handle.anchoredPosition = new Vector2(x, y);
        }

        private void WireHexInput()
        {
            if (hexInput == null)
                return;

            hexInput.onValueChanged.AddListener(OnHexChanged);
        }

        private void UpdateHexField(Color color)
        {
            if (hexInput == null)
                return;

            _suppressHexCallback = true;

            Color32 c32 = color;
            hexInput.text = $"#{c32.r:X2}{c32.g:X2}{c32.b:X2}";

            _suppressHexCallback = false;
        }

        private void OnHexChanged(string hex)
        {
            if (_suppressHexCallback)
                return;

            if (TryParseHex(hex, out var color))
            {
                _currentColor = color;
                Color.RGBToHSV(color, out var h, out var s, out _);
                UpdateHandlePosition(h, s);
                onColorChanged?.Invoke(color);
            }
        }

        private static bool TryParseHex(string hex, out Color color)
        {
            color = Color.white;

            if (string.IsNullOrWhiteSpace(hex))
                return false;

            hex = hex.Trim();
            if (hex.StartsWith("#"))
                hex = hex[1..];

            if (hex.Length is 6 or 8 &&
                ColorUtility.TryParseHtmlString("#" + hex, out color))
                return true;

            return false;
        }

        #endregion
    }
}