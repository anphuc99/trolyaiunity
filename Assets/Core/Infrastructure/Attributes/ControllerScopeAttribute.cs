using System;

namespace Core.Infrastructure.Attributes
{
	/// <summary>
	/// Defines the scope key for a controller class.
	/// Controllers are activated when a matching scope is entered.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class ControllerScopeAttribute : Attribute
	{
		/// <summary>
		/// Gets the scope key.
		/// </summary>
		public ControllerScopeKey ScopeKey { get; }

		/// <summary>
		/// Creates a new controller scope attribute.
		/// </summary>
		/// <param name="scopeKey">Scope key used to activate the controller.</param>
		public ControllerScopeAttribute(ControllerScopeKey scopeKey)
		{
			ScopeKey = scopeKey;
		}
	}
}
