using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Core.Infrastructure.Network
{
	/// <summary>
	/// Simple HTTP client for GET and JSON POST requests.
	/// </summary>
	public static class HttpClient
	{
		private const string LogPrefix = "[HttpClient]";
		private const string JsonContentType = "application/json";

		/// <summary>
		/// Sends a GET request and returns the response text.
		/// </summary>
		/// <param name="url">Request URL.</param>
		/// <param name="headers">Optional headers.</param>
		/// <param name="timeoutSeconds">Optional timeout in seconds (0 means default).</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Response text or null on failure.</returns>
		public static async UniTask<string> GetAsync(
			string url,
			Dictionary<string, string> headers = null,
			int timeoutSeconds = 0,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(url))
			{
				Debug.LogError($"{LogPrefix} GET failed: url is null/empty.");
				return null;
			}

			using (var request = UnityWebRequest.Get(url))
			{
				ApplyHeaders(request, headers);
				ApplyTimeout(request, timeoutSeconds);

				await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

				if (request.result != UnityWebRequest.Result.Success)
				{
					Debug.LogError($"{LogPrefix} GET '{url}' failed: {request.error}");
					return null;
				}

				return request.downloadHandler?.text;
			}
		}

		/// <summary>
		/// Sends a JSON POST request and returns the response text.
		/// </summary>
		/// <typeparam name="T">Payload type to serialize to JSON.</typeparam>
		/// <param name="url">Request URL.</param>
		/// <param name="payload">Payload object. If null, an empty JSON object is sent.</param>
		/// <param name="headers">Optional headers.</param>
		/// <param name="timeoutSeconds">Optional timeout in seconds (0 means default).</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Response text or null on failure.</returns>
		public static async UniTask<string> PostJsonAsync<T>(
			string url,
			T payload,
			Dictionary<string, string> headers = null,
			int timeoutSeconds = 0,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(url))
			{
				Debug.LogError($"{LogPrefix} POST failed: url is null/empty.");
				return null;
			}

			var json = payload != null ? JsonUtility.ToJson(payload) : "{}";
			var bodyBytes = Encoding.UTF8.GetBytes(json);

			using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
			{
				request.uploadHandler = new UploadHandlerRaw(bodyBytes);
				request.downloadHandler = new DownloadHandlerBuffer();
				request.SetRequestHeader("Content-Type", JsonContentType);
				ApplyHeaders(request, headers);
				ApplyTimeout(request, timeoutSeconds);

				await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

				if (request.result != UnityWebRequest.Result.Success)
				{
					Debug.LogError($"{LogPrefix} POST '{url}' failed: {request.error}");
					return null;
				}

				return request.downloadHandler?.text;
			}
		}

		private static void ApplyHeaders(UnityWebRequest request, Dictionary<string, string> headers)
		{
			if (request == null || headers == null || headers.Count == 0)
			{
				return;
			}

			foreach (var pair in headers)
			{
				if (string.IsNullOrWhiteSpace(pair.Key))
				{
					continue;
				}

				request.SetRequestHeader(pair.Key, pair.Value);
			}
		}

		private static void ApplyTimeout(UnityWebRequest request, int timeoutSeconds)
		{
			if (request == null || timeoutSeconds <= 0)
			{
				return;
			}

			request.timeout = timeoutSeconds;
		}
	}
}
