namespace Core.Infrastructure.Attributes
{
	/// <summary>
	/// Defines controller scope keys used for scene-based activation.
	/// Scene names should match these enum values for automatic mapping.
	/// </summary>
	public enum ControllerScopeKey
	{
		Global = 0,
		PlayerGameplay = 1,
		UserPlayerGameplay = 2
	}
}
