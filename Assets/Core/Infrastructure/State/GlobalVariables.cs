using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Core.Infrastructure.Attributes;

namespace Core.Infrastructure.State
{
	/// <summary>
	/// Provides a cross-scope global key-value store.
	/// Any scope can read values, but only controller code is allowed to mutate values.
	/// </summary>
	public static class GlobalVariables
	{
		private static readonly Dictionary<string, object> ValuesByKey = new Dictionary<string, object>(StringComparer.Ordinal);
		private static readonly object Sync = new object();

		/// <summary>
		/// Stores or replaces a value for the specified key.
		/// </summary>
		/// <param name="key">Global value key.</param>
		/// <param name="value">Value to store. Can be null.</param>
		/// <exception cref="ArgumentException">Thrown when key is null/empty.</exception>
		/// <exception cref="InvalidOperationException">Thrown when caller is not a controller.</exception>
		public static void Set(string key, object value)
		{
			ValidateKey(key);
			EnsureControllerCallerCanMutate();

			lock (Sync)
			{
				ValuesByKey[key] = value;
			}
		}

		/// <summary>
		/// Tries to remove a value by key.
		/// </summary>
		/// <param name="key">Global value key.</param>
		/// <returns>True when a value existed and was removed.</returns>
		/// <exception cref="ArgumentException">Thrown when key is null/empty.</exception>
		/// <exception cref="InvalidOperationException">Thrown when caller is not a controller.</exception>
		public static bool Remove(string key)
		{
			ValidateKey(key);
			EnsureControllerCallerCanMutate();

			lock (Sync)
			{
				return ValuesByKey.Remove(key);
			}
		}

		/// <summary>
		/// Clears all global values.
		/// </summary>
		/// <exception cref="InvalidOperationException">Thrown when caller is not a controller.</exception>
		public static void Clear()
		{
			EnsureControllerCallerCanMutate();

			lock (Sync)
			{
				ValuesByKey.Clear();
			}
		}

		/// <summary>
		/// Checks whether the specified key exists.
		/// </summary>
		/// <param name="key">Global value key.</param>
		/// <returns>True when the key exists.</returns>
		/// <exception cref="ArgumentException">Thrown when key is null/empty.</exception>
		public static bool Contains(string key)
		{
			ValidateKey(key);

			lock (Sync)
			{
				return ValuesByKey.ContainsKey(key);
			}
		}

		/// <summary>
		/// Tries to read a typed value by key.
		/// </summary>
		/// <typeparam name="T">Expected value type.</typeparam>
		/// <param name="key">Global value key.</param>
		/// <param name="value">Resolved typed value when found and type-compatible.</param>
		/// <returns>True when key exists and value is type-compatible.</returns>
		/// <exception cref="ArgumentException">Thrown when key is null/empty.</exception>
		public static bool TryGet<T>(string key, out T value)
		{
			ValidateKey(key);

			lock (Sync)
			{
				if (ValuesByKey.TryGetValue(key, out var raw) && raw is T typed)
				{
					value = typed;
					return true;
				}
			}

			value = default;
			return false;
		}

		/// <summary>
		/// Reads a typed value by key, or returns the fallback when missing or type-mismatched.
		/// </summary>
		/// <typeparam name="T">Expected value type.</typeparam>
		/// <param name="key">Global value key.</param>
		/// <param name="fallback">Fallback value when key is missing or incompatible.</param>
		/// <returns>Stored typed value or fallback.</returns>
		/// <exception cref="ArgumentException">Thrown when key is null/empty.</exception>
		public static T GetOrDefault<T>(string key, T fallback = default)
		{
			return TryGet<T>(key, out var value) ? value : fallback;
		}

		private static void ValidateKey(string key)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				throw new ArgumentException("GlobalVariables key must not be null/empty.", nameof(key));
			}
		}

		private static void EnsureControllerCallerCanMutate()
		{
			if (HasControllerInCallStack())
			{
				return;
			}

			throw new InvalidOperationException("Only Controller classes can mutate GlobalVariables.");
		}

		private static bool HasControllerInCallStack()
		{
			var trace = new StackTrace(skipFrames: 2, fNeedFileInfo: false);
			var frames = trace.GetFrames();
			if (frames == null)
			{
				return false;
			}

			for (var index = 0; index < frames.Length; index++)
			{
				var method = frames[index].GetMethod();
				var declaringType = method?.DeclaringType;
				if (declaringType == null || declaringType == typeof(GlobalVariables))
				{
					continue;
				}

				if (IsControllerType(declaringType))
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsControllerType(Type candidate)
		{
			if (!candidate.IsClass || !(candidate.IsAbstract && candidate.IsSealed))
			{
				return false;
			}

			if (candidate.GetCustomAttribute<ControllerScopeAttribute>(inherit: false) != null)
			{
				return true;
			}

			const BindingFlags methodFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
			var methods = candidate.GetMethods(methodFlags);
			for (var index = 0; index < methods.Length; index++)
			{
				var method = methods[index];
				if (method.IsDefined(typeof(RequestAttribute), inherit: false)
					|| method.IsDefined(typeof(ControllerInitAttribute), inherit: false)
					|| method.IsDefined(typeof(ControllerShutdownAttribute), inherit: false))
				{
					return true;
				}
			}

			return false;
		}
	}
}