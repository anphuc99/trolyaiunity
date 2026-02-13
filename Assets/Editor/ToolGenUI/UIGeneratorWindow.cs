using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EditorTools.UIGenerator
{
    /// <summary>
    /// Main window for AI-powered UI generation using Gemini API.
    /// Features: image generation, background removal, color adjustments, brush/eraser tools, auto-crop.
    /// </summary>
    public sealed class UIGeneratorWindow : EditorWindow
    {
        private const string WindowTitle = "AI UI Generator";
        private const string MenuPath = "Tools/AI UI Generator";

        // Target Image component
        private Image _targetImage;

        // Generation settings (Left panel - 1/3)
        private string _prompt = "A stylish game button with glossy effect";
        private bool _generateUI = true;
        private int _numberOfImages = 2;
        private Texture2D _referenceBackground;
        private bool _useReferenceBackground;
        private bool _isGenerating;
        private string _generationStatus = "";

        // Generated images selection
        private List<Texture2D> _generatedImages = new List<Texture2D>();
        private int _selectedImageIndex = -1;
        private Vector2 _generatedImagesScroll;

        // Direct image editing
        private Texture2D _directEditImage;

        // Editing settings (Right panel - 2/3)
        private Texture2D _editingTexture;
        private Texture2D _previewTexture;
        private Texture2D _originalTexture; // For undo
        private Texture2D _protectionMask; // Tracks manually edited pixels to protect from BG removal
        private Vector2 _editingScroll;
        private bool _needsPreviewUpdate;

        // Background removal
        private bool _enableBackgroundRemoval = false;
        private Color _backgroundColor = Color.green;
        private float _hueTolerance = 0.15f;
        private float _saturationTolerance = 0.5f;
        private float _valueTolerance = 0.5f;
        private bool _autoSampleBackground = true;

        // Color adjustments
        private float _hueShift = 0f;
        private float _saturationAdjust = 0f;
        private float _brightnessAdjust = 0f;

        // Brush/Eraser tools
        private enum ToolMode { None, Brush, Eraser, Restore }
        private ToolMode _currentTool = ToolMode.None;
        private Color _brushColor = Color.white;
        private int _brushSize = 10;
        private bool _isPainting;
        private Vector2 _lastPaintPos;

        // Canvas for painting
        private RenderTexture _paintCanvas;
        private Texture2D _paintTexture;

        // Sampled colors for background removal
        private List<Color> _sampledColors = new List<Color>();
        private Vector3 _avgBackgroundHsv;

        // Scroll positions
        private Vector2 _leftPanelScroll;
        private Vector2 _rightPanelScroll;

        /// <summary>
        /// Opens the window with optional target Image component.
        /// </summary>
        public static void Open(Image targetImage = null)
        {
            var window = GetWindow<UIGeneratorWindow>(WindowTitle);
            window.minSize = new Vector2(900, 600);
            window._targetImage = targetImage;

            if (targetImage != null && targetImage.sprite != null)
            {
                window._referenceBackground = targetImage.sprite.texture;
            }
        }

        [MenuItem(MenuPath)]
        private static void OpenFromMenu()
        {
            Open(null);
        }

        private void OnEnable()
        {
            _paintTexture = new Texture2D(1, 1);
        }

        private void OnDisable()
        {
            CleanupTextures();
        }

        private void OnDestroy()
        {
            CleanupTextures();
        }

        private void CleanupTextures()
        {
            if (_paintCanvas != null)
            {
                _paintCanvas.Release();
                DestroyImmediate(_paintCanvas);
            }
            if (_previewTexture != null) DestroyImmediate(_previewTexture);
            if (_paintTexture != null) DestroyImmediate(_paintTexture);
            foreach (var tex in _generatedImages)
            {
                if (tex != null) DestroyImmediate(tex);
            }
            _generatedImages.Clear();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // Left panel (1/3) - Generation
            DrawLeftPanel();

            // Separator
            GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));

            // Right panel (2/3) - Editing
            DrawRightPanel();

            EditorGUILayout.EndHorizontal();

            // Handle preview updates
            if (_needsPreviewUpdate && _editingTexture != null)
            {
                _needsPreviewUpdate = false;
                UpdatePreview();
            }
        }

        #region Left Panel - Generation

        private void DrawLeftPanel()
        {
            var panelWidth = position.width / 3f - 10f;
            EditorGUILayout.BeginVertical(GUILayout.Width(panelWidth));
            _leftPanelScroll = EditorGUILayout.BeginScrollView(_leftPanelScroll);

            EditorGUILayout.LabelField("AI Image Generation", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // API Settings
            DrawApiSettings();
            EditorGUILayout.Space(10);

            // Target Image
            DrawTargetImageSection();
            EditorGUILayout.Space(10);

            // Generation Type
            DrawGenerationTypeSection();
            EditorGUILayout.Space(10);

            // Prompt
            DrawPromptSection();
            EditorGUILayout.Space(10);

            // Reference Background
            DrawReferenceBackgroundSection();
            EditorGUILayout.Space(10);

            // Direct Edit Image
            DrawDirectEditSection();
            EditorGUILayout.Space(10);

            // Generate Button
            DrawGenerateButton();
            EditorGUILayout.Space(10);

            // Generated Images Selection
            DrawGeneratedImagesSection();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawApiSettings()
        {
            EditorGUILayout.LabelField("API Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var apiKey = EditorGUILayout.PasswordField("API Key", GeminiAPIClient.ApiKey);
            if (EditorGUI.EndChangeCheck())
            {
                GeminiAPIClient.ApiKey = apiKey;
            }

            EditorGUI.BeginChangeCheck();
            var modelIndex = EditorGUILayout.Popup("Model", GeminiAPIClient.SelectedModelIndex, GeminiAPIClient.AvailableModels);
            if (EditorGUI.EndChangeCheck())
            {
                GeminiAPIClient.SelectedModelIndex = modelIndex;
            }

            if (string.IsNullOrEmpty(GeminiAPIClient.ApiKey))
            {
                EditorGUILayout.HelpBox("Enter your Gemini API key to enable generation.", MessageType.Warning);
            }
        }

        private void DrawTargetImageSection()
        {
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            _targetImage = (Image)EditorGUILayout.ObjectField("Image Component", _targetImage, typeof(Image), true);

            if (_targetImage != null && _targetImage.sprite != null)
            {
                EditorGUILayout.LabelField($"Current: {_targetImage.sprite.texture.width}x{_targetImage.sprite.texture.height}");
            }
        }

        private void DrawGenerationTypeSection()
        {
            EditorGUILayout.LabelField("Generation Type", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_generateUI, "UI Element", "Button"))
            {
                _generateUI = true;
            }
            if (GUILayout.Toggle(!_generateUI, "Background", "Button"))
            {
                _generateUI = false;
            }
            EditorGUILayout.EndHorizontal();

            _numberOfImages = EditorGUILayout.IntSlider("Number of Images", _numberOfImages, 1, 4);
        }

        private void DrawPromptSection()
        {
            EditorGUILayout.LabelField("Prompt", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                _generateUI
                    ? "Describe the UI element you want (button, icon, panel, etc.)"
                    : "Describe the background scene you want",
                MessageType.Info);

            _prompt = EditorGUILayout.TextArea(_prompt, GUILayout.Height(80));
        }

        private void DrawReferenceBackgroundSection()
        {
            EditorGUILayout.LabelField("Reference Background", EditorStyles.boldLabel);

            _useReferenceBackground = EditorGUILayout.Toggle("Use Reference for Context", _useReferenceBackground);

            if (_useReferenceBackground)
            {
                _referenceBackground = (Texture2D)EditorGUILayout.ObjectField(
                    "Background Image", _referenceBackground, typeof(Texture2D), false);

                if (_targetImage != null && _targetImage.sprite != null)
                {
                    if (GUILayout.Button("Use Current Image Sprite"))
                    {
                        _referenceBackground = _targetImage.sprite.texture;
                    }
                }

                EditorGUILayout.HelpBox(
                    "AI will consider this background when generating to ensure visual compatibility.",
                    MessageType.Info);
            }
        }

        private void DrawDirectEditSection()
        {
            EditorGUILayout.LabelField("Edit Existing Image", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _directEditImage = (Texture2D)EditorGUILayout.ObjectField(
                "Image to Edit", _directEditImage, typeof(Texture2D), false);

            if (EditorGUI.EndChangeCheck() && _directEditImage != null)
            {
                LoadDirectImageForEditing(_directEditImage);
            }

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(_directEditImage == null))
            {
                if (GUILayout.Button("Load for Editing"))
                {
                    LoadDirectImageForEditing(_directEditImage);
                }
            }

            using (new EditorGUI.DisabledScope(_targetImage == null || _targetImage.sprite == null))
            {
                if (GUILayout.Button("Load Target Sprite"))
                {
                    _directEditImage = _targetImage.sprite.texture;
                    LoadDirectImageForEditing(_directEditImage);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Drag an image here to edit it directly without generating new images.",
                MessageType.Info);
        }

        /// <summary>
        /// Loads an existing image directly into the editing panel.
        /// </summary>
        private void LoadDirectImageForEditing(Texture2D sourceTexture)
        {
            if (sourceTexture == null) return;

            // Get readable version of the texture
            var readableSource = GetReadableTexture(sourceTexture);

            // Create editing texture
            _editingTexture = new Texture2D(readableSource.width, readableSource.height, TextureFormat.RGBA32, false);
            _editingTexture.SetPixels(readableSource.GetPixels());
            _editingTexture.Apply();

            // Store original for undo
            _originalTexture = new Texture2D(readableSource.width, readableSource.height, TextureFormat.RGBA32, false);
            _originalTexture.SetPixels(readableSource.GetPixels());
            _originalTexture.Apply();

            // Initialize protection mask (all black = no protection)
            if (_protectionMask != null) DestroyImmediate(_protectionMask);
            _protectionMask = new Texture2D(readableSource.width, readableSource.height, TextureFormat.RGBA32, false);
            var clearPixels = new Color[readableSource.width * readableSource.height];
            for (int i = 0; i < clearPixels.Length; i++) clearPixels[i] = Color.black;
            _protectionMask.SetPixels(clearPixels);
            _protectionMask.Apply();

            // Clean up temporary texture if created
            if (readableSource != sourceTexture)
            {
                DestroyImmediate(readableSource);
            }

            // Reset editing state
            _sampledColors.Clear();
            _currentTool = ToolMode.None;
            _needsPreviewUpdate = true;
            _selectedImageIndex = -1;

            // Sample background colors
            if (_autoSampleBackground)
            {
                SampleBackgroundColors();
            }

            _generationStatus = $"Loaded image for editing: {sourceTexture.width}x{sourceTexture.height}";
            Repaint();
        }

        /// <summary>
        /// Gets a readable copy of the texture. If already readable, returns the original.
        /// </summary>
        private Texture2D GetReadableTexture(Texture2D source)
        {
            if (source == null) return null;
            if (source.isReadable) return source;

            // Create a temporary RenderTexture to copy the texture
            var renderTex = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);

            Graphics.Blit(source, renderTex);

            var previousActive = RenderTexture.active;
            RenderTexture.active = renderTex;

            var readableTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readableTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readableTexture.Apply();

            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTex);

            return readableTexture;
        }

        private void DrawGenerateButton()
        {
            using (new EditorGUI.DisabledScope(_isGenerating || string.IsNullOrEmpty(GeminiAPIClient.ApiKey)))
            {
                if (GUILayout.Button(_isGenerating ? "Generating..." : "Generate Images", GUILayout.Height(35)))
                {
                    StartGeneration();
                }
            }

            if (!string.IsNullOrEmpty(_generationStatus))
            {
                EditorGUILayout.HelpBox(_generationStatus, MessageType.Info);
            }
        }

        private void DrawGeneratedImagesSection()
        {
            if (_generatedImages.Count == 0) return;

            EditorGUILayout.LabelField("Generated Images", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Click an image to select it for editing.", MessageType.Info);

            _generatedImagesScroll = EditorGUILayout.BeginScrollView(_generatedImagesScroll, GUILayout.Height(200));
            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < _generatedImages.Count; i++)
            {
                var tex = _generatedImages[i];
                if (tex == null) continue;

                var isSelected = i == _selectedImageIndex;
                var style = isSelected ? "Button" : "Box";

                EditorGUILayout.BeginVertical(GUILayout.Width(100));

                var rect = GUILayoutUtility.GetRect(90, 90);
                if (isSelected)
                {
                    EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), Color.cyan);
                }
                GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);

                if (GUILayout.Button(isSelected ? "Selected" : "Select", GUILayout.Width(90)))
                {
                    SelectGeneratedImage(i);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private void StartGeneration()
        {
            _isGenerating = true;
            _generationStatus = "Generating images...";

            // Clear previous images
            foreach (var tex in _generatedImages)
            {
                if (tex != null) DestroyImmediate(tex);
            }
            _generatedImages.Clear();
            _selectedImageIndex = -1;

            var refImage = _useReferenceBackground ? _referenceBackground : null;

            GeminiAPIClient.GenerateImages(_prompt, refImage, _numberOfImages, _generateUI, result =>
            {
                _isGenerating = false;

                if (result.Success)
                {
                    _generatedImages = result.GeneratedImages;
                    _generationStatus = $"Generated {result.GeneratedImages.Count} image(s). Select one to edit.";

                    if (_generatedImages.Count == 1)
                    {
                        SelectGeneratedImage(0);
                    }
                }
                else
                {
                    _generationStatus = $"Error: {result.Error}";
                }

                Repaint();
            });
        }

        private void SelectGeneratedImage(int index)
        {
            _selectedImageIndex = index;

            if (index >= 0 && index < _generatedImages.Count)
            {
                // Copy to editing texture using GetReadableTexture to handle format differences
                var source = _generatedImages[index];
                var readableSource = GetReadableTexture(source);

                _editingTexture = new Texture2D(readableSource.width, readableSource.height, TextureFormat.RGBA32, false);
                _editingTexture.SetPixels(readableSource.GetPixels());
                _editingTexture.Apply();

                // Store original for undo
                _originalTexture = new Texture2D(readableSource.width, readableSource.height, TextureFormat.RGBA32, false);
                _originalTexture.SetPixels(readableSource.GetPixels());
                _originalTexture.Apply();

                // Initialize protection mask (all black = no protection)
                if (_protectionMask != null) DestroyImmediate(_protectionMask);
                _protectionMask = new Texture2D(readableSource.width, readableSource.height, TextureFormat.RGBA32, false);
                var clearPixels = new Color[readableSource.width * readableSource.height];
                for (int i = 0; i < clearPixels.Length; i++) clearPixels[i] = Color.black;
                _protectionMask.SetPixels(clearPixels);
                _protectionMask.Apply();

                // Clean up if we created a copy
                if (readableSource != source)
                {
                    DestroyImmediate(readableSource);
                }

                // Reset editing state
                _sampledColors.Clear();
                _currentTool = ToolMode.None;
                _needsPreviewUpdate = true;

                // Sample background colors
                if (_autoSampleBackground)
                {
                    SampleBackgroundColors();
                }
            }
        }

        #endregion

        #region Right Panel - Editing

        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical();
            _rightPanelScroll = EditorGUILayout.BeginScrollView(_rightPanelScroll);

            EditorGUILayout.LabelField("Image Editing", EditorStyles.boldLabel);

            if (_editingTexture == null)
            {
                EditorGUILayout.HelpBox("Generate or select an image to start editing.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.BeginChangeCheck();

            // Preview
            DrawPreviewSection();
            EditorGUILayout.Space(10);

            // Tools
            DrawToolsSection();
            EditorGUILayout.Space(10);

            // Background Removal
            DrawBackgroundRemovalSection();
            EditorGUILayout.Space(10);

            // Color Adjustments
            DrawColorAdjustmentsSection();
            EditorGUILayout.Space(10);

            // Auto Crop
            DrawAutoCropSection();
            EditorGUILayout.Space(10);

            // Actions
            DrawActionsSection();

            if (EditorGUI.EndChangeCheck())
            {
                _needsPreviewUpdate = true;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            // Show _editingTexture directly while painting for immediate feedback
            // Show _previewTexture when not painting (with processed effects)
            var previewTex = _isPainting ? _editingTexture : (_previewTexture ?? _editingTexture);
            if (previewTex != null)
            {
                var maxSize = Mathf.Min(position.width * 2f / 3f - 40f, 400f);
                var aspect = (float)previewTex.height / previewTex.width;
                var previewWidth = Mathf.Min(maxSize, previewTex.width);
                var previewHeight = previewWidth * aspect;

                var rect = GUILayoutUtility.GetRect(previewWidth, previewHeight);

                // Calculate actual texture rect within the allocated rect (accounting for aspect ratio)
                var textureRect = CalculateScaledTextureRect(rect, previewTex);

                // Draw checkerboard background
                DrawCheckerboard(textureRect);

                // Draw preview - use StretchToFill since we calculated the exact rect
                GUI.DrawTexture(textureRect, previewTex, ScaleMode.StretchToFill);

                // Handle painting with the actual texture rect
                HandlePainting(textureRect);
            }
        }

        /// <summary>
        /// Calculates the actual rect where the texture will be drawn with ScaleToFit behavior.
        /// </summary>
        private Rect CalculateScaledTextureRect(Rect containerRect, Texture2D texture)
        {
            var texAspect = (float)texture.width / texture.height;
            var rectAspect = containerRect.width / containerRect.height;

            float width, height, x, y;

            if (texAspect > rectAspect)
            {
                // Texture is wider - fit to width
                width = containerRect.width;
                height = width / texAspect;
                x = containerRect.x;
                y = containerRect.y + (containerRect.height - height) / 2f;
            }
            else
            {
                // Texture is taller - fit to height
                height = containerRect.height;
                width = height * texAspect;
                x = containerRect.x + (containerRect.width - width) / 2f;
                y = containerRect.y;
            }

            return new Rect(x, y, width, height);
        }

        private void DrawToolsSection()
        {
            EditorGUILayout.LabelField("Drawing Tools", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            var noneStyle = _currentTool == ToolMode.None ? "Button" : "Button";
            var brushStyle = _currentTool == ToolMode.Brush ? "Button" : "Button";
            var eraserStyle = _currentTool == ToolMode.Eraser ? "Button" : "Button";

            GUI.backgroundColor = _currentTool == ToolMode.None ? Color.cyan : Color.white;
            if (GUILayout.Button("Select", GUILayout.Height(25)))
            {
                _currentTool = ToolMode.None;
            }

            GUI.backgroundColor = _currentTool == ToolMode.Brush ? Color.cyan : Color.white;
            if (GUILayout.Button("Brush (Paint)", GUILayout.Height(25)))
            {
                _currentTool = ToolMode.Brush;
            }

            GUI.backgroundColor = _currentTool == ToolMode.Eraser ? Color.cyan : Color.white;
            if (GUILayout.Button("Eraser", GUILayout.Height(25)))
            {
                _currentTool = ToolMode.Eraser;
            }

            GUI.backgroundColor = _currentTool == ToolMode.Restore ? Color.green : Color.white;
            if (GUILayout.Button("Restore", GUILayout.Height(25)))
            {
                _currentTool = ToolMode.Restore;
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (_currentTool == ToolMode.Restore)
            {
                EditorGUILayout.HelpBox("Tô lên vùng bị xóa nhầm để khôi phục màu gốc.", MessageType.Info);
            }

            if (_currentTool == ToolMode.Brush)
            {
                _brushColor = EditorGUILayout.ColorField("Brush Color", _brushColor);
            }

            if (_currentTool != ToolMode.None)
            {
                _brushSize = EditorGUILayout.IntSlider("Brush Size", _brushSize, 1, 50);

                if (GUILayout.Button("Regenerate with Edits"))
                {
                    // TODO: Implement inpainting regeneration
                    EditorUtility.DisplayDialog("Info", "Inpainting regeneration coming soon!", "OK");
                }
            }
        }

        private void DrawBackgroundRemovalSection()
        {
            EditorGUILayout.LabelField("Background Removal", EditorStyles.boldLabel);

            _enableBackgroundRemoval = EditorGUILayout.Toggle("Enable Background Removal", _enableBackgroundRemoval);

            using (new EditorGUI.DisabledScope(!_enableBackgroundRemoval))
            {
                _autoSampleBackground = EditorGUILayout.Toggle("Auto Sample Background", _autoSampleBackground);

            if (GUILayout.Button("Sample Background Colors"))
            {
                SampleBackgroundColors();
                _needsPreviewUpdate = true;
            }

            _backgroundColor = EditorGUILayout.ColorField("Background Color", _backgroundColor);

            _hueTolerance = EditorGUILayout.Slider("Hue Tolerance", _hueTolerance, 0f, 0.5f);
            _saturationTolerance = EditorGUILayout.Slider("Saturation Tolerance", _saturationTolerance, 0f, 1f);
            _valueTolerance = EditorGUILayout.Slider("Value Tolerance", _valueTolerance, 0f, 1f);

            if (_sampledColors.Count > 0)
            {
                EditorGUILayout.LabelField($"Sampled {_sampledColors.Count} background colors");
            }
            } // End disabled scope
        }

        private void DrawColorAdjustmentsSection()
        {
            EditorGUILayout.LabelField("Color Adjustments", EditorStyles.boldLabel);

            _hueShift = EditorGUILayout.Slider("Hue Shift (Chuyển màu)", _hueShift, -0.5f, 0.5f);
            _saturationAdjust = EditorGUILayout.Slider("Saturation (Độ tươi)", _saturationAdjust, -1f, 1f);
            _brightnessAdjust = EditorGUILayout.Slider("Brightness (Độ sáng)", _brightnessAdjust, -1f, 1f);

            if (GUILayout.Button("Reset Adjustments"))
            {
                _hueShift = 0f;
                _saturationAdjust = 0f;
                _brightnessAdjust = 0f;
                _needsPreviewUpdate = true;
            }
        }

        private void DrawAutoCropSection()
        {
            EditorGUILayout.LabelField("Auto Crop", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Crop image to fit content with 1px transparent border.", MessageType.Info);

            if (GUILayout.Button("Auto Crop to Alpha"))
            {
                AutoCropToAlpha();
            }
        }

        private void DrawActionsSection()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Undo All Edits", GUILayout.Height(30)))
            {
                if (_originalTexture != null)
                {
                    Graphics.CopyTexture(_originalTexture, _editingTexture);
                    _editingTexture.Apply();
                    _hueShift = 0f;
                    _saturationAdjust = 0f;
                    _brightnessAdjust = 0f;
                    
                    // Reset protection mask
                    if (_protectionMask != null)
                    {
                        var clearPixels = new Color[_protectionMask.width * _protectionMask.height];
                        for (int i = 0; i < clearPixels.Length; i++) clearPixels[i] = Color.black;
                        _protectionMask.SetPixels(clearPixels);
                        _protectionMask.Apply();
                    }
                    
                    _needsPreviewUpdate = true;
                }
            }

            if (GUILayout.Button("Apply to Image", GUILayout.Height(30)))
            {
                ApplyToTargetImage();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Save as PNG", GUILayout.Height(30)))
            {
                SaveAsPng();
            }
        }

        private void HandlePainting(Rect previewRect)
        {
            if (_currentTool == ToolMode.None) return;
            if (_editingTexture == null) return;

            var e = Event.current;
            if (!previewRect.Contains(e.mousePosition)) return;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                _isPainting = true;
                _lastPaintPos = GetTextureCoordinate(e.mousePosition, previewRect);
                PaintAt(_lastPaintPos);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isPainting)
            {
                var currentPos = GetTextureCoordinate(e.mousePosition, previewRect);
                PaintLine(_lastPaintPos, currentPos);
                _lastPaintPos = currentPos;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _isPainting)
            {
                _isPainting = false;
                _needsPreviewUpdate = true;
                e.Use();
            }

            if (_currentTool != ToolMode.None)
            {
                EditorGUIUtility.AddCursorRect(previewRect, MouseCursor.Arrow);
            }
        }

        private Vector2 GetTextureCoordinate(Vector2 mousePos, Rect previewRect)
        {
            // Always use _editingTexture dimensions since that's what we paint on
            if (_editingTexture == null) return Vector2.zero;
            
            var normalizedX = (mousePos.x - previewRect.x) / previewRect.width;
            var normalizedY = 1f - (mousePos.y - previewRect.y) / previewRect.height;

            return new Vector2(
                Mathf.Clamp(normalizedX * _editingTexture.width, 0, _editingTexture.width - 1),
                Mathf.Clamp(normalizedY * _editingTexture.height, 0, _editingTexture.height - 1)
            );
        }

        private void PaintAt(Vector2 pos)
        {
            if (_editingTexture == null) return;

            var x = Mathf.RoundToInt(pos.x);
            var y = Mathf.RoundToInt(pos.y);
            var radius = _brushSize / 2;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        var px = x + dx;
                        var py = y + dy;

                        if (px >= 0 && px < _editingTexture.width && py >= 0 && py < _editingTexture.height)
                        {
                            if (_currentTool == ToolMode.Brush)
                            {
                                _editingTexture.SetPixel(px, py, _brushColor);
                                // Mark as protected so BG removal doesn't affect it
                                if (_protectionMask != null)
                                    _protectionMask.SetPixel(px, py, Color.white);
                            }
                            else if (_currentTool == ToolMode.Eraser)
                            {
                                _editingTexture.SetPixel(px, py, Color.clear);
                                // Mark as protected (user explicitly erased)
                                if (_protectionMask != null)
                                    _protectionMask.SetPixel(px, py, Color.white);
                            }
                            else if (_currentTool == ToolMode.Restore && _originalTexture != null)
                            {
                                // Restore original pixel color from before any edits
                                var originalColor = _originalTexture.GetPixel(px, py);
                                _editingTexture.SetPixel(px, py, originalColor);
                                // Mark as protected so BG removal won't remove it again
                                if (_protectionMask != null)
                                    _protectionMask.SetPixel(px, py, Color.white);
                            }
                        }
                    }
                }
            }

            _editingTexture.Apply();
            if (_protectionMask != null)
                _protectionMask.Apply();
            Repaint();
        }

        private void PaintLine(Vector2 from, Vector2 to)
        {
            var distance = Vector2.Distance(from, to);
            var steps = Mathf.Max(1, Mathf.CeilToInt(distance / (_brushSize / 2f)));

            for (int i = 0; i <= steps; i++)
            {
                var t = (float)i / steps;
                var pos = Vector2.Lerp(from, to, t);
                PaintAt(pos);
            }
        }

        #endregion

        #region Processing

        private void SampleBackgroundColors()
        {
            if (_editingTexture == null) return;

            _sampledColors.Clear();
            var width = _editingTexture.width;
            var height = _editingTexture.height;

            // Sample from corners
            _sampledColors.Add(_editingTexture.GetPixel(0, height - 1));
            _sampledColors.Add(_editingTexture.GetPixel(width - 1, height - 1));
            _sampledColors.Add(_editingTexture.GetPixel(0, 0));
            _sampledColors.Add(_editingTexture.GetPixel(width - 1, 0));

            // Sample from edges
            var edgeSamples = 5;
            for (int i = 0; i < edgeSamples; i++)
            {
                var t = (float)i / (edgeSamples - 1);
                var x = Mathf.FloorToInt(t * (width - 1));
                var y = Mathf.FloorToInt(t * (height - 1));

                _sampledColors.Add(_editingTexture.GetPixel(x, height - 1));
                _sampledColors.Add(_editingTexture.GetPixel(x, 0));
                _sampledColors.Add(_editingTexture.GetPixel(0, y));
                _sampledColors.Add(_editingTexture.GetPixel(width - 1, y));
            }

            // Calculate average and set as background color
            var avgColor = Color.black;
            foreach (var c in _sampledColors)
            {
                avgColor += c;
            }
            avgColor /= _sampledColors.Count;
            _backgroundColor = avgColor;
            _backgroundColor.a = 1f;

            // Calculate average HSV
            UpdateAverageBackgroundHsv();
        }

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

        private void UpdatePreview()
        {
            if (_editingTexture == null) return;

            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
            }

            _previewTexture = ProcessTexture(_editingTexture);
            Repaint();
        }

        private Texture2D ProcessTexture(Texture2D source)
        {
            var width = source.width;
            var height = source.height;
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var sourcePixels = source.GetPixels();
            var maskPixels = _protectionMask != null ? _protectionMask.GetPixels() : null;
            var resultPixels = new Color[sourcePixels.Length];

            for (var i = 0; i < sourcePixels.Length; i++)
            {
                var pixel = sourcePixels[i];
                
                // Check if pixel is protected (manually edited by user)
                var isProtected = maskPixels != null && maskPixels[i].r > 0.5f;

                // Only remove background if enabled AND pixel is not protected
                if (_enableBackgroundRemoval && !isProtected && IsBackgroundColor(pixel))
                {
                    resultPixels[i] = Color.clear;
                }
                else
                {
                    resultPixels[i] = ApplyColorAdjustments(pixel);
                }
            }

            result.SetPixels(resultPixels);
            result.Apply();
            return result;
        }

        private bool IsBackgroundColor(Color pixel)
        {
            if (pixel.a < 0.1f) return false; // Already transparent

            Color.RGBToHSV(pixel, out var h, out var s, out var v);

            var hueDiff = Mathf.Abs(h - _avgBackgroundHsv.x);
            if (hueDiff > 0.5f) hueDiff = 1f - hueDiff;

            var satDiff = Mathf.Abs(s - _avgBackgroundHsv.y);
            var valDiff = Mathf.Abs(v - _avgBackgroundHsv.z);

            if (hueDiff <= _hueTolerance && satDiff <= _saturationTolerance && valDiff <= _valueTolerance)
            {
                return true;
            }

            // Check against sampled colors
            foreach (var sampledColor in _sampledColors)
            {
                Color.RGBToHSV(sampledColor, out var sh, out var ss, out var sv);
                hueDiff = Mathf.Abs(h - sh);
                if (hueDiff > 0.5f) hueDiff = 1f - hueDiff;

                if (hueDiff <= _hueTolerance &&
                    Mathf.Abs(s - ss) <= _saturationTolerance &&
                    Mathf.Abs(v - sv) <= _valueTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private Color ApplyColorAdjustments(Color color)
        {
            if (Mathf.Abs(_hueShift) < 0.001f && Mathf.Abs(_saturationAdjust) < 0.001f && Mathf.Abs(_brightnessAdjust) < 0.001f)
            {
                return color;
            }

            Color.RGBToHSV(color, out var h, out var s, out var v);

            h = (h + _hueShift + 1f) % 1f;
            s = Mathf.Clamp01(s + _saturationAdjust);
            v = Mathf.Clamp01(v + _brightnessAdjust);

            var result = Color.HSVToRGB(h, s, v);
            result.a = color.a;
            return result;
        }

        private void AutoCropToAlpha()
        {
            if (_previewTexture == null && _editingTexture == null) return;

            var source = _previewTexture ?? _editingTexture;
            var pixels = source.GetPixels();
            var width = source.width;
            var height = source.height;

            // Find bounds
            int minX = width, maxX = 0, minY = height, maxY = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var pixel = pixels[y * width + x];
                    if (pixel.a > 0.01f)
                    {
                        minX = Mathf.Min(minX, x);
                        maxX = Mathf.Max(maxX, x);
                        minY = Mathf.Min(minY, y);
                        maxY = Mathf.Max(maxY, y);
                    }
                }
            }

            if (minX > maxX || minY > maxY)
            {
                EditorUtility.DisplayDialog("Auto Crop", "No visible content found.", "OK");
                return;
            }

            // Add 1px border
            minX = Mathf.Max(0, minX - 1);
            minY = Mathf.Max(0, minY - 1);
            maxX = Mathf.Min(width - 1, maxX + 1);
            maxY = Mathf.Min(height - 1, maxY + 1);

            var newWidth = maxX - minX + 1;
            var newHeight = maxY - minY + 1;

            var cropped = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            var croppedPixels = new Color[newWidth * newHeight];

            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    croppedPixels[y * newWidth + x] = pixels[(minY + y) * width + (minX + x)];
                }
            }

            cropped.SetPixels(croppedPixels);
            cropped.Apply();

            // Replace editing texture
            DestroyImmediate(_editingTexture);
            _editingTexture = cropped;

            if (_previewTexture != null)
            {
                DestroyImmediate(_previewTexture);
                _previewTexture = null;
            }

            _needsPreviewUpdate = true;

            EditorUtility.DisplayDialog("Auto Crop", $"Cropped from {width}x{height} to {newWidth}x{newHeight}", "OK");
        }

        private void ApplyToTargetImage()
        {
            if (_targetImage == null)
            {
                EditorUtility.DisplayDialog("Error", "No target Image component selected.", "OK");
                return;
            }

            var texToApply = _previewTexture ?? _editingTexture;
            if (texToApply == null)
            {
                EditorUtility.DisplayDialog("Error", "No image to apply.", "OK");
                return;
            }

            // Save texture to project
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Generated Image",
                "GeneratedUI",
                "png",
                "Save the generated image as PNG");

            if (string.IsNullOrEmpty(path)) return;

            SaveTextureAsPng(texToApply, path);

            // Load as sprite and assign
            AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                Undo.RecordObject(_targetImage, "Apply Generated UI");
                _targetImage.sprite = sprite;
                EditorUtility.SetDirty(_targetImage);
            }

            EditorUtility.DisplayDialog("Success", $"Applied to {_targetImage.gameObject.name}", "OK");
        }

        private void SaveAsPng()
        {
            var texToSave = _previewTexture ?? _editingTexture;
            if (texToSave == null)
            {
                EditorUtility.DisplayDialog("Error", "No image to save.", "OK");
                return;
            }

            var path = EditorUtility.SaveFilePanelInProject(
                "Save as PNG",
                "GeneratedImage",
                "png",
                "Save the image as PNG");

            if (string.IsNullOrEmpty(path)) return;

            SaveTextureAsPng(texToSave, path);

            AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            EditorUtility.DisplayDialog("Success", $"Saved to {path}", "OK");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private void SaveTextureAsPng(Texture2D texture, string path)
        {
            var bytes = texture.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
        }

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
                        Mathf.Min(checkerSize, rect.width - x * checkerSize),
                        Mathf.Min(checkerSize, rect.height - y * checkerSize)
                    ), isLight ? light : dark);
                }
            }
        }

        #endregion
    }
}
