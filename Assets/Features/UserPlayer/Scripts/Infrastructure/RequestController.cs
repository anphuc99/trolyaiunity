using Core.Infrastructure.Requests;

namespace Features.UserPlayer.Infrastructure
{
	/// <summary>
	/// Feature-level request gateway that forwards to core infrastructure.
	/// </summary>
	public static class RequestController
	{
		public static void Execute(string key, object payload = null)
		{
			Core.Infrastructure.Requests.RequestController.Execute(key, payload);
		}

		public static void Initialize()
		{
			Core.Infrastructure.Requests.RequestController.Initialize();
		}
	}
}
