using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Features.GamePlay.SubFeatures.Chat.Model;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Features.GamePlay.SubFeatures.Chat.Infrastructure
{
	/// <summary>
	/// Client-side service for communicating with a local Ollama instance.
	/// Used on PC platforms to generate AI replies locally instead of through the cloud.
	/// </summary>
	public static class OllamaService
	{
		/// <summary>
		/// Default Ollama API base URL.
		/// </summary>
		public const string DefaultBaseUrl = "http://localhost:11434";

		/// <summary>
		/// Default model to use for local AI generation.
		/// </summary>
		public const string DefaultModel = "gemma3:4b";

		/// <summary>
		/// Timeout in seconds for Ollama requests. Local generation may take longer.
		/// </summary>
		private const int TimeoutSeconds = 300;

		private const string LogPrefix = "[OllamaService]";

		/// <summary>
		/// Sends a chat request to the local Ollama instance and returns the assistant reply.
		/// </summary>
		/// <param name="systemPrompt">System instruction prompt from server.</param>
		/// <param name="history">Chat history messages.</param>
		/// <param name="userMessage">Current user message to send.</param>
		/// <param name="baseUrl">Ollama API base URL. Defaults to http://localhost:11434.</param>
		/// <param name="model">Model name. Defaults to gemma3:4b.</param>
		/// <returns>Raw assistant content string from Ollama, or null on failure.</returns>
		public static async Task<OllamaChatResponsePayload> SendChatAsync(
			string systemPrompt,
			List<ChatHistoryMessagePayload> history,
			string userMessage,
			string baseUrl = null,
			string model = null)
		{
			var resolvedBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
			var resolvedModel = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
			var url = resolvedBaseUrl + "/api/chat";

			// Build Ollama messages array
			var messages = new List<OllamaChatMessage>();

			// Add system prompt
			if (!string.IsNullOrWhiteSpace(systemPrompt))
			{
				messages.Add(new OllamaChatMessage { Role = "system", Content = systemPrompt });
			}

			// Add history (skip system messages as we already added the prompt)
			if (history != null)
			{
				for (var i = 0; i < history.Count; i++)
				{
					var msg = history[i];
					if (msg == null || string.IsNullOrWhiteSpace(msg.Content))
					{
						continue;
					}

					var role = (msg.Role ?? "").ToLowerInvariant();
					if (role == "system")
					{
						continue;
					}

					if (role != "user" && role != "assistant")
					{
						continue;
					}

					messages.Add(new OllamaChatMessage { Role = role, Content = msg.Content });
				}
			}

			// Add current user message
			if (!string.IsNullOrWhiteSpace(userMessage))
			{
				messages.Add(new OllamaChatMessage { Role = "user", Content = userMessage });
			}

			var requestPayload = new OllamaChatRequestPayload
			{
				Model = resolvedModel,
				Messages = messages,
				Stream = false,
			};

			var jsonBody = JsonConvert.SerializeObject(requestPayload);
			var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

			using (var request = new UnityWebRequest(url, "POST"))
			{
				request.uploadHandler = new UploadHandlerRaw(bodyBytes);
				request.downloadHandler = new DownloadHandlerBuffer();
				request.SetRequestHeader("Content-Type", "application/json");
				request.timeout = TimeoutSeconds;

				var operation = request.SendWebRequest();

				// Await completion
				while (!operation.isDone)
				{
					await Task.Yield();
				}

				if (request.result != UnityWebRequest.Result.Success)
				{
					Debug.LogError($"{LogPrefix} Request failed: {request.error} (URL: {url})");
					return null;
				}

				var responseText = request.downloadHandler.text;
				if (string.IsNullOrWhiteSpace(responseText))
				{
					Debug.LogError($"{LogPrefix} Empty response from Ollama.");
					return null;
				}

				try
				{
					var response = JsonConvert.DeserializeObject<OllamaChatResponsePayload>(responseText);
					return response;
				}
				catch (System.Exception ex)
				{
					Debug.LogError($"{LogPrefix} Failed to parse Ollama response: {ex.Message}");
					return null;
				}
			}
		}

		/// <summary>
		/// Checks whether the local Ollama instance is reachable.
		/// </summary>
		/// <param name="baseUrl">Ollama API base URL.</param>
		/// <returns>True when Ollama is running and responsive.</returns>
		public static async Task<bool> IsAvailableAsync(string baseUrl = null)
		{
			var resolvedBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
			var url = resolvedBaseUrl + "/api/tags";

			try
			{
				using (var request = UnityWebRequest.Get(url))
				{
					request.timeout = 5;
					var operation = request.SendWebRequest();

					while (!operation.isDone)
					{
						await Task.Yield();
					}

					return request.result == UnityWebRequest.Result.Success;
				}
			}
			catch
			{
				return false;
			}
		}
	}
}
