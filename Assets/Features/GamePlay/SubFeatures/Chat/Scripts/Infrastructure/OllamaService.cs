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
		/// History is compressed into CharacterName:Text lines and embedded in the system prompt
		/// so Ollama treats it as context only and never tries to continue or mimic the format.
		/// </summary>
		/// <param name="systemPrompt">System instruction prompt from server.</param>
		/// <param name="history">Chat history messages.</param>
		/// <param name="userMessage">Current user message to send.</param>
		/// <param name="baseUrl">Ollama API base URL. Defaults to http://localhost:11434.</param>
		/// <param name="model">Model name. Defaults to gemma4:e4b.</param>
		/// <returns>Ollama response payload, or null on failure.</returns>
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

			// Build a single system message that contains both the original system prompt and the
			// full compressed chat history. Embedding history in the system role prevents Ollama
			// from mistaking the CharacterName:Text lines as a format it should continue producing.
			var systemBuilder = new StringBuilder();
			if (!string.IsNullOrWhiteSpace(systemPrompt))
			{
				systemBuilder.AppendLine(systemPrompt.Trim());
			}

			var compressedHistory = BuildCompressedHistory(history);
			if (!string.IsNullOrWhiteSpace(compressedHistory))
			{
				systemBuilder.AppendLine();
				systemBuilder.AppendLine("Lịch sử chat (chỉ để tham khảo ngữ cảnh, không phải định dạng trả lời):");
				systemBuilder.AppendLine(compressedHistory);
			}

			var messages = new List<OllamaChatMessage>
			{
				new OllamaChatMessage { Role = "system", Content = systemBuilder.ToString().Trim() },
			};

			// Only the current user message goes as a real turn.
			var currentUserMessage = userMessage?.Trim() ?? "";
			if (!string.IsNullOrWhiteSpace(currentUserMessage))
			{
				messages.Add(new OllamaChatMessage { Role = "user", Content = currentUserMessage });
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
		/// Requests Ollama to rewrite an invalid reply into valid assistant-turn JSON.
		/// Used as a safety gate before saving local replies to server history.
		/// </summary>
		/// <param name="invalidReply">Invalid non-JSON or malformed JSON reply.</param>
		/// <param name="originalSystemPrompt">Original generation system prompt to mirror output rules.</param>
		/// <param name="baseUrl">Ollama API base URL.</param>
		/// <param name="model">Model name.</param>
		/// <returns>Ollama response payload containing repaired JSON content, or null on failure.</returns>
		public static async Task<OllamaChatResponsePayload> RepairReplyJsonAsync(
			string invalidReply,
			string originalSystemPrompt = null,
			string baseUrl = null,
			string model = null)
		{
			if (string.IsNullOrWhiteSpace(invalidReply))
			{
				return null;
			}

			var resolvedBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
			var resolvedModel = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
			var url = resolvedBaseUrl + "/api/chat";

			var repairInstructionBuilder = new StringBuilder();
			repairInstructionBuilder.AppendLine("Rewrite the assistant response into a STRICT valid JSON reply.");
			repairInstructionBuilder.AppendLine("You must follow the same response rules as the original chat prompt.");
			repairInstructionBuilder.AppendLine();

			if (!string.IsNullOrWhiteSpace(originalSystemPrompt))
			{
				repairInstructionBuilder.AppendLine("ORIGINAL SYSTEM PROMPT (follow these rules exactly):");
				repairInstructionBuilder.AppendLine(originalSystemPrompt.Trim());
				repairInstructionBuilder.AppendLine();
			}

			repairInstructionBuilder.AppendLine("STRICT OUTPUT RULES:");
			repairInstructionBuilder.AppendLine("1) Return ONLY valid JSON (no markdown, no code fences, no explanation).");
			repairInstructionBuilder.AppendLine("2) Output must be a JSON array with 1-10 objects.");
			repairInstructionBuilder.AppendLine("3) Each object MUST include: MessageId, CharacterName, Text, Pinyin, Tone, Translation.");
			repairInstructionBuilder.AppendLine("4) MessageId should be a UUID string.");
			repairInstructionBuilder.AppendLine("5) Text must stay Chinese (Simplified) and keep original meaning.");
			repairInstructionBuilder.AppendLine("6) Translation must be Vietnamese.");
			repairInstructionBuilder.AppendLine("7) Tone must be English words only.");
			repairInstructionBuilder.AppendLine("8) Preserve optional memory sidecar fields if they already exist and remain valid.");
			repairInstructionBuilder.AppendLine();
			repairInstructionBuilder.AppendLine("INVALID ASSISTANT RESPONSE TO FIX:");
			repairInstructionBuilder.AppendLine(invalidReply.Trim());

			var repairInstruction = repairInstructionBuilder.ToString();

			var messages = new List<OllamaChatMessage>
			{
				new OllamaChatMessage { Role = "system", Content = "You are a strict JSON formatter for chat assistant replies." },
				new OllamaChatMessage { Role = "user", Content = repairInstruction },
			};

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
					Debug.LogError($"{LogPrefix} JSON repair request failed: {request.error} (URL: {url})");
					return null;
				}

				var responseText = request.downloadHandler.text;
				if (string.IsNullOrWhiteSpace(responseText))
				{
					Debug.LogError($"{LogPrefix} Empty JSON repair response from Ollama.");
					return null;
				}

				try
				{
					return JsonConvert.DeserializeObject<OllamaChatResponsePayload>(responseText);
				}
				catch (System.Exception ex)
				{
					Debug.LogError($"{LogPrefix} Failed to parse Ollama JSON repair response: {ex.Message}");
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
		/// Builds a compact text block from the full chat history for embedding in the system prompt.
		/// Format per line: "User:<text>", "developer:<text>", or "<CharacterName>:<text>".
		/// Developer and system messages are included so the model has full context.
		/// </summary>
		private static string BuildCompressedHistory(List<ChatHistoryMessagePayload> history)
		{
			if (history == null || history.Count == 0)
			{
				return "";
			}

			var sb = new StringBuilder();
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
						sb.AppendLine(compressed);
					}
				}
				else
				{
					var label = role == "developer" ? "developer" : "User";
					sb.AppendLine(label + ":" + msg.Content.Trim());
				}
			}

			return sb.ToString().TrimEnd();
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
