using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Core.Infrastructure.Network
{
	/// <summary>
	/// In-code fake server for offline testing without real HTTP calls.
	/// </summary>
	public static class FakeServer
	{
		private static readonly TimeSpan TokenTimeToLive = TimeSpan.FromMinutes(30);
		private static readonly Dictionary<string, DateTime> TokenExpirations = new Dictionary<string, DateTime>(StringComparer.Ordinal);

		private static readonly Dictionary<string, Func<string, string>> DefaultResponses = new Dictionary<string, Func<string, string>>(StringComparer.Ordinal)
		{
			{ BuildKey("GET", "/health"), _ => "{\"status\":\"ok\"}" },
			{ BuildKey("GET", "/version"), _ => "{\"version\":\"0.0.1\"}" },
			{ BuildKey("POST", "/login"), BuildLoginResponse },
			{ BuildKey("POST", "/token/validate"), BuildTokenValidationResponse }
		};

		private static readonly Dictionary<string, Func<string, string>> Responses = CloneDefaults();

		/// <summary>
		/// Attempts to resolve a fake response for the given request.
		/// </summary>
		/// <param name="method">HTTP method (GET/POST).</param>
		/// <param name="url">Request URL or relative path.</param>
		/// <param name="jsonPayload">Optional JSON payload.</param>
		/// <param name="response">Resolved fake response.</param>
		/// <returns>True when a specific fake response was found.</returns>
		public static bool TryGetResponse(string method, string url, string jsonPayload, out string response)
		{
			var key = BuildKey(method, url);
			if (Responses.TryGetValue(key, out var handler) && handler != null)
			{
				response = handler(jsonPayload);
				return true;
			}

			response = GetDefaultResponse(method, jsonPayload);
			return false;
		}

		/// <summary>
		/// Registers or replaces a fake response handler for a method/path.
		/// </summary>
		/// <param name="method">HTTP method (GET/POST).</param>
		/// <param name="path">Absolute path (e.g. /login).</param>
		/// <param name="handler">Handler producing response JSON.</param>
		public static void Register(string method, string path, Func<string, string> handler)
		{
			if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(path))
			{
				return;
			}

			Responses[BuildKey(method, path)] = handler;
		}

		/// <summary>
		/// Resets all fake responses back to the defaults.
		/// </summary>
		public static void ResetToDefaults()
		{
			Responses.Clear();
			foreach (var pair in DefaultResponses)
			{
				Responses[pair.Key] = pair.Value;
			}

			TokenExpirations.Clear();
		}

		/// <summary>
		/// Builds a normalized fake response key for logging.
		/// </summary>
		/// <param name="method">HTTP method (GET/POST).</param>
		/// <param name="url">Request URL or relative path.</param>
		/// <returns>Normalized key in the form METHOD:/path.</returns>
		public static string BuildKey(string method, string url)
		{
			var normalizedMethod = string.IsNullOrWhiteSpace(method) ? "GET" : method.Trim().ToUpperInvariant();
			var path = GetPathOnly(url);
			return $"{normalizedMethod}:{path}";
		}

		private static string BuildLoginResponse(string jsonPayload)
		{
			var request = ParseLoginPayload(jsonPayload);
			if (request != null && IsValidLogin(request))
			{
				var token = "fake-jwt";
				RegisterToken(token);
				return "{\"Token\":\"fake-jwt\"}";
			}

			return "{\"Error\":\"Invalid credentials\"}";
		}

		private static string BuildTokenValidationResponse(string jsonPayload)
		{
			var request = ParseTokenValidationPayload(jsonPayload);
			if (request == null || string.IsNullOrWhiteSpace(request.Token))
			{
				return SerializeTokenValidationResponse(false, "Missing token");
			}

			if (!IsTokenKnown(request.Token))
			{
				return SerializeTokenValidationResponse(false, "Invalid token");
			}

			if (IsTokenExpired(request.Token))
			{
				return SerializeTokenValidationResponse(false, "Token expired");
			}

			return SerializeTokenValidationResponse(true, null);
		}

		private static string GetDefaultResponse(string method, string jsonPayload)
		{
			var normalizedMethod = string.IsNullOrWhiteSpace(method) ? "GET" : method.Trim().ToUpperInvariant();
			return normalizedMethod == "POST" ? (jsonPayload ?? "{}") : "{}";
		}

		private static string GetPathOnly(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
			{
				return "/";
			}

			if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
			{
				return string.IsNullOrWhiteSpace(absolute.AbsolutePath) ? "/" : absolute.AbsolutePath;
			}

			var trimmed = url.Trim();
			var queryIndex = trimmed.IndexOf('?', StringComparison.Ordinal);
			if (queryIndex >= 0)
			{
				trimmed = trimmed.Substring(0, queryIndex);
			}

			if (!trimmed.StartsWith("/", StringComparison.Ordinal))
			{
				trimmed = "/" + trimmed;
			}

			return string.IsNullOrWhiteSpace(trimmed) ? "/" : trimmed;
		}

		private static Dictionary<string, Func<string, string>> CloneDefaults()
		{
			var clone = new Dictionary<string, Func<string, string>>(StringComparer.Ordinal);
			foreach (var pair in DefaultResponses)
			{
				clone[pair.Key] = pair.Value;
			}
			return clone;
		}

		private static LoginPayload ParseLoginPayload(string jsonPayload)
		{
			if (string.IsNullOrWhiteSpace(jsonPayload))
			{
				return null;
			}

			try
			{
				return JsonConvert.DeserializeObject<LoginPayload>(jsonPayload);
			}
			catch
			{
				return null;
			}
		}

		private static TokenValidationPayload ParseTokenValidationPayload(string jsonPayload)
		{
			if (string.IsNullOrWhiteSpace(jsonPayload))
			{
				return null;
			}

			try
			{
				return JsonConvert.DeserializeObject<TokenValidationPayload>(jsonPayload);
			}
			catch
			{
				return null;
			}
		}

		private static bool IsValidLogin(LoginPayload payload)
		{
			var password = !string.IsNullOrWhiteSpace(payload.Password)
				? payload.Password
				: payload.Passwork;

			return string.Equals(payload.Username, "mimi", StringComparison.Ordinal)
				&& string.Equals(password, "123456", StringComparison.Ordinal);
		}

		private static void RegisterToken(string token)
		{
			if (string.IsNullOrWhiteSpace(token))
			{
				return;
			}

			TokenExpirations[token] = DateTime.UtcNow.Add(TokenTimeToLive);
		}

		private static bool IsTokenKnown(string token)
		{
			return TokenExpirations.ContainsKey(token)
				|| string.Equals(token, "fake-jwt", StringComparison.Ordinal)
				|| token.EndsWith("-expired", StringComparison.Ordinal);
		}

		private static bool IsTokenExpired(string token)
		{
			if (string.IsNullOrWhiteSpace(token))
			{
				return true;
			}

			if (token.EndsWith("-expired", StringComparison.Ordinal))
			{
				return true;
			}

			if (TokenExpirations.TryGetValue(token, out var expiresAtUtc))
			{
				return DateTime.UtcNow > expiresAtUtc;
			}

			return false;
		}

		private static string SerializeTokenValidationResponse(bool valid, string error)
		{
			var response = new TokenValidationResponse
			{
				Valid = valid,
				Error = error
			};
			return JsonConvert.SerializeObject(response);
		}

		[Serializable]
		private sealed class LoginPayload
		{
			public string Username;
			public string Password;
			public string Passwork;
		}

		[Serializable]
		private sealed class TokenValidationPayload
		{
			public string Token;
		}

		[Serializable]
		private sealed class TokenValidationResponse
		{
			public bool Valid;
			public string Error;
		}
	}
}
