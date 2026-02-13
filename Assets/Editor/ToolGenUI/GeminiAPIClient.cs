using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEditor;

namespace EditorTools.UIGenerator
{
    /// <summary>
    /// Client for communicating with Google Gemini API for image generation.
    /// Supports gemini-2.0-flash-preview-image and imagen-3.0-generate-002 models.
    /// </summary>
    public static class GeminiAPIClient
    {
        private const string ApiEndpointTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
        private const string PrefsKeyApiKey = "GeminiAPIKey";
        private const string PrefsKeyModel = "GeminiModel";

        public static string[] AvailableModels = new[]
        {
            "gemini-2.5-flash-image",
            "gemini-3-pro-image-preview"
        };

        /// <summary>
        /// Gets or sets the API key stored in EditorPrefs.
        /// </summary>
        public static string ApiKey
        {
            get => EditorPrefs.GetString(PrefsKeyApiKey, "");
            set => EditorPrefs.SetString(PrefsKeyApiKey, value);
        }

        /// <summary>
        /// Gets or sets the selected model index.
        /// </summary>
        public static int SelectedModelIndex
        {
            get => EditorPrefs.GetInt(PrefsKeyModel, 0);
            set => EditorPrefs.SetInt(PrefsKeyModel, value);
        }

        /// <summary>
        /// Gets the currently selected model name.
        /// </summary>
        public static string SelectedModel => AvailableModels[Mathf.Clamp(SelectedModelIndex, 0, AvailableModels.Length - 1)];

        /// <summary>
        /// Result of an image generation request.
        /// </summary>
        public class GenerationResult
        {
            public bool Success;
            public string Error;
            public List<Texture2D> GeneratedImages = new List<Texture2D>();
        }

        /// <summary>
        /// Generates images using Gemini API.
        /// </summary>
        /// <param name="prompt">The text prompt for generation.</param>
        /// <param name="referenceImage">Optional reference image (background) for context.</param>
        /// <param name="numberOfImages">Number of images to generate (1-4).</param>
        /// <param name="generateUI">True for UI elements, false for backgrounds.</param>
        /// <param name="onComplete">Callback when generation completes.</param>
        public static void GenerateImages(
            string prompt,
            Texture2D referenceImage,
            int numberOfImages,
            bool generateUI,
            Action<GenerationResult> onComplete)
        {
            if (string.IsNullOrEmpty(ApiKey))
            {
                onComplete?.Invoke(new GenerationResult
                {
                    Success = false,
                    Error = "API Key is not set. Please enter your Gemini API key."
                });
                return;
            }

            var result = new GenerationResult();
            var pendingRequests = numberOfImages;

            for (int i = 0; i < numberOfImages; i++)
            {
                GenerateSingleImage(prompt, referenceImage, generateUI, i, (texture, error) =>
                {
                    if (texture != null)
                    {
                        result.GeneratedImages.Add(texture);
                    }
                    else if (!string.IsNullOrEmpty(error) && string.IsNullOrEmpty(result.Error))
                    {
                        result.Error = error;
                    }

                    pendingRequests--;
                    if (pendingRequests <= 0)
                    {
                        result.Success = result.GeneratedImages.Count > 0;
                        onComplete?.Invoke(result);
                    }
                });
            }
        }

        private static void GenerateSingleImage(
            string prompt,
            Texture2D referenceImage,
            bool generateUI,
            int index,
            Action<Texture2D, string> onComplete)
        {
            var url = string.Format(ApiEndpointTemplate, SelectedModel, ApiKey);

            // Build the request body
            var fullPrompt = BuildPrompt(prompt, generateUI, index);
            var requestBody = BuildRequestBody(fullPrompt, referenceImage);

            var request = new UnityWebRequest(url, "POST");
            var bodyRaw = Encoding.UTF8.GetBytes(requestBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            
            EditorApplication.CallbackFunction checkComplete = null;
            checkComplete = () =>
            {
                if (!operation.isDone) return;

                EditorApplication.update -= checkComplete;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Gemini API Error: {request.error}\nResponse: {request.downloadHandler.text}");
                    onComplete?.Invoke(null, $"API Error: {request.error}");
                    request.Dispose();
                    return;
                }

                try
                {
                    var texture = ParseImageFromResponse(request.downloadHandler.text);
                    onComplete?.Invoke(texture, null);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to parse response: {ex.Message}\nResponse: {request.downloadHandler.text}");
                    onComplete?.Invoke(null, $"Parse Error: {ex.Message}");
                }

                request.Dispose();
            };

            EditorApplication.update += checkComplete;
        }

        private static string BuildPrompt(string userPrompt, bool generateUI, int variationIndex)
        {
            var sb = new StringBuilder();

            if (generateUI)
            {
                sb.AppendLine("Generate a game UI element image with transparent background (PNG with alpha).");
                sb.AppendLine("The UI should be clean, modern, and suitable for mobile games.");
                sb.AppendLine("Style: 2D cartoon/anime style, vibrant colors, clear outlines.");
            }
            else
            {
                sb.AppendLine("Generate a game background image.");
                sb.AppendLine("The background should be suitable for mobile games.");
                sb.AppendLine("Style: 2D illustration, atmospheric, detailed.");
            }

            sb.AppendLine();
            sb.AppendLine("User Request:");
            sb.AppendLine(userPrompt);

            if (variationIndex > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Create variation #{variationIndex + 1}, make it slightly different from other variations.");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets a readable copy of the texture. If already readable, returns the original.
        /// Caller must destroy the returned texture if it's different from the input.
        /// </summary>
        private static Texture2D GetReadableTexture(Texture2D source)
        {
            if (source == null) return null;
            
            // Check if texture is already readable
            if (source.isReadable) return source;
            
            // Create a temporary RenderTexture to copy the texture
            var renderTex = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            
            // Copy the source texture to the RenderTexture
            Graphics.Blit(source, renderTex);
            
            // Store the active RenderTexture and set ours
            var previousActive = RenderTexture.active;
            RenderTexture.active = renderTex;
            
            // Create a new readable Texture2D
            var readableTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readableTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readableTexture.Apply();
            
            // Restore previous RenderTexture
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTex);
            
            return readableTexture;
        }

        private static string BuildRequestBody(string prompt, Texture2D referenceImage)
        {
            var parts = new List<string>();

            // Add text prompt
            parts.Add($"{{\"text\": \"{EscapeJson(prompt)}\"}}");

            // Add reference image if provided
            if (referenceImage != null)
            {
                var readableTexture = GetReadableTexture(referenceImage);
                var imageBytes = readableTexture.EncodeToPNG();
                var base64Image = Convert.ToBase64String(imageBytes);
                parts.Add($"{{\"inline_data\": {{\"mime_type\": \"image/png\", \"data\": \"{base64Image}\"}}}}");
                
                // Clean up if we created a copy
                if (readableTexture != referenceImage)
                {
                    UnityEngine.Object.DestroyImmediate(readableTexture);
                }
            }

            var partsJson = string.Join(",", parts);

            // Build generation config for image output
            var generationConfig = @"""generationConfig"": {
                ""responseModalities"": [""TEXT"", ""IMAGE""],
                ""temperature"": 1.0
            }";

            return $"{{\"contents\": [{{\"parts\": [{partsJson}]}}], {generationConfig}}}";
        }

        private static Texture2D ParseImageFromResponse(string jsonResponse)
        {
            // Parse the Gemini response to extract base64 image data
            // Response format: {"candidates":[{"content":{"parts":[{"inlineData":{"mimeType":"image/png","data":"base64..."}}]}}]}

            var dataStart = jsonResponse.IndexOf("\"data\":", StringComparison.Ordinal);
            if (dataStart < 0)
            {
                // Try alternate format
                dataStart = jsonResponse.IndexOf("\"data\" :", StringComparison.Ordinal);
            }

            if (dataStart < 0)
            {
                throw new Exception("No image data found in response");
            }

            dataStart = jsonResponse.IndexOf("\"", dataStart + 7, StringComparison.Ordinal) + 1;
            var dataEnd = jsonResponse.IndexOf("\"", dataStart, StringComparison.Ordinal);

            if (dataStart < 0 || dataEnd < 0)
            {
                throw new Exception("Failed to extract base64 data");
            }

            var base64Data = jsonResponse.Substring(dataStart, dataEnd - dataStart);
            var imageBytes = Convert.FromBase64String(base64Data);

            var texture = new Texture2D(2, 2);
            texture.LoadImage(imageBytes);
            return texture;
        }

        private static string EscapeJson(string text)
        {
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
