namespace Features.Login.Model
{
	/// <summary>
	/// Data model for Login. Stores cached login state/data.
	/// </summary>
	public sealed class LoginModel
	{
		/// <summary>The username or email entered by the user.</summary>
		public string Username { get; set; }

		/// <summary>Whether the user is currently authenticated.</summary>
		public bool IsAuthenticated { get; set; }

		/// <summary>The social login provider used, if any (e.g. "google", "facebook", "apple").</summary>
		public string SocialProvider { get; set; }
	}
}
