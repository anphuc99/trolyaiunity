using System;

namespace Features.EditCharacter.Requests
{
	/// <summary>
	/// Request keys for this feature.
	/// </summary>
	public static class EditCharacterRequests
	{
		public const string Echo = "edit.character.echo.request";
		public const string FetchVoices = "edit.character.fetch.voices.request";
		public const string LoadSelectedCharacter = "edit.character.load.selected.character.request";
		public const string SubmitCharacter = "edit.character.submit.character.request";
		public const string UploadAvatar = "edit.character.upload.avatar.request";
		public const string CloseScope = "edit.character.close.scope.request";
		public const string OpenScope = "character.info.open.edit.scope.request";
	}
}
