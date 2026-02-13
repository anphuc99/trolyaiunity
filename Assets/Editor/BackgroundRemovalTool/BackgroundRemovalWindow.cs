using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EditorTools.BackgroundRemoval
{
    /// <summary>
    /// Editor window to remove background color from images.
    /// Supports backgrounds with color variations using HSV color space matching.
    /// Handles semi-transparent elements that were blended with the background.
    /// </summary>
    public sealed class BackgroundRemovalWindow : EditorWindow
    {
        private const string WindowTitle = "Background Removal Tool";
        private const string MenuPath = "Tools/Background Removal Tool";

        private Texture2D _sourceTexture;
        private Color _backgroundColor = Color.green;

        // HSV-based tolerance settings
        private float _hueTolerance = 0.1f;
        private float _saturationTolerance = 0.4f;
        private float _valueTolerance = 0.4f;
        private bool _useHsvMatching = true;
        private float _rgbTolerance = 0.15f;

        private float _alphaThreshold = 0.95f;
        private bool _restoreTransparency = true;
        private bool _autoSampleEdges = true;
        private string _outputSuffix = "_nobg";

        private Texture2D _previewTexture;
        private Vector2 _scrollPosition;

        // Sampled background colors from edges
        private List<Color> _sampledColors = new List<Color>();
        private Vector3 _avgBackgroundHsv;

        // Track if preview needs regeneration
        private bool _needsPreviewUpdate;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<BackgroundRemovalWindow>(WindowTitle);
            window.minSize = new Vector2(420, 600);
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.LabelField("Background Removal Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Remove background with color variations using HSV matching.\n" +
                "Preview updates automatically when settings change.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Begin tracking changes for auto-preview
            EditorGUI.BeginChangeCheck();

            // Source texture selection
            EditorGUILayout.LabelField("Source Image", EditorStyles.boldLabel);
            var newTexture = (Texture2D)EditorGUILayout.ObjectField(
                "Texture", _sourceTexture, typeof(Texture2D), false);

            if (newTexture != _sourceTexture)
            {
                _sourceTexture = newTexture;
                _sampledColors.Clear();
                ClearPreview();
                _needsPreviewUpdate = true;
            }

            if (_sourceTexture != null)
            {
                EditorGUILayout.LabelField($"Size: {_sourceTexture.width} x {_sourceTexture.height}");
            }

            EditorGUILayout.Space(10);

            // Background color settings
            EditorGUILayout.LabelField("Background Color Settings", EditorStyles.boldLabel);

            _autoSampleEdges = EditorGUILayout.Toggle(
                new GUIContent("Auto Sample From Edges",
                    "Automatically sample multiple colors from image edges to detect background"),
                _autoSampleEdges);

            if (_autoSampleEdges && _sourceTexture != null)
            {
                EditorGUILayout.HelpBox(
                    "Click 'Sample Background Colors' to analyze edges and corners.\n" +
                    "This helps detect backgrounds with color variations.",
                    MessageType.Info);

                if (GUILayout.Button("Sample Background Colors", GUILayout.Height(25)))
                {
                    SampleBackgroundColorsFromEdges();
                    _needsPreviewUpdate = true;
                }

                if (_sampledColors.Count > 0)
                {
                    EditorGUILayout.LabelField($"Sampled {_sampledColors.Count} colors from edges");
                }
            }

            _backgroundColor = EditorGUILayout.ColorField("Primary Background Color", _backgroundColor);

            if (GUILayout.Button("Sample From Top-Left Corner"))
            {
                SampleBackgroundColor();
                _needsPreviewUpdate = true;
            }

            EditorGUILayout.Space(10);

            // HSV Matching settings
            EditorGUILayout.LabelField("Color Matching Settings", EditorStyles.boldLabel);

            _useHsvMatching = EditorGUILayout.Toggle(
                new GUIContent("Use HSV Matching",
                    "Use HSV color space for better matching of similar colors"),
                _useHsvMatching);

            if (_useHsvMatching)
            {
                _hueTolerance = EditorGUILayout.Slider(
                    new GUIContent("Hue Tolerance",
                        "How much hue difference is allowed (0-0.5). Green is around 0.33"),
                    _hueTolerance, 0f, 0.5f);

                _saturationTolerance = EditorGUILayout.Slider(
                    new GUIContent("Saturation Tolerance",
                        "How much saturation difference is allowed (0-1)"),
                    _saturationTolerance, 0f, 1f);

                _valueTolerance = EditorGUILayout.Slider(
                    new GUIContent("Value/Brightness Tolerance",
                        "How much brightness difference is allowed (0-1)"),
                    _valueTolerance, 0f, 1f);
            }
            else
            {
                _rgbTolerance = EditorGUILayout.Slider(
                    new GUIContent("RGB Tolerance",
                        "How much RGB color difference is allowed (0-1)"),
                    _rgbTolerance, 0f, 0.5f);
            }

            EditorGUILayout.Space(10);

            // Transparency restoration settings
            EditorGUILayout.LabelField("Transparency Settings", EditorStyles.boldLabel);

            _restoreTransparency = EditorGUILayout.Toggle(
                new GUIContent("Restore Transparency",
                    "Attempt to restore original transparency for semi-transparent elements"),
                _restoreTransparency);

            if (_restoreTransparency)
            {
                _alphaThreshold = EditorGUILayout.Slider(
                    new GUIContent("Alpha Detection Threshold",
                        "Minimum alpha to consider for transparency restoration (0-1)"),
                    _alphaThreshold, 0.5f, 1f);
            }

            // Check if any settings changed
            if (EditorGUI.EndChangeCheck())
            {
                _needsPreviewUpdate = true;
            }

            _outputSuffix = EditorGUILayout.TextField("Output Suffix", _outputSuffix);

            EditorGUILayout.Space(10);

            // Action button - only Save
            using (new EditorGUI.DisabledScope(_sourceTexture == null))
            {
                if (GUILayout.Button("Process & Save", GUILayout.Height(30)))
                {
                    ProcessAndSave();
                }
            }

            // Auto-generate preview if needed
            if (_needsPreviewUpdate && _sourceTexture != null)
            {
                _needsPreviewUpdate = false;
                GeneratePreview();
            }

            // Preview display
            if (_previewTexture != null)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

                var previewRect = GUILayoutUtility.GetRect(
                    _previewTexture.width, _previewTexture.height,
                    GUILayout.MaxWidth(position.width - 20),
                    GUILayout.MaxHeight(300));

                // Draw checkerboard background to show transparency
                DrawCheckerboard(previewRect);
                GUI.DrawTexture(previewRect, _previewTexture, ScaleMode.ScaleToFit);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Samples the background color from the top-left corner of the source texture.
        /// </summary>
        private void SampleBackgroundColor()
        {
            if (_sourceTexture == null) return;

            EnsureTextureReadable(_sourceTexture, () =>
            {
                _backgroundColor = _sourceTexture.GetPixel(0, _sourceTexture.height - 1);
                _backgroundColor.a = 1f;
                _sampledColors.Clear();
                _sampledColors.Add(_backgroundColor);
                UpdateAverageBackgroundHsv();
            });
        }

        /// <summary>
        /// Samples background colors from multiple edge positions to handle color variations.
        /// </summary>
        private void SampleBackgroundColorsFromEdges()
        {
            if (_sourceTexture == null) return;

            EnsureTextureReadable(_sourceTexture, () =>
            {
                _sampledColors.Clear();
                var width = _sourceTexture.width;
                var height = _sourceTexture.height;

                // Sample from corners
                _sampledColors.Add(_sourceTexture.GetPixel(0, height - 1)); // Top-left
                _sampledColors.Add(_sourceTexture.GetPixel(width - 1, height - 1)); // Top-right
                _sampledColors.Add(_sourceTexture.GetPixel(0, 0)); // Bottom-left
                _sampledColors.Add(_sourceTexture.GetPixel(width - 1, 0)); // Bottom-right

                // Sample from edges (multiple points)
                var edgeSamples = 10;
                for (var i = 0; i < edgeSamples; i++)
                {
                    var t = (float)i / (edgeSamples - 1);
                    var x = Mathf.FloorToInt(t * (width - 1));
                    var y = Mathf.FloorToInt(t * (height - 1));

                    // Top edge
                    _sampledColors.Add(_sourceTexture.GetPixel(x, height - 1));
                    // Bottom edge
                    _sampledColors.Add(_sourceTexture.GetPixel(x, 0));
                    // Left edge
                    _sampledColors.Add(_sourceTexture.GetPixel(0, y));
                    // Right edge
                    _sampledColors.Add(_sourceTexture.GetPixel(width - 1, y));
                }

                // Set primary background color to average
                var avgColor = Color.black;
                foreach (var c in _sampledColors)
                {
                    avgColor += c;
                }
                avgColor /= _sampledColors.Count;
                _backgroundColor = avgColor;
                _backgroundColor.a = 1f;

                UpdateAverageBackgroundHsv();
            });
        }

        /// <summary>
        /// Updates the average HSV values from sampled background colors.
        /// </summary>
        private void UpdateAverageBackgroundHsv()
        {
            if (_sampledColors.Count == 0)
            {
                Color.RGBToHSV(_backgroundColor, out var h, out var s, out var v);
                _avgBackgroundHsv = new Vector3(h, s, v);
                return;
            }

            var totalH = 0f;
            var totalS = 0f;
            var totalV = 0f;

            foreach (var color in _sampledColors)
            {
                Color.RGBToHSV(color, out var h, out var s, out var v);
                totalH += h;
                totalS += s;
                totalV += v;
            }

            _avgBackgroundHsv = new Vector3(
                totalH / _sampledColors.Count,
                totalS / _sampledColors.Count,
                totalV / _sampledColors.Count
            );
        }

        /// <summary>
        /// Generates a preview of the background removal result.
        /// </summary>
        private void GeneratePreview()
        {
            if (_sourceTexture == null) return;

            EnsureTextureReadable(_sourceTexture, () =>
            {
                if (_sampledColors.Count == 0)
                {
                    UpdateAverageBackgroundHsv();
                }
                _previewTexture = ProcessTexture(_sourceTexture);
            });
        }

        /// <summary>
        /// Processes the texture and saves it to disk.
        /// </summary>
        private void ProcessAndSave()
        {
            if (_sourceTexture == null) return;

            EnsureTextureReadable(_sourceTexture, () =>
            {
                if (_sampledColors.Count == 0)
                {
                    UpdateAverageBackgroundHsv();
                }
                var result = ProcessTexture(_sourceTexture);
                SaveTexture(result, _sourceTexture);
                DestroyImmediate(result);
            });
        }

        /// <summary>
        /// Processes the source texture, removing background and restoring transparency.
        /// Uses HSV color space for better matching of similar colors.
        /// </summary>
        /// <param name="source">Source texture to process.</param>
        /// <returns>New texture with background removed.</returns>
        private Texture2D ProcessTexture(Texture2D source)
        {
            var width = source.width;
            var height = source.height;
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var sourcePixels = source.GetPixels();
            var resultPixels = new Color[sourcePixels.Length];

            for (var i = 0; i < sourcePixels.Length; i++)
            {
                var pixel = sourcePixels[i];

                if (IsBackgroundColor(pixel))
                {
                    // Background match - make fully transparent
                    resultPixels[i] = new Color(0, 0, 0, 0);
                }
                else if (_restoreTransparency && IsBlendedWithBackground(pixel, out var originalColor, out var originalAlpha))
                {
                    // This pixel appears to be a semi-transparent color blended with background
                    resultPixels[i] = new Color(originalColor.r, originalColor.g, originalColor.b, originalAlpha);
                }
                else
                {
                    // Keep original pixel
                    resultPixels[i] = pixel;
                }
            }

            result.SetPixels(resultPixels);
            result.Apply();
            return result;
        }

        /// <summary>
        /// Checks if a pixel color matches the background using HSV or RGB comparison.
        /// </summary>
        /// <param name="pixel">The pixel color to check.</param>
        /// <returns>True if the pixel is a background color.</returns>
        private bool IsBackgroundColor(Color pixel)
        {
            if (_useHsvMatching)
            {
                return IsBackgroundColorHsv(pixel);
            }
            else
            {
                return IsBackgroundColorRgb(pixel);
            }
        }

        /// <summary>
        /// Checks if a pixel matches background using HSV color space.
        /// Better for detecting similar shades of the same color.
        /// </summary>
        /// <param name="pixel">The pixel color to check.</param>
        /// <returns>True if the pixel matches background in HSV space.</returns>
        private bool IsBackgroundColorHsv(Color pixel)
        {
            Color.RGBToHSV(pixel, out var h, out var s, out var v);

            // Check against average background HSV
            var hueDiff = Mathf.Abs(h - _avgBackgroundHsv.x);
            // Hue wraps around (0 and 1 are the same)
            if (hueDiff > 0.5f) hueDiff = 1f - hueDiff;

            var satDiff = Mathf.Abs(s - _avgBackgroundHsv.y);
            var valDiff = Mathf.Abs(v - _avgBackgroundHsv.z);

            if (hueDiff <= _hueTolerance && satDiff <= _saturationTolerance && valDiff <= _valueTolerance)
            {
                return true;
            }

            // Also check against all sampled colors for edge cases
            foreach (var sampledColor in _sampledColors)
            {
                Color.RGBToHSV(sampledColor, out var sh, out var ss, out var sv);
                hueDiff = Mathf.Abs(h - sh);
                if (hueDiff > 0.5f) hueDiff = 1f - hueDiff;

                satDiff = Mathf.Abs(s - ss);
                valDiff = Mathf.Abs(v - sv);

                if (hueDiff <= _hueTolerance && satDiff <= _saturationTolerance && valDiff <= _valueTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a pixel matches background using RGB euclidean distance.
        /// </summary>
        /// <param name="pixel">The pixel color to check.</param>
        /// <returns>True if the pixel matches background in RGB space.</returns>
        private bool IsBackgroundColorRgb(Color pixel)
        {
            var pixelVec = new Vector3(pixel.r, pixel.g, pixel.b);
            var bgVec = new Vector3(_backgroundColor.r, _backgroundColor.g, _backgroundColor.b);

            if (Vector3.Distance(pixelVec, bgVec) <= _rgbTolerance)
            {
                return true;
            }

            // Also check against sampled colors
            foreach (var sampledColor in _sampledColors)
            {
                var sampledVec = new Vector3(sampledColor.r, sampledColor.g, sampledColor.b);
                if (Vector3.Distance(pixelVec, sampledVec) <= _rgbTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if a pixel is the result of alpha blending a foreground color with the background.
        /// If so, calculates the original foreground color and alpha.
        /// </summary>
        /// <param name="blendedColor">The color to analyze.</param>
        /// <param name="originalColor">Output: the estimated original foreground color.</param>
        /// <param name="originalAlpha">Output: the estimated original alpha value.</param>
        /// <returns>True if the pixel appears to be blended with background.</returns>
        private bool IsBlendedWithBackground(Color blendedColor, out Color originalColor, out float originalAlpha)
        {
            originalColor = blendedColor;
            originalAlpha = 1f;

            // Try different alpha values to see if this could be a blended color
            // Formula: blended = foreground * alpha + background * (1 - alpha)
            // Solving for foreground: foreground = (blended - background * (1 - alpha)) / alpha

            for (var alpha = _alphaThreshold; alpha >= 0.1f; alpha -= 0.05f)
            {
                var invAlpha = 1f - alpha;

                // Calculate potential original foreground color
                var r = (blendedColor.r - _backgroundColor.r * invAlpha) / alpha;
                var g = (blendedColor.g - _backgroundColor.g * invAlpha) / alpha;
                var b = (blendedColor.b - _backgroundColor.b * invAlpha) / alpha;

                // Check if the calculated color is valid (within 0-1 range)
                if (r >= -0.01f && r <= 1.01f &&
                    g >= -0.01f && g <= 1.01f &&
                    b >= -0.01f && b <= 1.01f)
                {
                    // Verify by reversing: does this foreground + alpha recreate the blended color?
                    var testR = Mathf.Clamp01(r) * alpha + _backgroundColor.r * invAlpha;
                    var testG = Mathf.Clamp01(g) * alpha + _backgroundColor.g * invAlpha;
                    var testB = Mathf.Clamp01(b) * alpha + _backgroundColor.b * invAlpha;

                    var error = Mathf.Abs(testR - blendedColor.r) +
                                Mathf.Abs(testG - blendedColor.g) +
                                Mathf.Abs(testB - blendedColor.b);

                    if (error < 0.02f)
                    {
                        // Check if original color is significantly different from background
                        var origColor = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b));

                        // Use a stricter check - original should not look like background
                        if (!IsBackgroundColor(origColor))
                        {
                            originalColor = origColor;
                            originalAlpha = alpha;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Saves the processed texture to disk as PNG.
        /// </summary>
        /// <param name="texture">Texture to save.</param>
        /// <param name="originalTexture">Original texture for path reference.</param>
        private void SaveTexture(Texture2D texture, Texture2D originalTexture)
        {
            var originalPath = AssetDatabase.GetAssetPath(originalTexture);
            var directory = Path.GetDirectoryName(originalPath);
            var filename = Path.GetFileNameWithoutExtension(originalPath);
            var newPath = Path.Combine(directory, filename + _outputSuffix + ".png");

            var bytes = texture.EncodeToPNG();
            File.WriteAllBytes(newPath, bytes);

            AssetDatabase.Refresh();

            // Configure import settings for the new texture
            var importer = AssetImporter.GetAtPath(newPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            Debug.Log($"Background removed and saved to: {newPath}");
            EditorUtility.DisplayDialog(WindowTitle, $"Saved to:\n{newPath}", "OK");

            // Select the new asset
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(newPath);
        }

        /// <summary>
        /// Ensures the texture is readable before performing operations.
        /// Temporarily enables Read/Write if needed.
        /// </summary>
        /// <param name="texture">Texture to check.</param>
        /// <param name="action">Action to perform when texture is readable.</param>
        private void EnsureTextureReadable(Texture2D texture, System.Action action)
        {
            var path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
            {
                Debug.LogError("Could not get TextureImporter for the selected texture.");
                return;
            }

            var wasReadable = importer.isReadable;

            if (!wasReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            try
            {
                action?.Invoke();
            }
            finally
            {
                if (!wasReadable)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }
        }

        /// <summary>
        /// Draws a checkerboard pattern to visualize transparency.
        /// </summary>
        /// <param name="rect">Rectangle to draw in.</param>
        private void DrawCheckerboard(Rect rect)
        {
            var checkerSize = 10f;
            var light = new Color(0.8f, 0.8f, 0.8f);
            var dark = new Color(0.6f, 0.6f, 0.6f);

            var xCount = Mathf.CeilToInt(rect.width / checkerSize);
            var yCount = Mathf.CeilToInt(rect.height / checkerSize);

            for (var y = 0; y < yCount; y++)
            {
                for (var x = 0; x < xCount; x++)
                {
                    var isLight = (x + y) % 2 == 0;
                    EditorGUI.DrawRect(new Rect(
                        rect.x + x * checkerSize,
                        rect.y + y * checkerSize,
                        checkerSize,
                        checkerSize
                    ), isLight ? light : dark);
                }
            }
        }

        /// <summary>
        /// Clears the preview texture.
        /// </summary>
        private void ClearPreview()
        {
            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }
        }

        private void OnDestroy()
        {
            ClearPreview();
        }
    }
}
