using System;

namespace Core.Infrastructure.Attributes
{
	/// <summary>
	/// Marks a static controller method as a request endpoint, invokable via a string key.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public class RequestAttribute : Attribute
	{
		/// <summary>
		/// Gets the request key used for routing.
		/// </summary>
		public string Key { get; }

		/// <summary>
		/// Creates a new request attribute.
		/// </summary>
		/// <param name="key">Unique routing key for the request.</param>
		/// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
		public RequestAttribute(string key)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				throw new ArgumentException("Request key cannot be null or whitespace.", nameof(key));
			}

			Key = key;
		}
	}
}
