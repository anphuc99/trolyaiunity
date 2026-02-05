using Core.Infrastructure.Requests;
using UnityEngine;

namespace Core.Infrastructure.Views
{
	/// <summary>
	/// Base class for all Views.
	/// Handles attribute-based event binding and provides a single request gateway to Controllers.
	/// </summary>
	public abstract class BaseView : MonoBehaviour
	{
		private ViewEventBinder _binder;

		/// <summary>
		/// Sends a request to the Controller layer via string key.
		/// </summary>
		/// <param name="key">Request key.</param>
		/// <param name="payload">Optional payload.</param>
		protected void SendRequest(string key, object payload = null)
		{
			RequestController.Execute(key, payload);
		}

		/// <summary>
		/// Unity Awake. Initializes binder.
		/// </summary>
		protected virtual void Awake()
		{
			_binder = new ViewEventBinder(this);
		}

		/// <summary>
		/// Unity OnEnable. Auto-binds event handlers.
		/// </summary>
		protected virtual void OnEnable()
		{
			if (_binder == null)
			{
				_binder = new ViewEventBinder(this);
			}

			_binder.Bind();
		}

		/// <summary>
		/// Unity OnDisable. Auto-unbinds event handlers.
		/// </summary>
		protected virtual void OnDisable()
		{
			_binder?.Unbind();
		}

		/// <summary>
		/// Unity OnDestroy. Ensures all subscriptions are released.
		/// </summary>
		protected virtual void OnDestroy()
		{
			_binder?.Unbind();
		}
	}
}
