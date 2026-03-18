using System;

namespace Features.CreateCharater.Requests
{
	/// <summary>
	/// Request keys for this feature.
	/// </summary>
	public static class CreateCharaterRequests
	{
		public const string Echo = "create.charater.echo.request";
		public const string FetchPersonalities = "create.charater.fetch_personalities.request";
		public const string FetchVoices = "create.charater.fetch.voices.request";
		public const string SubmitCharacter = "create.charater.submit.request";
		public const string UploadAvatar = "create.charater.upload_avatar.request";
		public const string CloseScope = "create.charater.close.scope.request";
	}
}
