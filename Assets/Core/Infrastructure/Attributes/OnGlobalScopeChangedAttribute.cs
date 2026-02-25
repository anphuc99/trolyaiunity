using System;

namespace Core.Infrastructure.Attributes
{
	/// <summary>
	/// Marks a static controller method to receive global notifications when any controller scope is activated or deactivated.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public sealed class OnGlobalScopeChangedAttribute : Attribute
	{
	}
}
