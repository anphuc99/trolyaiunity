using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Core.Infrastructure.Authentication;
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
		private static readonly SemaphoreSlim _refreshSemaphore = new SemaphoreSlim(1, 1);
		private static bool _isRefreshing;

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
			var response = await SendRequestWithRetryAsync(
				() => UnityWebRequest.Get(resolvedUrl),
				headers,
				timeoutSeconds,
				cancellationToken);

			return response;
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

			var response = await SendRequestWithRetryAsync(
				() =>
				{
					var request = new UnityWebRequest(resolvedUrl, UnityWebRequest.kHttpVerbPOST);
					request.uploadHandler = new UploadHandlerRaw(bodyBytes);
					request.downloadHandler = new DownloadHandlerBuffer();
					request.SetRequestHeader("Content-Type", JsonContentType);
					return request;
				},
				headers,
				timeoutSeconds,
				cancellationToken);

			return response;
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
			if (request == null)
			{
				return;
			}

			var token = AuthTokenModel.AccessToken;
			if (!string.IsNullOrWhiteSpace(token))
			{
				request.SetRequestHeader("Authorization", $"Bearer {token}");
			}

			if (headers == null || headers.Count == 0)
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

		private static async UniTask<string> SendRequestWithRetryAsync(
			System.Func<UnityWebRequest> createRequest,
			Dictionary<string, string> headers,
			int timeoutSeconds,
			CancellationToken cancellationToken)
		{
			// Try 1
			var (response, code) = await SendSingleRequestAsync(createRequest, headers, timeoutSeconds, cancellationToken);

			// If 401, potentially expired token, try refresh once
			if (code == 401 && !UseFakeResponses())
			{
				// Check if it's explicitly TOKEN_EXPIRED
				bool isTokenExpired = false;
				try
				{
					var errorObj = JsonConvert.DeserializeObject<AuthErrorResponse>(response);
					if (errorObj != null && errorObj.Code == "TOKEN_EXPIRED")
					{
						isTokenExpired = true;
					}
				}
				catch { /* ignored */ }

				if (isTokenExpired) 
				{
					Debug.Log($"{LogPrefix} Received 401 with TOKEN_EXPIRED. Attempting refresh...");
					bool refreshed = await TryRefreshTokenAsync(cancellationToken);
					if (refreshed)
					{
						Debug.Log($"{LogPrefix} Token refreshed successfully. Retrying original request...");
						(response, code) = await SendSingleRequestAsync(createRequest, headers, timeoutSeconds, cancellationToken);
					}
				}
				else
				{
					Debug.LogWarning($"{LogPrefix} Received 401 but not TOKEN_EXPIRED. Returning error to user.");
				}
			}

			return response;
		}

		private static async UniTask<(string text, long code)> SendSingleRequestAsync(
			System.Func<UnityWebRequest> createRequest,
			Dictionary<string, string> headers,
			int timeoutSeconds,
			CancellationToken cancellationToken)
		{
			using (var request = createRequest())
			{
				ApplyHeaders(request, headers);
				ApplyTimeout(request, timeoutSeconds);

				try
				{
					await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
				}
				catch (System.Exception ex) when (!(ex is System.OperationCanceledException)) { }

				long responseCode = request.responseCode;
				string responseText = request.downloadHandler?.text;

				if (request.result != UnityWebRequest.Result.Success)
				{
					Debug.LogError($"{LogPrefix} Request to '{request.url}' failed ({request.responseCode}): {request.error}");
					return (responseText, responseCode);
				}

				return (responseText, responseCode);
			}
		}

		private static async UniTask<bool> TryRefreshTokenAsync(CancellationToken cancellationToken)
		{
			var refreshToken = AuthTokenModel.RefreshToken;
			if (string.IsNullOrWhiteSpace(refreshToken))
			{
				return false;
			}

			await _refreshSemaphore.WaitAsync(cancellationToken);
			try
			{
				if (_isRefreshing)
				{
					// This should not happen with semaphore, but just in case
					return false; 
				}

				_isRefreshing = true;
				Debug.Log($"{LogPrefix} Refreshing access token...");

				var payload = new TokenRefreshRequest { RefreshToken = refreshToken };
				var jsonPayload = JsonConvert.SerializeObject(payload);
				var resolvedUrl = ResolveUrl(NetworkEndpoints.TokenRefresh);

				using (var request = new UnityWebRequest(resolvedUrl, UnityWebRequest.kHttpVerbPOST))
				{
					byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
					request.uploadHandler = new UploadHandlerRaw(bodyRaw);
					request.downloadHandler = new DownloadHandlerBuffer();
					request.SetRequestHeader("Content-Type", JsonContentType);
					ApplyTimeout(request, 10);

					try
					{
						await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);
					}
					catch (System.Exception ex) when (!(ex is System.OperationCanceledException)) { }

					if (request.result == UnityWebRequest.Result.Success)
					{
						var responseJson = request.downloadHandler?.text;
						var result = JsonConvert.DeserializeObject<TokenRefreshResponse>(responseJson);
						if (result != null && !string.IsNullOrEmpty(result.AccessToken))
						{
							AuthTokenModel.AccessToken = result.AccessToken;
							if (!string.IsNullOrEmpty(result.RefreshToken))
							{
								AuthTokenModel.RefreshToken = result.RefreshToken;
							}
							Debug.Log($"{LogPrefix} Access token renewed successfully.");
							return true;
						}
					}

					Debug.LogError($"{LogPrefix} Failed to refresh token: {request.error}");
					return false;
				}
			}
			finally
			{
				_isRefreshing = false;
				_refreshSemaphore.Release();
			}
		}

		private static bool IsAccessTokenExpired()
		{
			var token = AuthTokenModel.AccessToken;
			if (string.IsNullOrWhiteSpace(token))
			{
				return true;
			}

			try
			{
				var parts = token.Split('.');
				if (parts.Length != 3)
				{
					// If not a standard JWT, check if it's "fake-jwt"
					if (token.StartsWith("fake-jwt")) return false; 
					return false;
				}

				var payload = parts[1];
				// Fix base64 padding
				int mod4 = payload.Length % 4;
				if (mod4 > 0) payload += new string('=', 4 - mod4);
				
				var json = Encoding.UTF8.GetString(System.Convert.FromBase64String(payload));
				var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

				if (data != null && data.TryGetValue("exp", out var expObj))
				{
					var exp = System.Convert.ToInt64(expObj);
					var expTime = System.DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
					// Refresh if expiring within 30 seconds
					return System.DateTime.UtcNow.AddSeconds(30) > expTime;
				}
			}
			catch
			{
				// If parsing fails, don't assume expired, let 401 handle it
			}

			return false;
		}
	}
}
