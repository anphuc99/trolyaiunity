using Core.Infrastructure.Requests;

namespace Features.GamePlay.Infrastructure
{
	/// <summary>
	/// Feature-level request gateway that forwards to core infrastructure.
	/// </summary>
	public static class RequestController
	{
		/// <summary>
		/// Executes a request by key.
		/// </summary>
		/// <param name="key">Request key.</param>
		/// <param name="payload">Optional payload.</param>
		public static void Execute(string key, object payload = null)
		{
			Core.Infrastructure.Requests.RequestController.Execute(key, payload);
		}

		/// <summary>
		/// Executes a request by key and returns a typed result.
		/// </summary>
		/// <typeparam name="T">Expected return type.</typeparam>
		/// <param name="key">Request key.</param>
		/// <param name="payload">Optional payload.</param>
		/// <returns>The typed result or default when unavailable/mismatched.</returns>
		public static T Execute<T>(string key, object payload = null)
		{
			return Core.Infrastructure.Requests.RequestController.Execute<T>(key, payload);
		}

		/// <summary>
		/// Ensures request bindings are initialized.
		/// </summary>
		public static void Initialize()
		{
			Core.Infrastructure.Requests.RequestController.Initialize();
		}
	}
}
