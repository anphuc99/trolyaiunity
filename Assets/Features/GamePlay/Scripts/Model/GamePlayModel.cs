namespace Features.GamePlay.Model
{
	/// <summary>
	/// Available GamePlay subcontrollers.
	/// </summary>
	public enum GamePlaySubControllerType
	{
		Home,
		Character,
		Chat,
		Journal,
		Practice,
		Story,
		Task,
		Setting,
		MyLog,
		LearningPath
	}

	/// <summary>
	/// Data model for GamePlay.
	/// </summary>
	public sealed class GamePlayModel
	{
		/// <summary>
		/// Example data property.
		/// </summary>
		public string Value { get; set; }
	}
}
