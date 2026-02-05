using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Infrastructure.Attributes;
using UnityEngine;

namespace Core.Infrastructure.Requests
{
	/// <summary>
	/// Routes View requests (string key + optional payload) to static controller methods marked with <see cref="RequestAttribute"/>.
	/// Scans assemblies once and builds fast invokers; request execution does not use reflection.
	/// </summary>
	public static class RequestController
	{
		private const string LogPrefix = "[RequestController]";

		private static readonly object InitSync = new object();
		private static bool _initialized;

		private static readonly Dictionary<string, Action<object>> InvokersByKey = new Dictionary<string, Action<object>>(StringComparer.Ordinal);

		/// <summary>
		/// Ensures request bindings are initialized.
		/// Safe to call multiple times.
		/// </summary>
		public static void Initialize()
		{
			EnsureInitialized();
		}

		/// <summary>
		/// Executes a request by key.
		/// </summary>
		/// <param name="key">Request key.</param>
		/// <param name="payload">Optional payload.</param>
		public static void Execute(string key, object payload = null)
		{
			EnsureInitialized();

			if (string.IsNullOrWhiteSpace(key))
			{
				Debug.LogError($"{LogPrefix} Execute failed: key is null/empty.");
				return;
			}

			if (!InvokersByKey.TryGetValue(key, out var invoker) || invoker == null)
			{
				Debug.LogError($"{LogPrefix} Unknown request key '{key}'.");
				return;
			}

			try
			{
				invoker(payload);
			}
			catch (Exception ex)
			{
				Debug.LogError($"{LogPrefix} Request '{key}' threw: {ex}");
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void RuntimeInit()
		{
			EnsureInitialized();
		}

		private static void EnsureInitialized()
		{
			if (_initialized)
			{
				return;
			}

			lock (InitSync)
			{
				if (_initialized)
				{
					return;
				}

				BuildBindings();
				_initialized = true;
			}
		}

		private static void BuildBindings()
		{
			InvokersByKey.Clear();

			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (var assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
			{
				var assembly = assemblies[assemblyIndex];
				if (assembly == null)
				{
					continue;
				}

				Type[] types;
				try
				{
					types = assembly.GetTypes();
				}
				catch (ReflectionTypeLoadException ex)
				{
					types = ex.Types;
				}
				catch
				{
					continue;
				}

				if (types == null)
				{
					continue;
				}

				for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
				{
					var type = types[typeIndex];
					if (type == null)
					{
						continue;
					}

					var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					for (var methodIndex = 0; methodIndex < methods.Length; methodIndex++)
					{
						var method = methods[methodIndex];
						if (method == null)
						{
							continue;
						}

						var attribute = method.GetCustomAttribute<RequestAttribute>(inherit: false);
						if (attribute == null)
						{
							continue;
						}

						RegisterMethod(attribute.Key, method);
					}
				}
			}
		}

		private static void RegisterMethod(string key, MethodInfo method)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				Debug.LogError($"{LogPrefix} Ignoring [Request] with null/empty key on {Describe(method)}.");
				return;
			}

			if (method == null)
			{
				Debug.LogError($"{LogPrefix} Ignoring [Request] key '{key}': method is null.");
				return;
			}

			if (!method.IsStatic)
			{
				Debug.LogError($"{LogPrefix} Ignoring request '{key}': method must be static ({Describe(method)}).");
				return;
			}

			if (method.ReturnType != typeof(void))
			{
				Debug.LogError($"{LogPrefix} Ignoring request '{key}': method must return void ({Describe(method)}).");
				return;
			}

			var parameters = method.GetParameters();
			if (parameters.Length > 1)
			{
				Debug.LogError($"{LogPrefix} Ignoring request '{key}': method must have 0 or 1 parameter ({Describe(method)}).");
				return;
			}

			if (InvokersByKey.ContainsKey(key))
			{
				Debug.LogError($"{LogPrefix} Duplicate request key '{key}'. Keeping first registration; ignoring {Describe(method)}.");
				return;
			}

			Action<object> invoker;
			if (parameters.Length == 0)
			{
				invoker = CreateNoArgInvoker(key, method);
			}
			else
			{
				invoker = CreateSingleArgInvoker(key, method, parameters[0]);
			}

			if (invoker == null)
			{
				Debug.LogError($"{LogPrefix} Failed to register request '{key}' for {Describe(method)}.");
				return;
			}

			InvokersByKey[key] = invoker;
		}

		private static Action<object> CreateNoArgInvoker(string key, MethodInfo method)
		{
			try
			{
				var action = (Action)Delegate.CreateDelegate(typeof(Action), method);
				return _ => action();
			}
			catch (Exception ex)
			{
				Debug.LogError($"{LogPrefix} Invalid request '{key}': cannot bind no-arg delegate for {Describe(method)}: {ex}");
				return null;
			}
		}

		private static Action<object> CreateSingleArgInvoker(string key, MethodInfo method, ParameterInfo parameter)
		{
			if (parameter == null)
			{
				Debug.LogError($"{LogPrefix} Invalid request '{key}': parameter info missing for {Describe(method)}.");
				return null;
			}

			if (parameter.ParameterType.IsByRef)
			{
				Debug.LogError($"{LogPrefix} Invalid request '{key}': ref/out parameters are not supported ({Describe(method)}).");
				return null;
			}

			var parameterType = parameter.ParameterType;
			try
			{
				var delegateType = typeof(Action<>).MakeGenericType(parameterType);
				var typedDelegate = Delegate.CreateDelegate(delegateType, method);
				return CreateTypedInvoker(key, parameterType, typedDelegate);
			}
			catch (Exception ex)
			{
				Debug.LogError($"{LogPrefix} Invalid request '{key}': cannot bind single-arg delegate for {Describe(method)}: {ex}");
				return null;
			}
		}

		private static Action<object> CreateTypedInvoker(string key, Type parameterType, Delegate typedDelegate)
		{
			try
			{
				var factory = typeof(RequestController).GetMethod(nameof(CreateTypedInvokerGeneric), BindingFlags.Static | BindingFlags.NonPublic);
				var genericFactory = factory.MakeGenericMethod(parameterType);
				return (Action<object>)genericFactory.Invoke(null, new object[] { key, typedDelegate });
			}
			catch (Exception ex)
			{
				Debug.LogError($"{LogPrefix} Failed to build invoker for request '{key}' (param {parameterType}): {ex}");
				return null;
			}
		}

		private static Action<object> CreateTypedInvokerGeneric<T>(string key, Delegate typedDelegate)
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
						Debug.LogError($"{LogPrefix} Request '{key}' expected payload of type {typeof(T).FullName} but got null.");
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

				Debug.LogError($"{LogPrefix} Request '{key}' payload type mismatch. Expected {typeof(T).FullName}, got {payload.GetType().FullName}.");
			};
		}

		private static string Describe(MethodInfo method)
		{
			if (method == null)
			{
				return "<null method>";
			}

			var declaring = method.DeclaringType != null ? method.DeclaringType.FullName : "<unknown type>";
			return $"{declaring}.{method.Name}";
		}
	}
}
