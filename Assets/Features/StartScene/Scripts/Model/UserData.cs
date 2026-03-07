namespace Features.StartScene.Model
{
	/// <summary>
	/// Basic user information returned by the server.
	/// </summary>
	[System.Serializable]
	public sealed class UserData
	{
		public int id;
		public string username;
		public int role;
		// Add other fields as needed
	}
}
