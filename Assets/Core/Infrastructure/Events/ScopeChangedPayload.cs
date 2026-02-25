using Core.Infrastructure.Attributes;

namespace Core.Infrastructure.Events
{
	/// <summary>
	/// Describes a scope lifecycle change.
	/// </summary>
	public sealed class ScopeChangedPayload
	{
		/// <summary>
		/// Scope that changed state.
		/// </summary>
		public ControllerScopeKey ScopeKey { get; set; }

		/// <summary>
		/// True when a scope is opened (activated), false when closed (deactivated).
		/// </summary>
		public bool IsOpened { get; set; }
	}
}
