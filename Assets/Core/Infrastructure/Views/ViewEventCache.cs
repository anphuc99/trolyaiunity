using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Infrastructure.Attributes;
using UnityEngine;

namespace Core.Infrastructure.Views
{
	/// <summary>
	/// Caches per-view-type event handler metadata discovered via <see cref="OnEventAttribute"/>.
	/// Reflection is performed once per view type.
	/// </summary>
	public static class ViewEventCache
	{
		private const string LogPrefix = "[ViewEventCache]";

		private static readonly object Sync = new object();
		private static readonly Dictionary<Type, ViewEventDescriptor[]> Cache = new Dictionary<Type, ViewEventDescriptor[]>();

		/// <summary>
		/// Gets cached descriptors for a view type.
		/// </summary>
		/// <param name="viewType">Concrete view type.</param>
		/// <returns>Array of descriptors; empty if none.</returns>
		public static ViewEventDescriptor[] GetOrCreate(Type viewType)
		{
			if (viewType == null)
			{
				return Array.Empty<ViewEventDescriptor>();
			}

			lock (Sync)
			{
				if (Cache.TryGetValue(viewType, out var existing))
				{
					return existing;
				}

				var created = BuildDescriptors(viewType);
				Cache[viewType] = created;
				return created;
			}
		}

		private static ViewEventDescriptor[] BuildDescriptors(Type viewType)
		{
			try
			{
				var list = new List<ViewEventDescriptor>();
				var methods = viewType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

				for (var i = 0; i < methods.Length; i++)
				{
					var method = methods[i];
					if (method == null)
					{
						continue;
					}

					var attributes = method.GetCustomAttributes<OnEventAttribute>(inherit: true);
					foreach (var attribute in attributes)
					{
						if (attribute == null)
						{
							continue;
						}

						var descriptor = TryCreateDescriptor(attribute.Key, method);
						if (descriptor.HasValue)
						{
							list.Add(descriptor.Value);
						}
					}
				}

				return list.Count == 0 ? Array.Empty<ViewEventDescriptor>() : list.ToArray();
			}
			catch (Exception ex)
			{
				Debug.LogError($"{LogPrefix} Failed building descriptors for type {viewType.FullName}: {ex}");
				return Array.Empty<ViewEventDescriptor>();
			}
		}

		private static ViewEventDescriptor? TryCreateDescriptor(string key, MethodInfo method)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				Debug.LogError($"{LogPrefix} Ignoring [OnEvent] with null/empty key on {Describe(method)}.");
				return null;
			}

			if (method.ReturnType != typeof(void))
			{
				Debug.LogError($"{LogPrefix} Ignoring handler for '{key}': method must return void ({Describe(method)}).");
				return null;
			}

			if (method.IsGenericMethodDefinition)
			{
				Debug.LogError($"{LogPrefix} Ignoring handler for '{key}': generic methods are not supported ({Describe(method)}).");
				return null;
			}

			var parameters = method.GetParameters();
			if (parameters.Length > 1)
			{
				Debug.LogError($"{LogPrefix} Ignoring handler for '{key}': method must have 0 or 1 parameter ({Describe(method)}).");
				return null;
			}

			Type parameterType = null;
			if (parameters.Length == 1)
			{
				if (parameters[0].ParameterType.IsByRef)
				{
					Debug.LogError($"{LogPrefix} Ignoring handler for '{key}': ref/out parameters are not supported ({Describe(method)}).");
					return null;
				}

				parameterType = parameters[0].ParameterType;
			}

			return new ViewEventDescriptor(key, method, parameterType);
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

	/// <summary>
	/// Immutable metadata about a view event handler method.
	/// </summary>
	public readonly struct ViewEventDescriptor
	{
		/// <summary>
		/// Event key this method handles.
		/// </summary>
		public readonly string Key;

		/// <summary>
		/// Target method.
		/// </summary>
		public readonly MethodInfo Method;

		/// <summary>
		/// Optional payload type (null if the method takes no parameters).
		/// </summary>
		public readonly Type ParameterType;

		public ViewEventDescriptor(string key, MethodInfo method, Type parameterType)
		{
			Key = key;
			Method = method;
			ParameterType = parameterType;
		}
	}
}
