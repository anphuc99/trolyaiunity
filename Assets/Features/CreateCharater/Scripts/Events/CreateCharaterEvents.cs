using System;

namespace Features.CreateCharater.Events
{
	/// <summary>
	/// Event keys for this feature.
	/// </summary>
	public static class CreateCharaterEvents
	{
		public const string Echoed = "create.charater.echo.event";
		public const string PersonalitiesLoaded = "create.charater.personalities_loaded.event";
		public const string CharacterCreationSucceeded = "create.charater.creation_succeeded.event";
		public const string CharacterCreationFailed = "create.charater.creation_failed.event";
	}
}
