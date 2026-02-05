using System;

namespace Features.UserPlayer6.Infrastructure.Attributes
{
	/// <summary>
	/// Feature-level request attribute that forwards to core infrastructure.
	/// </summary>
	public sealed class RequestAttribute : Core.Infrastructure.Attributes.RequestAttribute
	{
		public RequestAttribute(string key) : base(key)
		{
		}
	}
}
