using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Core.Infrastructure.Events
{
	/// <summary>
	/// Unity-independent publish/subscribe message bus.
	/// Controllers publish events; Views receive them via attribute-based binding.
	/// </summary>
	public static class EventBus
	{
		private const string DefaultLogPrefix = "[EventBus]";

		private static readonly object Sync = new object();
		private static readonly Dictionary<string, List<Action<object>>> HandlersByKey = new Dictionary<string, List<Action<object>>>(StringComparer.Ordinal);

		/// <summary>
		/// Optional error logger. If unset, errors are written to <see cref="Trace"/>.
		/// </summary>
		public static Action<string> LogError { get; set; } = message => Trace.TraceError(message);

		/// <summary>
		/// Publishes an event.
		/// </summary>
		/// <param name="key">Event key.</param>
		/// <param name="payload">Optional payload.</param>
		public static void Publish(string key, object payload = null)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				LogError?.Invoke($"{DefaultLogPrefix} Publish failed: key is null/empty.");
				return;
			}

			Action<object>[] snapshot;
			lock (Sync)
			{
				if (!HandlersByKey.TryGetValue(key, out var handlers) || handlers.Count == 0)
				{
					return;
				}

				snapshot = handlers.ToArray();
			}

			for (var i = 0; i < snapshot.Length; i++)
			{
				var handler = snapshot[i];
				if (handler == null)
				{
					continue;
				}

				try
				{
					handler(payload);
				}
				catch (Exception ex)
				{
					LogError?.Invoke($"{DefaultLogPrefix} Handler threw for key '{key}': {ex}");
				}
			}
		}

		/// <summary>
		/// Subscribes a handler to an event key.
		/// </summary>
		/// <param name="key">Event key.</param>
		/// <param name="handler">Handler to invoke.</param>
		public static void Subscribe(string key, Action<object> handler)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				LogError?.Invoke($"{DefaultLogPrefix} Subscribe failed: key is null/empty.");
				return;
			}

			if (handler == null)
			{
				LogError?.Invoke($"{DefaultLogPrefix} Subscribe failed: handler is null for key '{key}'.");
				return;
			}

			lock (Sync)
			{
				if (!HandlersByKey.TryGetValue(key, out var handlers))
				{
					handlers = new List<Action<object>>();
					HandlersByKey[key] = handlers;
				}

				handlers.Add(handler);
			}
		}

		/// <summary>
		/// Unsubscribes a handler from an event key.
		/// </summary>
		/// <param name="key">Event key.</param>
		/// <param name="handler">Handler to remove.</param>
		public static void Unsubscribe(string key, Action<object> handler)
		{
			if (string.IsNullOrWhiteSpace(key) || handler == null)
			{
				return;
			}

			lock (Sync)
			{
				if (!HandlersByKey.TryGetValue(key, out var handlers) || handlers.Count == 0)
				{
					return;
				}

				for (var i = handlers.Count - 1; i >= 0; i--)
				{
					if (ReferenceEquals(handlers[i], handler))
					{
						handlers.RemoveAt(i);
						break;
					}
				}

				if (handlers.Count == 0)
				{
					HandlersByKey.Remove(key);
				}
			}
		}

		/// <summary>
		/// Removes all handlers for all keys.
		/// Intended for test environments.
		/// </summary>
		public static void ClearAll()
		{
			lock (Sync)
			{
				HandlersByKey.Clear();
			}
		}
	}
}
