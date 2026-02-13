using System.IO;
using UnityEditor;
using UnityEngine;

namespace EditorTools.BackgroundRemoval
{
    /// <summary>
    /// Editor window to remove solid background color from images.
    /// Handles semi-transparent elements that were blended with the background,
    /// restoring their original transparent appearance.
    /// </summary>
    public sealed class BackgroundRemovalWindow : EditorWindow
    {
        private const string WindowTitle = "Background Removal Tool";
        private const string MenuPath = "Tools/Background Removal Tool";

        private Texture2D _sourceTexture;
        private Color _backgroundColor = Color.green;
        private float _tolerance = 0.01f;
        private float _alphaThreshold = 0.95f;
        private bool _restoreTransparency = true;
        private bool _pickColorFromImage;
        private string _outputSuffix = "_nobg";

        private Texture2D _previewTexture;
        private Vector2 _scrollPosition;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<BackgroundRemovalWindow>(WindowTitle);
            window.minSize = new Vector2(400, 500);
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.LabelField("Background Removal Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Remove solid background color from images.\n" +
                "Handles semi-transparent elements by reversing alpha blending.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            // Source texture selection
            EditorGUILayout.LabelField("Source Image", EditorStyles.boldLabel);
            var newTexture = (Texture2D)EditorGUILayout.ObjectField(
                "Texture", _sourceTexture, typeof(Texture2D), false);

            if (newTexture != _sourceTexture)
            {
                _sourceTexture = newTexture;
                ClearPreview();
            }

            if (_sourceTexture != null)
            {
                EditorGUILayout.LabelField($"Size: {_sourceTexture.width} x {_sourceTexture.height}");
            }

            EditorGUILayout.Space(10);

            // Background color settings
            EditorGUILayout.LabelField("Background Color Settings", EditorStyles.boldLabel);

            _pickColorFromImage = EditorGUILayout.Toggle("Pick Color From Image", _pickColorFromImage);

            if (_pickColorFromImage && _sourceTexture != null)
            {
                EditorGUILayout.HelpBox(
                    "Click 'Sample Top-Left Corner' to use the color from the image corner, " +
                    "or manually set the color below.", MessageType.Info);

                if (GUILayout.Button("Sample Top-Left Corner"))
                {
                    SampleBackgroundColor();
                }
            }

            _backgroundColor = EditorGUILayout.ColorField("Background Color", _backgroundColor);

            EditorGUILayout.Space(10);

            // Processing settings
            EditorGUILayout.LabelField("Processing Settings", EditorStyles.boldLabel);

            _tolerance = EditorGUILayout.Slider(
                new GUIContent("Color Tolerance",
                    "How much color difference is allowed to consider a pixel as background (0-1)"),
                _tolerance, 0f, 0.3f);

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

            _outputSuffix = EditorGUILayout.TextField("Output Suffix", _outputSuffix);

            EditorGUILayout.Space(10);

            // Action buttons
            using (new EditorGUI.DisabledScope(_sourceTexture == null))
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Preview", GUILayout.Height(30)))
                {
                    GeneratePreview();
                }

                if (GUILayout.Button("Process & Save", GUILayout.Height(30)))
                {
                    ProcessAndSave();
                }

                EditorGUILayout.EndHorizontal();
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
            });
        }

        /// <summary>
        /// Generates a preview of the background removal result.
        /// </summary>
        private void GeneratePreview()
        {
            if (_sourceTexture == null) return;

            EnsureTextureReadable(_sourceTexture, () =>
            {
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
                var result = ProcessTexture(_sourceTexture);
                SaveTexture(result, _sourceTexture);
                DestroyImmediate(result);
            });
        }

        /// <summary>
        /// Processes the source texture, removing background and restoring transparency.
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

            var bgColor = new Vector3(_backgroundColor.r, _backgroundColor.g, _backgroundColor.b);

            for (var i = 0; i < sourcePixels.Length; i++)
            {
                var pixel = sourcePixels[i];
                var pixelVec = new Vector3(pixel.r, pixel.g, pixel.b);

                // Calculate color distance from background
                var distance = Vector3.Distance(pixelVec, bgColor);

                if (distance <= _tolerance)
                {
                    // Exact or near-exact background match - make fully transparent
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

                    if (error < 0.01f)
                    {
                        // Check if original color is significantly different from background
                        var origVec = new Vector3(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b));
                        var bgVec = new Vector3(_backgroundColor.r, _backgroundColor.g, _backgroundColor.b);

                        if (Vector3.Distance(origVec, bgVec) > _tolerance * 2f)
                        {
                            originalColor = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
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
