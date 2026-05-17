using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Features.GamePlay.SubFeatures.Chat.Model;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Shares.Model;

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

		public static string AUTHORIZATION => "Bearer " + EnvSettings.Instance.OllamaAuthorization;

		/// <summary>
		/// Default model to use for local AI generation.
		/// </summary>
		public const string DefaultModel = "hf.co/TrevorJS/gemma-4-E4B-it-uncensored-GGUF:Q8_0";

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
			repairInstructionBuilder.AppendLine("Rewrite the assistant response into STRICT pipe-delimited format.");
			repairInstructionBuilder.AppendLine("Do NOT generate new dialogue. Only reformat the content below.");
			repairInstructionBuilder.AppendLine();

			repairInstructionBuilder.AppendLine("PIPE-DELIMITED FORMAT GUIDE (MUST MATCH):");
			repairInstructionBuilder.AppendLine("1) Return ONLY pipe-delimited lines. No JSON, no markdown, no code fences, no explanation.");
			repairInstructionBuilder.AppendLine("2) Each line represents one message with exactly 7 fields separated by | (pipe).");
			repairInstructionBuilder.AppendLine("3) Field order: MessageId|CharacterName|Hanzi|Pinyin|Emotion|Intensity|Translation");
			repairInstructionBuilder.AppendLine("4) MessageId: UUID-like string.");
			repairInstructionBuilder.AppendLine("5) CharacterName: speaker name or 叙述者 for narrator.");
			repairInstructionBuilder.AppendLine("6) Hanzi: Chinese text (Simplified). May contain Latin letters for names.");
			repairInstructionBuilder.AppendLine("7) Pinyin: Latin characters with tone marks. Must NOT contain any Chinese characters.");
			repairInstructionBuilder.AppendLine("8) Emotion: one of: angry, shouting, disgusted, sad, scared, surprised, shy, affectionate, happy, excited, serious, neutral.");
			repairInstructionBuilder.AppendLine("9) Intensity: one of: low, medium, high.");
			repairInstructionBuilder.AppendLine("10) Translation: Vietnamese only.");
			repairInstructionBuilder.AppendLine("11) Do NOT use | inside any field value.");
			repairInstructionBuilder.AppendLine("12) 叙述者 narrator lines should appear before character dialogue lines.");
			repairInstructionBuilder.AppendLine();
			repairInstructionBuilder.AppendLine("REFERENCE EXAMPLES (follow this style):");
			repairInstructionBuilder.AppendLine("11111111-2222-3333-4444-555555555555|叙述者|Mimi 猛地站起来，瞪着你。|Mimi měng de zhàn qǐ lái, dèng zhe nǐ.|neutral|low|Mimi đột ngột đứng dậy, trừng mắt nhìn bạn.");
			repairInstructionBuilder.AppendLine("317e30c6-6c46-448c-b1a4-91aa5b9253a1|Mimi|你怎么这样!!!|Nǐ zěn me zhè yàng!!!|angry|high|Sao bạn lại như vậy!");
			repairInstructionBuilder.AppendLine();
			repairInstructionBuilder.AppendLine("a1b2c3d4-0000-0000-0000-000000000001|叙述者|Mimi 微笑着向你挥手。|Mimi wēi xiào zhe xiàng nǐ huī shǒu.|neutral|low|Mimi mỉm cười vẫy tay chào bạn.");
			repairInstructionBuilder.AppendLine("a1b2c3d4-e5f6-7890-abcd-ef1234567890|Mimi|好的，我们这周末去公园！|Hǎo de, wǒ men zhè zhōu mò qù gōng yuán!|happy|medium|Được rồi, chúng ta sẽ đi công viên cuối tuần này!");
			repairInstructionBuilder.AppendLine();
			repairInstructionBuilder.AppendLine("INVALID ASSISTANT RESPONSE TO FIX:");
			repairInstructionBuilder.AppendLine(invalidReply.Trim());

			var repairInstruction = repairInstructionBuilder.ToString();

			var messages = new List<OllamaChatMessage>
			{
				new OllamaChatMessage { Role = "system", Content = "You are a strict pipe-delimited text formatter for chat assistant replies. Format: MessageId|CharacterName|Hanzi|Pinyin|Emotion|Intensity|Translation" },
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
