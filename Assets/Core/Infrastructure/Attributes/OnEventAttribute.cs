using System;

namespace Core.Infrastructure.Attributes
{
	/// <summary>
	/// Marks a view instance method as an EventBus handler, bound automatically by <see cref="Core.Infrastructure.Views.ViewEventBinder"/>.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
	public class OnEventAttribute : Attribute
	{
		/// <summary>
		/// Gets the event key to bind.
		/// </summary>
		public string Key { get; }

		/// <summary>
		/// Creates a new event binding attribute.
		/// </summary>
		/// <param name="key">Event key to subscribe to.</param>
		/// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
		public OnEventAttribute(string key)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				throw new ArgumentException("Event key cannot be null or whitespace.", nameof(key));
			}

			Key = key;
		}
	}
}
