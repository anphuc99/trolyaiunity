using System;

namespace Core.Infrastructure.Attributes
{
	/// <summary>
	/// Marks a static controller method to be called when its scope is entered.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public sealed class ControllerInitAttribute : Attribute
	{
	}
}
