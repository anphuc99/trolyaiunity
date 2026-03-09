namespace Features.GamePlay.Model
{
	/// <summary>
	/// Available GamePlay subcontrollers.
	/// </summary>
	public enum GamePlaySubControllerType
	{
		Character,
		Chat,
		Journal,
		Setting,
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
