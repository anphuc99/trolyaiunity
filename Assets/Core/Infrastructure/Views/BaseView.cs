using Core.Infrastructure.Attributes;
using Core.Infrastructure.Requests;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Infrastructure.Views
{
	/// <summary>
	/// Base class for all Views.
	/// Handles attribute-based event binding and provides a single request gateway to Controllers.
	/// </summary>
	public abstract class BaseView : MonoBehaviour
	{
		private ViewEventBinder _binder;
		private bool _enabledAfterScope;

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
		/// Sends a request to the Controller layer and returns a typed result.
		/// </summary>
		/// <typeparam name="T">Expected return type.</typeparam>
		/// <param name="key">Request key.</param>
		/// <param name="payload">Optional payload.</param>
		/// <returns>The typed result or default when unavailable/mismatched.</returns>
		protected T SendRequest<T>(string key, object payload = null)
		{
			return RequestController.Execute<T>(key, payload);
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

			EnsureScopeActiveForThisViewScene();
			_binder.Bind();
			_enabledAfterScope = true;
			OnEnabled();
		}

		/// <summary>
		/// Called after this view is enabled, after its scene scope is ensured active,
		/// and after event binding is applied.
		/// </summary>
		protected virtual void OnEnabled()
		{
		}

		/// <summary>
		/// Unity OnDisable. Auto-unbinds event handlers.
		/// </summary>
		protected virtual void OnDisable()
		{
			_binder?.Unbind();
			if (_enabledAfterScope)
			{
				OnDisabled();
				_enabledAfterScope = false;
			}
		}

		/// <summary>
		/// Called when disabling a view that previously ran <see cref="OnEnabled"/>.
		/// </summary>
		protected virtual void OnDisabled()
		{
		}

		/// <summary>
		/// Unity OnDestroy. Ensures all subscriptions are released.
		/// </summary>
		protected virtual void OnDestroy()
		{
			_binder?.Unbind();
			_enabledAfterScope = false;
		}

		private void EnsureScopeActiveForThisViewScene()
		{
			var scene = gameObject.scene;
			if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.name))
			{
				return;
			}

			if (!System.Enum.TryParse(scene.name, ignoreCase: false, out ControllerScopeKey scopeKey))
			{
				return;
			}

			RequestController.ActivateScope(scopeKey);
		}
	}
}
