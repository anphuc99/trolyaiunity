namespace Core.Features.SampleEcho
{
	/// <summary>
	/// String keys for the SampleEcho feature.
	/// Keeps request/event routing keys centralized to avoid magic strings.
	/// </summary>
	public static class SampleEchoKeys
	{
		/// <summary>
		/// View -> Controller request key.
		/// Payload: any object (optional).
		/// </summary>
		public const string RequestEcho = "sample.echo.request";

		/// <summary>
		/// Controller -> View event key.
		/// Payload: any object (optional).
		/// </summary>
		public const string EventEchoed = "sample.echo.event";
	}
}
