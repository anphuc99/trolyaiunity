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
			var messages = new List<OllamaChatMessage>();

			// Add system prompt
			var systemInstruction = BuildOllamaSystemInstruction(systemPrompt ?? "");
			if (!string.IsNullOrWhiteSpace(systemInstruction))
			{
				messages.Add(new OllamaChatMessage { Role = "system", Content = systemInstruction });
			}

			var pendingDeveloperMessages = new List<string>();

			void FlushDeveloperOnlyBlock()
			{
				if (pendingDeveloperMessages.Count == 0) return;
				var merged = FormatMergedDeveloperUserMessage(pendingDeveloperMessages, "");
				if (!string.IsNullOrWhiteSpace(merged))
				{
					messages.Add(new OllamaChatMessage { Role = "user", Content = merged });
				}
				pendingDeveloperMessages.Clear();
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

					if (role == "developer")
					{
						pendingDeveloperMessages.Add(msg.Content);
					}
					else if (role == "assistant")
					{
						FlushDeveloperOnlyBlock();
						messages.Add(new OllamaChatMessage { Role = "assistant", Content = msg.Content });
					}
					else if (role == "user")
					{
						if (pendingDeveloperMessages.Count > 0)
						{
							var merged = FormatMergedDeveloperUserMessage(pendingDeveloperMessages, msg.Content);
							if (!string.IsNullOrWhiteSpace(merged))
							{
								messages.Add(new OllamaChatMessage { Role = "user", Content = merged });
							}
							pendingDeveloperMessages.Clear();
						}
						else
						{
							messages.Add(new OllamaChatMessage { Role = "user", Content = msg.Content });
						}
					}
				}
			}

			// Add current user message
			var currentUserMessage = userMessage?.Trim() ?? "";
			if (pendingDeveloperMessages.Count > 0)
			{
				var merged = FormatMergedDeveloperUserMessage(pendingDeveloperMessages, currentUserMessage);
				if (!string.IsNullOrWhiteSpace(merged))
				{
					messages.Add(new OllamaChatMessage { Role = "user", Content = merged });
				}
				pendingDeveloperMessages.Clear();
			}
			else if (!string.IsNullOrWhiteSpace(currentUserMessage))
			{
				messages.Add(new OllamaChatMessage { Role = "user", Content = currentUserMessage });
			}
			else
			{
				FlushDeveloperOnlyBlock();
			}

			foreach (var msg in messages)
			{
				if(msg.Role != "system")
				{
					Debug.Log($"{LogPrefix} Prepared message - Role: {msg.Role}, Content: {msg.Content}");
				}
			}

			var requestPayload = new OllamaChatRequestPayload
			{
				Model = resolvedModel,
				Messages = messages,
				Stream = false,
			};

			var jsonBody = JsonConvert.SerializeObject(requestPayload);
			Debug.Log($"{LogPrefix} Sending payload to Ollama: {jsonBody}");
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

		private static string BuildOllamaSystemInstruction(string systemPrompt)
		{
			const string developerRoleExplanation = @"
====================================
DEVELOPER ROLE EXPLANATION
====================================
You may receive a USER message that contains one or more blocks formatted like this:

developer:
<instruction text>

user:
<actual user message>

How this format works:
1. Every ""developer:"" block is META-LEVEL instruction, not end-user dialogue.
2. The final ""user:"" block is the real user message you should answer.
3. If there is no ""user:"" block, treat the content as context update only.

Developer instructions can:
1. Provide context updates (e.g., story progress, relationship changes)
2. Announce character additions or removals
3. Request conversation summaries
4. Provide editing instructions for previous messages

When ""developer:"" blocks are present:
- DO NOT answer or quote developer text directly
- Apply those instructions silently as constraints/context
- Answer only the ""user:"" part naturally
- Never expose internal reasoning about these instructions

";
			return developerRoleExplanation + systemPrompt;
		}

		private static string FormatMergedDeveloperUserMessage(List<string> developerMessages, string userContent)
		{
			var sections = new List<string>();
			foreach (var entry in developerMessages)
			{
				if (!string.IsNullOrWhiteSpace(entry))
				{
					sections.Add($"developer:\n{entry.Trim()}");
				}
			}

			var trimmedUserContent = userContent?.Trim() ?? "";
			if (!string.IsNullOrWhiteSpace(trimmedUserContent))
			{
				sections.Add($"user:\n{trimmedUserContent}");
			}

			return string.Join("\n\n", sections).Trim();
		}
	}
}
