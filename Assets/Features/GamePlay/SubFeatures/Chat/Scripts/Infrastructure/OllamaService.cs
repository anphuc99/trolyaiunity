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
		public const string DefaultModel = "gemma4:e4b";

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
		/// <param name="model">Model name. Defaults to gemma4:e4b.</param>
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
			// Build compact message array using CharacterName:Text format to minimise token usage.
			var messages = new List<OllamaChatMessage>();

			// System prompt (sent as-is; no extra wrapper needed with the compact history format)
			if (!string.IsNullOrWhiteSpace(systemPrompt))
			{
				messages.Add(new OllamaChatMessage { Role = "system", Content = systemPrompt.Trim() });
			}

			// Compressed history: user → "User:<text>", developer → "developer:<text>",
			// assistant → "<CharacterName>:<Text>" lines (JSON stripped).
			if (history != null)
			{
				foreach (var msg in history)
				{
					if (msg == null || string.IsNullOrWhiteSpace(msg.Content))
					{
						continue;
					}

					var role = (msg.Role ?? "").ToLowerInvariant();
					if (role == "system")
					{
						continue;
					}

					if (role == "assistant")
					{
						var compressed = CompressAssistantContent(msg.Content);
						if (!string.IsNullOrWhiteSpace(compressed))
						{
							messages.Add(new OllamaChatMessage { Role = "assistant", Content = compressed });
						}
					}
					else
					{
						// user → "User:<text>", developer → "developer:<text>"
						var label = role == "developer" ? "developer" : "User";
						messages.Add(new OllamaChatMessage { Role = "user", Content = label + ":" + msg.Content.Trim() });
					}
				}
			}

			// Current user message
			var currentUserMessage = userMessage?.Trim() ?? "";
			if (!string.IsNullOrWhiteSpace(currentUserMessage))
			{
				messages.Add(new OllamaChatMessage { Role = "user", Content = "User:" + currentUserMessage });
			}

			Debug.Log($"{LogPrefix} Sending {messages.Count} messages to Ollama (model: {resolvedModel}).");

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

		/// <summary>
		/// Sends a summarization request to the local Ollama instance.
		/// Uses a simple prompt with compressed conversation history.
		/// </summary>
		/// <param name="compressedHistory">Compressed conversation in CharacterName:Text format.</param>
		/// <param name="baseUrl">Ollama API base URL.</param>
		/// <param name="model">Model name.</param>
		/// <returns>Raw assistant content string, or null on failure.</returns>
		public static async Task<OllamaChatResponsePayload> SummarizeConversationAsync(
			string compressedHistory,
			string baseUrl = null,
			string model = null)
		{
			var resolvedBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
			var resolvedModel = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
			var url = resolvedBaseUrl + "/api/chat";

			var summaryInstruction = @"Please summarize the above conversation in Vietnamese, update the story description, and return it in JSON format as follows:
{
  ""Summary"": ""Summary of the conversation here."",
  ""UpdatedStoryDescription"": ""The story description has been updated here.""
}
Only return the JSON object, no extra text.";

			var messages = new List<OllamaChatMessage>
			{
				new OllamaChatMessage { Role = "user", Content = compressedHistory },
				new OllamaChatMessage { Role = "user", Content = summaryInstruction }
			};

			var requestPayload = new OllamaChatRequestPayload
			{
				Model = resolvedModel,
				Messages = messages,
				Stream = false,
			};

			var jsonBody = JsonConvert.SerializeObject(requestPayload);
			Debug.Log($"{LogPrefix} Sending summarization request to Ollama.");
			var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

			using (var request = new UnityWebRequest(url, "POST"))
			{
				request.uploadHandler = new UploadHandlerRaw(bodyBytes);
				request.downloadHandler = new DownloadHandlerBuffer();
				request.SetRequestHeader("Content-Type", "application/json");
				request.timeout = TimeoutSeconds;

				var operation = request.SendWebRequest();

				while (!operation.isDone)
				{
					await Task.Yield();
				}

				if (request.result != UnityWebRequest.Result.Success)
				{
					Debug.LogError($"{LogPrefix} Summarization request failed: {request.error}");
					return null;
				}

				var responseText = request.downloadHandler.text;
				if (string.IsNullOrWhiteSpace(responseText))
				{
					Debug.LogError($"{LogPrefix} Empty summarization response from Ollama.");
					return null;
				}

				try
				{
					var response = JsonConvert.DeserializeObject<OllamaChatResponsePayload>(responseText);
					return response;
				}
				catch (System.Exception ex)
				{
					Debug.LogError($"{LogPrefix} Failed to parse Ollama summarization response: {ex.Message}");
					return null;
				}
			}
		}

		/// <summary>
		/// Compresses an assistant reply (JSON array or single object of turns) into compact
		/// "CharacterName:Text" lines. Falls back to "Mimi:content" when JSON cannot be parsed.
		/// </summary>
		private static string CompressAssistantContent(string content)
		{
			if (string.IsNullOrWhiteSpace(content))
			{
				return "";
			}

			try
			{
				var turns = JsonConvert.DeserializeObject<List<ChatAssistantTurnPayload>>(content);
				if (turns != null && turns.Count > 0)
				{
					return BuildCompressedTurns(turns);
				}
			}
			catch { }

			try
			{
				var turn = JsonConvert.DeserializeObject<ChatAssistantTurnPayload>(content);
				if (turn != null && !string.IsNullOrWhiteSpace(turn.Text))
				{
					var name = !string.IsNullOrWhiteSpace(turn.CharacterName) ? turn.CharacterName.Trim() : "Mimi";
					return name + ":" + turn.Text.Trim();
				}
			}
			catch { }

			return "Mimi:" + content.Trim();
		}

		/// <summary>
		/// Formats a list of parsed assistant turns as "CharacterName:Text" lines.
		/// </summary>
		private static string BuildCompressedTurns(List<ChatAssistantTurnPayload> turns)
		{
			var sb = new StringBuilder();
			foreach (var turn in turns)
			{
				if (turn == null)
				{
					continue;
				}

				var name = !string.IsNullOrWhiteSpace(turn.CharacterName) ? turn.CharacterName.Trim() : "Mimi";
				var text = !string.IsNullOrWhiteSpace(turn.Text) ? turn.Text.Trim() : "";
				if (!string.IsNullOrWhiteSpace(text))
				{
					sb.AppendLine(name + ":" + text);
				}
			}

			return sb.ToString().TrimEnd();
		}
	}
}
