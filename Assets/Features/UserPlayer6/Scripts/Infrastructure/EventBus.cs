using System;

namespace Features.UserPlayer6.Infrastructure
{
	/// <summary>
	/// Feature-level event bus that forwards to core infrastructure.
	/// </summary>
	public static class EventBus
	{
		public static void Publish(string key, object payload = null)
		{
			Core.Infrastructure.Events.EventBus.Publish(key, payload);
		}

		public static void Subscribe(string key, Action<object> handler)
		{
			Core.Infrastructure.Events.EventBus.Subscribe(key, handler);
		}

		public static void Unsubscribe(string key, Action<object> handler)
		{
			Core.Infrastructure.Events.EventBus.Unsubscribe(key, handler);
		}
	}
}
