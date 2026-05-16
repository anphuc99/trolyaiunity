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
		public const string DefaultBaseUrl = "http://175.155.64.164:19731";

		public const string AUTHORIZATION = "Bearer ";

		/// <summary>
		/// Default model to use for local AI generation.
		/// </summary>
		public const string DefaultModel = "hf.co/mradermacher/Qwen2.5-Coder-32B-Instruct-Uncensored-i1-GGUF:Q4_K_M";

		/// <summary>
		/// Timeout in seconds for Ollama requests. Local generation may take longer.
		/// </summary>
		private const int TimeoutSeconds = 300;

		private const string LogPrefix = "[OllamaService]";

		/// <summary>
		/// Sends a chat request to the local Ollama instance and returns the assistant reply.
		/// Uses the provided system prompt + raw history turns as Ollama messages.
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

			var messages = new List<OllamaChatMessage>();

			var trimmedSystemPrompt = systemPrompt?.Trim() ?? "";
			if (!string.IsNullOrWhiteSpace(trimmedSystemPrompt))
			{
				messages.Add(new OllamaChatMessage { Role = "system", Content = trimmedSystemPrompt });
			}

			if (history != null && history.Count > 0)
			{
				foreach (var message in history)
				{
					if (message == null || string.IsNullOrWhiteSpace(message.Content))
					{
						continue;
					}

					var role = (message.Role ?? "").Trim().ToLowerInvariant();
					if (role == "system")
					{
						continue;
					}

					if (role == "assistant" && IsRecallMemoryContent(message.Content))
					{
						continue;
					}

					var mappedRole = role == "assistant"
						? "assistant"
						: role == "developer" ? "system" : "user";

					messages.Add(new OllamaChatMessage
					{
						Role = mappedRole,
						Content = message.Content.Trim(),
					});
				}
			}

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
				request.SetRequestHeader("Authorization", AUTHORIZATION);
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
					Debug.Log($"{LogPrefix} Received response from Ollama: {responseText}");
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
		/// Sends a request to Ollama to generate a conversational outline before generating the actual JSON response.
		/// </summary>
		public static async Task<OllamaChatResponsePayload> GenerateOutlineAsync(
			List<ChatHistoryMessagePayload> history,
			string userMessage,
			string baseUrl = null,
			string model = null)
		{
			var outlineSystemPrompt = "你是一个剧本导演。请根据聊天记录和用户的最新消息，为接下来的角色回复起草一个详细的大纲。请包含：1）当前的场景/背景；2）角色在当前情境下的合理想法和心理活动；3）各个角色接下来将要说的话的要点。请仅返回中文大纲文本，不需要生成JSON格式。";
			return await SendChatAsync(outlineSystemPrompt, history, userMessage, baseUrl, model);
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

			// Note: the original system prompt is intentionally excluded here.
			// Including the full production prompt (character info, story context, lesson data)
			// makes the repair request too large and causes Ollama to produce a new reply
			// instead of strictly reformatting the invalid content.
			var repairInstructionBuilder = new StringBuilder();
			repairInstructionBuilder.AppendLine("Rewrite the assistant response into a STRICT valid JSON reply.");
			repairInstructionBuilder.AppendLine("Do NOT generate new dialogue. Only reformat the content below into valid JSON.");
			repairInstructionBuilder.AppendLine();

			repairInstructionBuilder.AppendLine("JSON FORMAT GUIDE (MUST MATCH):");
			repairInstructionBuilder.AppendLine("1) Return ONLY valid JSON. No markdown, no code fences, no explanation.");
			repairInstructionBuilder.AppendLine("2) Return a JSON ARRAY of 1-10 objects.");
			repairInstructionBuilder.AppendLine("3) Every object MUST include EXACT keys with this casing:");
			repairInstructionBuilder.AppendLine("   MessageId, CharacterName, Text, Pinyin, Tone, Translation");
			repairInstructionBuilder.AppendLine("4) MessageId should be UUID-like string.");
			repairInstructionBuilder.AppendLine("5) Text: Chinese (Simplified). Keep original meaning.");
			repairInstructionBuilder.AppendLine("6) Pinyin: include spaces between syllables.");
			repairInstructionBuilder.AppendLine("7) Tone: English-only descriptive direction.");
			repairInstructionBuilder.AppendLine("8) Translation: Vietnamese only.");
			repairInstructionBuilder.AppendLine("9) If memory sidecar fields exist or are needed, use EXACT key names:");
			repairInstructionBuilder.AppendLine("   GlobalMemoryEn, GlobalMemoryType, GlobalMemoryImportance");
			repairInstructionBuilder.AppendLine("   ImportantMemoryEn, ImportantMemoryType, ImportantMemoryImportance, ImportantMemoryActor");
			repairInstructionBuilder.AppendLine("10) Do NOT return recall_memory command in this repair step.");
			repairInstructionBuilder.AppendLine();
			repairInstructionBuilder.AppendLine("REFERENCE EXAMPLES (follow this style):");
			repairInstructionBuilder.AppendLine("[");
			repairInstructionBuilder.AppendLine("  {");
			repairInstructionBuilder.AppendLine("    \"MessageId\": \"317e30c6-6c46-448c-b1a4-91aa5b9253a1\",");
			repairInstructionBuilder.AppendLine("    \"CharacterName\": \"Mimi\",");
			repairInstructionBuilder.AppendLine("    \"Text\": \"你怎么这样!!!\",");
			repairInstructionBuilder.AppendLine("    \"Pinyin\": \"Nǐ zěn me zhè yàng!!!\",");
			repairInstructionBuilder.AppendLine("    \"Tone\": \"Angry and hurt, voice rising with frustration, fast and sharp delivery\",");
			repairInstructionBuilder.AppendLine("    \"Emotion\": \"angry\",,");
			repairInstructionBuilder.AppendLine("    \"Intensity\": \"high\",,");
			repairInstructionBuilder.AppendLine("    \"Translation\": \"Sao bạn lại như vậy!\"");
			repairInstructionBuilder.AppendLine("    \"Context\": \"Mimi hào hứng vẫy tay\",");
			repairInstructionBuilder.AppendLine("  }");
			repairInstructionBuilder.AppendLine("]");
			repairInstructionBuilder.AppendLine();
			repairInstructionBuilder.AppendLine("[");
			repairInstructionBuilder.AppendLine("  {");
			repairInstructionBuilder.AppendLine("    \"MessageId\": \"a1b2c3d4-e5f6-7890-abcd-ef1234567890\",");
			repairInstructionBuilder.AppendLine("    \"CharacterName\": \"Mimi\",");
			repairInstructionBuilder.AppendLine("    \"Text\": \"好的，我们这周末去公园！\",");
			repairInstructionBuilder.AppendLine("    \"Pinyin\": \"Hǎo de, wǒ men zhè zhōu mò qù gōng yuán!\",");
			repairInstructionBuilder.AppendLine("    \"Tone\": \"Excited and happy, bright voice with a big smile, upbeat pace\",");
			repairInstructionBuilder.AppendLine("    \"Translation\": \"Được rồi, chúng ta sẽ đi công viên cuối tuần này!\",");
			repairInstructionBuilder.AppendLine("    \"GlobalMemoryEn\": \"The group decided to visit the park this weekend.\",");
			repairInstructionBuilder.AppendLine("    \"GlobalMemoryType\": \"plan\",");
			repairInstructionBuilder.AppendLine("    \"GlobalMemoryImportance\": \"high\"");
			repairInstructionBuilder.AppendLine("  }");
			repairInstructionBuilder.AppendLine("]");
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
				request.SetRequestHeader("Authorization", AUTHORIZATION);
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
				Format = "json",
			};

			var jsonBody = JsonConvert.SerializeObject(requestPayload);
			Debug.Log($"{LogPrefix} Sending summarization request to Ollama.");
			var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

			using (var request = new UnityWebRequest(url, "POST"))
			{
				request.uploadHandler = new UploadHandlerRaw(bodyBytes);
				request.downloadHandler = new DownloadHandlerBuffer();
				request.SetRequestHeader("Content-Type", "application/json");
				request.SetRequestHeader("Authorization", AUTHORIZATION);
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
		/// Returns true when the message content is a recall-memory tool call.
		/// Recall-memory messages are JSON objects with a "recall_memory" array key,
		/// produced by the server AI pipeline — they must not be treated as dialogue.
		/// </summary>
		private static bool IsRecallMemoryContent(string content)
		{
			if (string.IsNullOrWhiteSpace(content))
			{
				return false;
			}

			var trimmed = content.Trim();
			if (!trimmed.StartsWith("{"))
			{
				return false;
			}

			try
			{
				var obj = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(trimmed);
				return obj != null && obj.ContainsKey("recall_memory");
			}
			catch
			{
				return false;
			}
		}

	}
}
