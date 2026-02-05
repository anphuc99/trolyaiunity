using Core.Features.SampleEcho;
using Core.Infrastructure.Attributes;
using Core.Infrastructure.Views;
using UnityEngine;

namespace Core.Features.SampleEcho
{
	/// <summary>
	/// Sample view that sends a request and receives an echoed event.
	/// No manual event subscription is allowed; binding is attribute-based via <see cref="BaseView"/>.
	/// </summary>
	public sealed class SampleEchoView : BaseView
	{
		[SerializeField]
		private string _message = "Hello from SampleEchoView";

		/// <summary>
		/// Sends the echo request.
		/// Can be wired from UI Button OnClick in the Inspector.
		/// </summary>
		public void SendEchoRequest()
		{
			SendRequest(SampleEchoKeys.RequestEcho, _message);
		}

		/// <summary>
		/// Receives the echoed payload.
		/// </summary>
		/// <param name="payload">Echoed payload (may be null).</param>
		[OnEvent(SampleEchoKeys.EventEchoed)]
		private void OnEchoed(object payload)
		{
			var text = payload != null ? payload.ToString() : "<null>";
			Debug.Log($"[SampleEchoView] Echoed: {text}", this);
		}
	}
}
