using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Setting.Events;
using Features.GamePlay.SubFeatures.Setting.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Setting.Requests;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Setting.View
{
	/// <summary>
	/// View for Setting.
	/// </summary>
	public sealed class SettingView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(SettingRequests.Echo, _message);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(SettingEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[SettingView] Echoed: " + payload, this);
		}
	}
}
