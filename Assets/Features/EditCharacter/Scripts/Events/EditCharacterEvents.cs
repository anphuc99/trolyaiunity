using System;

namespace Features.EditCharacter.Events
{
	/// <summary>
	/// Event keys for this feature.
	/// </summary>
	public static class EditCharacterEvents
	{
		public const string Echoed = "edit.character.echo.event";
		public const string VoicesLoaded = "edit.character.voices.loaded.event";
		public const string SelectedCharacterLoaded = "edit.character.selected.character.loaded.event";
		public const string SubmitSucceeded = "edit.character.submit.succeeded.event";
		public const string SubmitFailed = "edit.character.submit.failed.event";
		public const string AvatarUploadSucceeded = "edit.character.avatar.upload.succeeded.event";
		public const string AvatarUploadFailed = "edit.character.avatar.upload.failed.event";
	}
}
