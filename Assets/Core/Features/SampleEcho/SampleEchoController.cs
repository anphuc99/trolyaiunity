using Core.Features.SampleEcho;
using Core.Infrastructure.Attributes;
using Core.Infrastructure.Events;

namespace Core.Features.SampleEcho
{
	/// <summary>
	/// Sample controller demonstrating View -> RequestController -> Controller -> EventBus -> View.
	/// Pure C# (no Unity dependency) and stateless.
	/// </summary>
	public static class SampleEchoController
	{
		/// <summary>
		/// Echoes the incoming payload back to views via <see cref="EventBus"/>.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(SampleEchoKeys.RequestEcho)]
		public static void Echo(object payload)
		{
			EventBus.Publish(SampleEchoKeys.EventEchoed, payload);
		}
	}
}
