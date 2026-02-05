using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Infrastructure.Events;
using UnityEngine;

namespace Core.Infrastructure.Views
{
	/// <summary>
	/// Binds and unbinds a view instance to EventBus using cached <see cref="ViewEventDescriptor"/> metadata.
	/// </summary>
	public sealed class ViewEventBinder
	{
		private const string LogPrefix = "[ViewEventBinder]";

		private readonly MonoBehaviour _view;
		private readonly List<Subscription> _subscriptions = new List<Subscription>();
		private bool _prepared;

		/// <summary>
		/// Creates a binder for the specified view instance.
		/// </summary>
		/// <param name="view">View instance to bind.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="view"/> is null.</exception>
		public ViewEventBinder(MonoBehaviour view)
		{
			_view = view != null ? view : throw new ArgumentNullException(nameof(view));
		}

		/// <summary>
		/// Subscribes all handlers declared via <see cref="Core.Infrastructure.Attributes.OnEventAttribute"/>.
		/// Idempotent.
		/// </summary>
		public void Bind()
		{
			if (_view == null)
			{
				return;
			}

			if (!_prepared)
			{
				PrepareSubscriptions();
				_prepared = true;
			}

			for (var i = 0; i < _subscriptions.Count; i++)
			{
				var subscription = _subscriptions[i];
				if (!subscription.IsActive)
				{
					EventBus.Subscribe(subscription.Key, subscription.Handler);
					subscription.IsActive = true;
					_subscriptions[i] = subscription;
				}
			}
		}

		/// <summary>
		/// Unsubscribes all handlers.
		/// Idempotent.
		/// </summary>
		public void Unbind()
		{
			for (var i = 0; i < _subscriptions.Count; i++)
			{
				var subscription = _subscriptions[i];
				if (subscription.IsActive)
				{
					EventBus.Unsubscribe(subscription.Key, subscription.Handler);
					subscription.IsActive = false;
					_subscriptions[i] = subscription;
				}
			}
		}

		private void PrepareSubscriptions()
		{
			_subscriptions.Clear();

			var descriptors = ViewEventCache.GetOrCreate(_view.GetType());
			if (descriptors.Length == 0)
			{
				return;
			}

			for (var i = 0; i < descriptors.Length; i++)
			{
				var descriptor = descriptors[i];
				var handler = CreateHandler(descriptor);
				if (handler == null)
				{
					continue;
				}

				_subscriptions.Add(new Subscription(descriptor.Key, handler));
			}
		}

		private Action<object> CreateHandler(ViewEventDescriptor descriptor)
		{
			if (descriptor.Method == null)
			{
				Debug.LogError($"{LogPrefix} Invalid descriptor: method is null.");
				return null;
			}

			if (string.IsNullOrWhiteSpace(descriptor.Key))
			{
				Debug.LogError($"{LogPrefix} Invalid descriptor: key is null/empty on {_view.GetType().FullName}.");
				return null;
			}

			try
			{
				if (descriptor.ParameterType == null)
				{
					var action = (Action)descriptor.Method.CreateDelegate(typeof(Action), _view);
					return _ => action();
				}

				var delegateType = typeof(Action<>).MakeGenericType(descriptor.ParameterType);
				var typedDelegate = descriptor.Method.CreateDelegate(delegateType, _view);
				return CreateTypedHandler(descriptor.Key, descriptor.ParameterType, typedDelegate);
			}
			catch (Exception ex)
			{
				Debug.LogError($"{LogPrefix} Failed binding handler for '{descriptor.Key}' on {_view.GetType().FullName}: {ex}");
				return null;
			}
		}

		private static Action<object> CreateTypedHandler(string key, Type parameterType, Delegate typedDelegate)
		{
			try
			{
				var factory = typeof(ViewEventBinder).GetMethod(nameof(CreateTypedHandlerGeneric), BindingFlags.Static | BindingFlags.NonPublic);
				var genericFactory = factory.MakeGenericMethod(parameterType);
				return (Action<object>)genericFactory.Invoke(null, new object[] { key, typedDelegate });
			}
			catch (Exception ex)
			{
				Debug.LogError($"{LogPrefix} Failed to build handler for '{key}' (param {parameterType}): {ex}");
				return null;
			}
		}

		private static Action<object> CreateTypedHandlerGeneric<T>(string key, Delegate typedDelegate)
		{
			var action = (Action<T>)typedDelegate;
			var isValueType = typeof(T).IsValueType;
			var isNullableValueType = Nullable.GetUnderlyingType(typeof(T)) != null;

			return payload =>
			{
				if (payload == null)
				{
					if (isValueType && !isNullableValueType)
					{
						Debug.LogError($"{LogPrefix} Event '{key}' expected payload of type {typeof(T).FullName} but got null.");
						return;
					}

					action(default);
					return;
				}

				if (payload is T typed)
				{
					action(typed);
					return;
				}

				Debug.LogError($"{LogPrefix} Event '{key}' payload type mismatch. Expected {typeof(T).FullName}, got {payload.GetType().FullName}.");
			};
		}

		private struct Subscription
		{
			public readonly string Key;
			public readonly Action<object> Handler;
			public bool IsActive;

			public Subscription(string key, Action<object> handler)
			{
				Key = key;
				Handler = handler;
				IsActive = false;
			}
		}
	}
}
