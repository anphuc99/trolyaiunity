using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
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
		private const string SettingsResourcePath = "NetworkSettings";
		private const string DefaultBaseUrl = "http://localhost:5000";
		private static NetworkSettings _settings;

		/// <summary>
		/// Sends a GET request and returns the response text.
		/// </summary>
		/// <param name="url">Request URL or relative path.</param>
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

			if (UseFakeResponses())
			{
				return await FakeGetAsync(url, cancellationToken);
			}

			var resolvedUrl = ResolveUrl(url);
			using (var request = UnityWebRequest.Get(resolvedUrl))
			{
				ApplyHeaders(request, headers);
				ApplyTimeout(request, timeoutSeconds);

				try
				{
					await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
				}
				catch (System.Exception ex) when (!(ex is System.OperationCanceledException))
				{
					// Exception is caught to allow checking request.result below.
				}

				if (request.result != UnityWebRequest.Result.Success)
				{
					Debug.LogError($"{LogPrefix} GET '{resolvedUrl}' failed ({request.responseCode}): {request.error}");
					return null;
				}

				return request.downloadHandler?.text;
			}
		}

		/// <summary>
		/// Sends a GET request and returns the response text as a Task.
		/// </summary>
		/// <param name="url">Request URL or relative path.</param>
		/// <param name="headers">Optional headers.</param>
		/// <param name="timeoutSeconds">Optional timeout in seconds (0 means default).</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Response text or null on failure.</returns>
		public static Task<string> GetTaskAsync(
			string url,
			Dictionary<string, string> headers = null,
			int timeoutSeconds = 0,
			CancellationToken cancellationToken = default)
		{
			return GetAsync(url, headers, timeoutSeconds, cancellationToken).AsTask();
		}

		/// <summary>
		/// Sends a JSON POST request and returns the response text.
		/// </summary>
		/// <typeparam name="T">Payload type to serialize to JSON.</typeparam>
		/// <param name="url">Request URL or relative path.</param>
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

			var json = payload != null ? JsonConvert.SerializeObject(payload) : "{}";

			if (UseFakeResponses())
			{
				return await FakePostJsonAsync(url, json, cancellationToken);
			}
			var bodyBytes = Encoding.UTF8.GetBytes(json);

			var resolvedUrl = ResolveUrl(url);
			using (var request = new UnityWebRequest(resolvedUrl, UnityWebRequest.kHttpVerbPOST))
			{
				request.uploadHandler = new UploadHandlerRaw(bodyBytes);
				request.downloadHandler = new DownloadHandlerBuffer();
				request.SetRequestHeader("Content-Type", JsonContentType);
				ApplyHeaders(request, headers);
				ApplyTimeout(request, timeoutSeconds);

				try
				{
					await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
				}
				catch (System.Exception ex) when (!(ex is System.OperationCanceledException))
				{
					// Exception is caught to allow checking request.result below.
				}

				if (request.result != UnityWebRequest.Result.Success)
				{
					Debug.LogError($"{LogPrefix} POST '{resolvedUrl}' failed ({request.responseCode}): {request.error}");
					return null;
				}

				return request.downloadHandler?.text;
			}
		}

		/// <summary>
		/// Sends a JSON POST request and returns the response text as a Task.
		/// </summary>
		/// <typeparam name="T">Payload type to serialize to JSON.</typeparam>
		/// <param name="url">Request URL or relative path.</param>
		/// <param name="payload">Payload object. If null, an empty JSON object is sent.</param>
		/// <param name="headers">Optional headers.</param>
		/// <param name="timeoutSeconds">Optional timeout in seconds (0 means default).</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Response text or null on failure.</returns>
		public static Task<string> PostJsonTaskAsync<T>(
			string url,
			T payload,
			Dictionary<string, string> headers = null,
			int timeoutSeconds = 0,
			CancellationToken cancellationToken = default)
		{
			return PostJsonAsync(url, payload, headers, timeoutSeconds, cancellationToken).AsTask();
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

		private static string ResolveUrl(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
			{
				return url;
			}

			if (url.StartsWith("http://") || url.StartsWith("https://"))
			{
				return url;
			}

			var baseUrl = ResolveBaseUrl();
			var normalizedBase = baseUrl?.TrimEnd('/') ?? string.Empty;
			var normalizedPath = url.TrimStart('/');
			return string.IsNullOrEmpty(normalizedBase)
				? normalizedPath
				: $"{normalizedBase}/{normalizedPath}";
		}

		private static string ResolveBaseUrl()
		{
			if (_settings == null)
			{
				_settings = Resources.Load<NetworkSettings>(SettingsResourcePath);
			}

			if (_settings == null)
			{
				return DefaultBaseUrl;
			}

			var baseUrl = _settings.BaseUrl;
			if (string.IsNullOrWhiteSpace(baseUrl))
			{
				return DefaultBaseUrl;
			}

			return baseUrl;
		}

		private static bool UseFakeResponses()
		{
			if (_settings == null)
			{
				_settings = Resources.Load<NetworkSettings>(SettingsResourcePath);
			}

			return _settings != null && _settings.UseFakeUrl;
		}

		private static UniTask<string> FakeGetAsync(string url, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return UniTask.FromCanceled<string>(cancellationToken);
			}

			if (FakeServer.TryGetResponse("GET", url, null, out var response))
			{
				return UniTask.FromResult(response);
			}

			var key = FakeServer.BuildKey("GET", url);
			Debug.LogWarning($"{LogPrefix} Fake GET missing for '{key}'.");
			return UniTask.FromResult(response);
		}

		private static UniTask<string> FakePostJsonAsync(string url, string jsonPayload, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return UniTask.FromCanceled<string>(cancellationToken);
			}

			if (FakeServer.TryGetResponse("POST", url, jsonPayload, out var response))
			{
				return UniTask.FromResult(response);
			}

			var key = FakeServer.BuildKey("POST", url);
			Debug.LogWarning($"{LogPrefix} Fake POST missing for '{key}'. Returning fallback.");
			return UniTask.FromResult(response);
		}
	}
}
