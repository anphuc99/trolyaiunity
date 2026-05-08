using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Core.Infrastructure.Authentication;
using Core.Infrastructure.Network;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Features.GamePlay.SubFeatures.Chat.Infrastructure
{
	/// <summary>
	/// Response payload from GET /api/reference-audio.
	/// </summary>
	public sealed class GptSoVitsReferenceAudioPayload
	{
		/// <summary>
		/// WAV file name (e.g. audio_Kore_xxx.wav).
		/// </summary>
		[JsonProperty("file")]
		public string File { get; set; }

		/// <summary>
		/// Reference text in Chinese (prompt_text for GPT-SoVITS).
		/// </summary>
		[JsonProperty("text")]
		public string Text { get; set; }

		/// <summary>
		/// Emotion of the reference audio.
		/// </summary>
		[JsonProperty("emotion")]
		public string Emotion { get; set; }

		/// <summary>
		/// Intensity of the reference audio.
		/// </summary>
		[JsonProperty("intensity")]
		public string Intensity { get; set; }

		/// <summary>
		/// Server-relative download URL (e.g. /ref-files/Kore/audio_Kore_xxx.wav).
		/// </summary>
		[JsonProperty("downloadUrl")]
		public string DownloadUrl { get; set; }
	}

	/// <summary>
	/// Wrapper returned by /api/reference-audio endpoint.
	/// </summary>
	public sealed class GptSoVitsReferenceAudioResponse
	{
		[JsonProperty("success")]
		public bool Success { get; set; }

		[JsonProperty("reference")]
		public GptSoVitsReferenceAudioPayload Reference { get; set; }
	}

	/// <summary>
	/// Response payload from POST /api/process-tts-wav.
	/// </summary>
	public sealed class GptSoVitsProcessWavResponsePayload
	{
		/// <summary>
		/// Audio ID on server.
		/// </summary>
		[JsonProperty("audioId")]
		public string AudioId { get; set; }

		/// <summary>
		/// Relative URL to the processed MP3 file (e.g. /audio/xxx.mp3).
		/// </summary>
		[JsonProperty("url")]
		public string Url { get; set; }
	}

	/// <summary>
	/// Client-side service that handles the full GPT-SoVITS TTS pipeline on PC.
	/// Flow:
	///   1. GET /api/reference-audio → get best reference WAV + refText
	///   2. Cache reference WAV locally under %AppData%/Mimichat/ref_cache/
	///   3. POST http://127.0.0.1:9880/tts → local GPT-SoVITS → raw WAV bytes
	///   4. POST /api/process-tts-wav → pitch shift + trim silence + convert MP3
	///   5. Return absolute MP3 URL ready for playback.
	/// </summary>
	public static class GptSoVitsTtsService
	{
		/// <summary>
		/// Local GPT-SoVITS API base URL.
		/// </summary>
		public const string GptSoVitsBaseUrl = "http://127.0.0.1:9872";

		/// <summary>
		/// Local reference WAV cache directory (rooted under Application.persistentDataPath).
		/// </summary>
		private const string RefCacheDirName = "ref_cache";

		/// <summary>
		/// Base path to the local GPT-SoVITS installation (for finding model weights).
		/// </summary>
		private const string DefaultGptSoVitsInstallPath = @"C:\Users\tolit\Downloads\GPT-SoVITS-v2pro-20250604\GPT-SoVITS-v2pro-20250604";

		private static string _currentVoiceName = null;
		private const string LogPrefix = "[GptSoVitsTtsService]";

		/// <summary>
		/// Executes the full GPT-SoVITS TTS pipeline and returns the final MP3 URL.
		/// Returns null on any failure; errors are logged but not thrown.
		/// </summary>
		/// <param name="text">Chinese text to synthesize.</param>
		/// <param name="emotion">Emotion label (e.g. happy, neutral).</param>
		/// <param name="intensity">Intensity label: low | medium | high.</param>
		/// <param name="voiceName">GPT-SoVITS voice/dataset name (e.g. Kore, Leda).</param>
		/// <param name="tone">Tone hint forwarded to server for pitch resolution.</param>
		/// <param name="characterName">Character display name forwarded to server.</param>
		/// <param name="messageId">Optional message ID forwarded to server.</param>
		/// <param name="serverBaseUrl">Normalized remote server base URL (no trailing slash or /api).</param>
		/// <returns>Absolute MP3 URL, or null on failure.</returns>
		public static async Task<string> SynthesizeAsync(
			string text,
			string emotion,
			string intensity,
			string voiceName,
			string tone,
			string characterName,
			string messageId,
			string serverBaseUrl)
		{
							// ── Step 1: Get reference audio info from server ───────────────────────
				var refInfo = await FetchReferenceAudioAsync(text, emotion, intensity, voiceName, serverBaseUrl);
				if (refInfo == null)
				{
					Debug.LogWarning(LogPrefix + " Failed to fetch reference audio info from server.");
					return null;
				}

				// ── Step 2: Download + cache reference WAV locally ─────────────────────
				var localRefPath = await GetOrDownloadRefWavAsync(refInfo, serverBaseUrl);
				if (string.IsNullOrEmpty(localRefPath) || !System.IO.File.Exists(localRefPath))
				{
					Debug.LogWarning(LogPrefix + " Reference WAV not available locally.");
					return null;
				}

				// ── Step 2.5: Ensure correct models are loaded for the voice ───────────
				await EnsureModelsLoadedAsync(voiceName);

				// ── Step 3: Call local GPT-SoVITS to generate raw WAV ──────────────────
				var rawWavBytes = await CallGptSoVitsAsync(text, localRefPath, refInfo.Text);
				if (rawWavBytes == null || rawWavBytes.Length == 0)
				{
					Debug.LogWarning(LogPrefix + " GPT-SoVITS returned empty WAV output.");
					return null;
				}

				// ── Step 4: Upload WAV to server for pitch-shift + trim + MP3 convert ──
				var processResult = await UploadWavToServerAsync(
					rawWavBytes, text, tone, characterName, messageId, serverBaseUrl);
				if (processResult == null || string.IsNullOrWhiteSpace(processResult.Url))
				{
					Debug.LogWarning(LogPrefix + " Server failed to process WAV into MP3.");
					return null;
				}

				// ── Step 5: Resolve absolute MP3 URL ──────────────────────────────────
				var mp3Url = ResolveAbsoluteUrl(processResult.Url, serverBaseUrl);
				Debug.Log(LogPrefix + " TTS pipeline complete. MP3 URL: " + mp3Url);
				return mp3Url;
		}

		// ──────────────────────────────────────────────────────────────────────────
		// Step 1: Fetch reference audio metadata from server
		// ──────────────────────────────────────────────────────────────────────────

		private static async Task<GptSoVitsReferenceAudioPayload> FetchReferenceAudioAsync(
			string text, string emotion, string intensity, string voiceName, string serverBaseUrl)
		{
			var safeEmotion = string.IsNullOrWhiteSpace(emotion) ? "neutral" : emotion.Trim().ToLowerInvariant();
			var safeIntensity = string.IsNullOrWhiteSpace(intensity) ? "medium" : intensity.Trim().ToLowerInvariant();
			var safeVoiceName = string.IsNullOrWhiteSpace(voiceName) ? string.Empty : voiceName.Trim();

			var query = "?text=" + Uri.EscapeDataString(text ?? string.Empty)
				+ "&emotion=" + Uri.EscapeDataString(safeEmotion)
				+ "&intensity=" + Uri.EscapeDataString(safeIntensity)
				+ "&voiceName=" + Uri.EscapeDataString(safeVoiceName);

			var url = serverBaseUrl.TrimEnd('/') + NetworkEndpoints.ReferenceAudio + query;

			using (var request = UnityWebRequest.Get(url))
			{
				// Forward auth token if HttpClient has a cached auth header helper; use raw header here.
				var authToken = AuthTokenModel.AccessToken;
				if (!string.IsNullOrEmpty(authToken))
				{
					request.SetRequestHeader("Authorization", "Bearer " + authToken);
				}

				request.timeout = 15;
				var op = request.SendWebRequest();
				while (!op.isDone)
				{
					await Task.Yield();
				}

				if (request.result != UnityWebRequest.Result.Success)
				{
					Debug.LogWarning(LogPrefix + " Reference audio request failed: " + request.error);
					return null;
				}

				var responseText = request.downloadHandler.text;
				if (string.IsNullOrWhiteSpace(responseText))
				{
					return null;
				}

				var wrapper = JsonConvert.DeserializeObject<GptSoVitsReferenceAudioResponse>(responseText);
				if (wrapper == null || !wrapper.Success || wrapper.Reference == null)
				{
					Debug.LogWarning(LogPrefix + " Server returned failure for reference audio.");
					return null;
				}

				return wrapper.Reference;
			}
		}

		// ──────────────────────────────────────────────────────────────────────────
		// Step 2: Get or download reference WAV to local cache
		// ──────────────────────────────────────────────────────────────────────────

		private static async Task<string> GetOrDownloadRefWavAsync(
			GptSoVitsReferenceAudioPayload refInfo, string serverBaseUrl)
		{
			var cacheDir = Path.Combine(Application.persistentDataPath, RefCacheDirName);
			Directory.CreateDirectory(cacheDir);

			var safeFileName = SanitizeFileName(refInfo.File ?? "ref.wav");
			var localPath = Path.Combine(cacheDir, safeFileName);

			if (System.IO.File.Exists(localPath))
			{
				Debug.Log(LogPrefix + " Using cached reference WAV: " + localPath);
				return localPath;
			}

			// Download from server
			var downloadUrl = ResolveAbsoluteUrl(refInfo.DownloadUrl, serverBaseUrl);
			if (string.IsNullOrWhiteSpace(downloadUrl))
			{
				Debug.LogWarning(LogPrefix + " Invalid download URL for reference WAV.");
				return null;
			}

			using (var request = UnityWebRequest.Get(downloadUrl))
			{
				var authToken = AuthTokenModel.AccessToken;
				if (!string.IsNullOrEmpty(authToken))
				{
					request.SetRequestHeader("Authorization", "Bearer " + authToken);
				}

				request.timeout = 30;
				var op = request.SendWebRequest();
				while (!op.isDone)
				{
					await Task.Yield();
				}

				if (request.result != UnityWebRequest.Result.Success)
				{
					Debug.LogWarning(LogPrefix + " Failed to download reference WAV: " + request.error);
					return null;
				}

				var wavBytes = request.downloadHandler.data;
				if (wavBytes == null || wavBytes.Length == 0)
				{
					Debug.LogWarning(LogPrefix + " Downloaded reference WAV is empty.");
					return null;
				}

				System.IO.File.WriteAllBytes(localPath, wavBytes);
				Debug.Log(LogPrefix + " Reference WAV cached at: " + localPath);
				return localPath;
			}
		}

		// ──────────────────────────────────────────────────────────────────────────
		// Step 3: Call local GPT-SoVITS API to generate WAV
		// ──────────────────────────────────────────────────────────────────────────

		private static async Task<bool> EnsureModelsLoadedAsync(string voiceName)
		{
			if (string.IsNullOrWhiteSpace(voiceName)) return true;
			if (string.Equals(_currentVoiceName, voiceName, StringComparison.OrdinalIgnoreCase)) return true;

			try
			{
				var gptWeightsDir = Path.Combine(DefaultGptSoVitsInstallPath, "GPT_weights_v2Pro");
				var sovitsWeightsDir = Path.Combine(DefaultGptSoVitsInstallPath, "SoVITS_weights_v2Pro");

				if (!Directory.Exists(gptWeightsDir) || !Directory.Exists(sovitsWeightsDir))
				{
					Debug.LogWarning(LogPrefix + " Model directories not found. Using previously loaded models.");
					return true;
				}

				var gptFiles = Directory.GetFiles(gptWeightsDir, voiceName + "-*.ckpt");
				var selectedGpt = gptFiles.OrderByDescending(f =>
				{
					var match = Regex.Match(Path.GetFileName(f), @"-e(\d+)\.ckpt$");
					return match.Success ? int.Parse(match.Groups[1].Value) : 0;
				}).FirstOrDefault();

				var sovitsFiles = Directory.GetFiles(sovitsWeightsDir, voiceName + "_*.pth");
				var selectedSovits = sovitsFiles.OrderByDescending(f =>
				{
					var match = Regex.Match(Path.GetFileName(f), @"_e(\d+)_s(\d+)\.pth$");
					return match.Success ? int.Parse(match.Groups[2].Value) : 0;
				}).FirstOrDefault();

				if (selectedGpt != null)
				{
					var url = GptSoVitsBaseUrl + "/set_gpt_weights?weights_path=" + Uri.EscapeDataString(selectedGpt.Replace("\\", "/"));
					using (var req = UnityWebRequest.Get(url))
					{
						var op = req.SendWebRequest();
						while (!op.isDone) await Task.Yield();
						if (req.result != UnityWebRequest.Result.Success)
							Debug.LogWarning(LogPrefix + " Failed to set GPT weights: " + req.error);
						else
							Debug.Log(LogPrefix + " Successfully set GPT weights: " + Path.GetFileName(selectedGpt));
					}
				}

				if (selectedSovits != null)
				{
					var url = GptSoVitsBaseUrl + "/set_sovits_weights?weights_path=" + Uri.EscapeDataString(selectedSovits.Replace("\\", "/"));
					using (var req = UnityWebRequest.Get(url))
					{
						var op = req.SendWebRequest();
						while (!op.isDone) await Task.Yield();
						if (req.result != UnityWebRequest.Result.Success)
							Debug.LogWarning(LogPrefix + " Failed to set SoVITS weights: " + req.error);
						else
							Debug.Log(LogPrefix + " Successfully set SoVITS weights: " + Path.GetFileName(selectedSovits));
					}
				}

				_currentVoiceName = voiceName;
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning(LogPrefix + " Error updating models: " + ex.Message);
				return false;
			}
		}

		private static async Task<byte[]> CallGptSoVitsAsync(
			string text, string refAudioPath, string refText)
		{
			var url = GptSoVitsBaseUrl + "/tts";

			var requestBody = new
			{
				text = text ?? string.Empty,
				text_lang = "zh",
				ref_audio_path = refAudioPath,
				prompt_text = refText ?? string.Empty,
				prompt_lang = "zh",
				speed_factor = 0.8f
			};

			var jsonBody = JsonConvert.SerializeObject(requestBody);
			var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

			using (var request = new UnityWebRequest(url, "POST"))
			{
				request.uploadHandler = new UploadHandlerRaw(bodyBytes);
				request.downloadHandler = new DownloadHandlerBuffer();
				request.SetRequestHeader("Content-Type", "application/json");
				request.timeout = 120;

				var op = request.SendWebRequest();
				while (!op.isDone)
				{
					await Task.Yield();
				}

				if (request.result != UnityWebRequest.Result.Success)
				{
					Debug.LogWarning(LogPrefix + " GPT-SoVITS call failed: " + request.error
						+ ". Make sure GPT-SoVITS is running at " + GptSoVitsBaseUrl);
					return null;
				}

				var wavData = request.downloadHandler.data;
				Debug.Log(LogPrefix + " GPT-SoVITS returned " + (wavData?.Length ?? 0) + " bytes of WAV.");
				return wavData;
			}
		}

		// ──────────────────────────────────────────────────────────────────────────
		// Step 4: Upload WAV to server for pitch-shift + trim + MP3 convert
		// ──────────────────────────────────────────────────────────────────────────

		private static async Task<GptSoVitsProcessWavResponsePayload> UploadWavToServerAsync(
			byte[] wavBytes, string text, string tone, string characterName, string messageId, string serverBaseUrl)
		{
			var url = serverBaseUrl.TrimEnd('/') + NetworkEndpoints.ProcessTtsWav;

			var form = new WWWForm();
			form.AddBinaryData("audio", wavBytes, "tts_output.wav", "audio/wav");
			form.AddField("text", text ?? string.Empty);
			form.AddField("tone", string.IsNullOrWhiteSpace(tone) ? "neutral" : tone.Trim());
			form.AddField("characterName", string.IsNullOrWhiteSpace(characterName) ? string.Empty : characterName.Trim());
			if (!string.IsNullOrWhiteSpace(messageId))
			{
				form.AddField("messageId", messageId.Trim());
			}

			using (var request = UnityWebRequest.Post(url, form))
			{
				var authToken = AuthTokenModel.AccessToken;
				if (!string.IsNullOrEmpty(authToken))
				{
					request.SetRequestHeader("Authorization", "Bearer " + authToken);
				}

				request.timeout = 60;
				var op = request.SendWebRequest();
				while (!op.isDone)
				{
					await Task.Yield();
				}

				if (request.result != UnityWebRequest.Result.Success)
				{
					Debug.LogWarning(LogPrefix + " WAV upload/process failed: " + request.error);
					return null;
				}

				var responseText = request.downloadHandler.text;
				if (string.IsNullOrWhiteSpace(responseText))
				{
					return null;
				}

				try
				{
					return JsonConvert.DeserializeObject<GptSoVitsProcessWavResponsePayload>(responseText);
				}
				catch (Exception ex)
				{
					Debug.LogWarning(LogPrefix + " Failed to parse process-tts-wav response: " + ex.Message);
					return null;
				}
			}
		}

		// ──────────────────────────────────────────────────────────────────────────
		// Helpers
		// ──────────────────────────────────────────────────────────────────────────

		private static string ResolveAbsoluteUrl(string relativeOrAbsoluteUrl, string serverBaseUrl)
		{
			if (string.IsNullOrWhiteSpace(relativeOrAbsoluteUrl))
			{
				return null;
			}

			if (relativeOrAbsoluteUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
				|| relativeOrAbsoluteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				return relativeOrAbsoluteUrl;
			}

			var baseUrl = (serverBaseUrl ?? string.Empty).TrimEnd('/');
			var path = relativeOrAbsoluteUrl.StartsWith("/") ? relativeOrAbsoluteUrl : "/" + relativeOrAbsoluteUrl;
			return baseUrl + path;
		}

		private static string SanitizeFileName(string fileName)
		{
			foreach (var c in Path.GetInvalidFileNameChars())
			{
				fileName = fileName.Replace(c, '_');
			}

			return fileName;
		}
	}
}
