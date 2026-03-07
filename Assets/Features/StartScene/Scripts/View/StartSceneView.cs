using Core.Infrastructure.Views;
using Features.StartScene.Events;
using Features.StartScene.Infrastructure.Attributes;
using Features.StartScene.Requests;
using UnityEngine;

namespace Features.StartScene.View
{
	/// <summary>
	/// View for StartScene.
	/// </summary>
	public sealed class StartSceneView : BaseView
	{
		[SerializeField]
		private string _message = "Hello";

		/// <summary>
		/// Example method to send a request.
		/// </summary>
		public void SendEcho()
		{
			SendRequest(StartSceneRequests.Echo, _message);
		}

		/// <summary>
		/// Called after this view is enabled and scope is active.
		/// </summary>
		protected override void OnEnabled()
		{
			SendRequest(StartSceneRequests.CheckToken);
		}

		/// <summary>
		/// Example event handler (auto-bound).
		/// </summary>
		/// <param name="payload">Payload from controller.</param>
		[OnEvent(StartSceneEvents.Echoed)]
		private void OnEchoed(object payload)
		{
			Debug.Log("[StartSceneView] Echoed: " + payload, this);
		}

		/// <summary>
		/// Handles accepted token events.
		/// </summary>
		/// <param name="payload">Token string.</param>
		[OnEvent(StartSceneEvents.TokenAccepted)]
		private void OnTokenAccepted(object payload)
		{
			Debug.Log("[StartSceneView] Token accepted: " + payload, this);
		}

		/// <summary>
		/// Handles rejected token events.
		/// </summary>
		/// <param name="payload">Error message.</param>
		[OnEvent(StartSceneEvents.TokenRejected)]
		private void OnTokenRejected(object payload)
		{
			Debug.LogWarning("[StartSceneView] Token rejected: " + payload, this);
		}
	}
}
