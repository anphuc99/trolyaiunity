using System;
using System.Collections;
using System.Reflection;

namespace Share.Utils
{
	/// <summary>
	/// Shared helpers for runtime file browser dialogs (SimpleFileBrowser reflection).
	/// </summary>
	public static class FileBrowserUtils
	{
		/// <summary>
		/// Finds the SimpleFileBrowser.FileBrowser type via reflection.
		/// </summary>
		/// <returns>Type reference, or null if not installed.</returns>
		public static Type FindSimpleFileBrowserType()
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (var i = 0; i < assemblies.Length; i++)
			{
				var assembly = assemblies[i];
				if (assembly == null)
				{
					continue;
				}

				var type = assembly.GetType("SimpleFileBrowser.FileBrowser", false);
				if (type != null)
				{
					return type;
				}
			}

			return null;
		}

		/// <summary>
		/// Opens the SimpleFileBrowser dialog via reflection and invokes a callback with the selected path.
		/// Must be driven by a MonoBehaviour coroutine (yield return from StartCoroutine).
		/// </summary>
		/// <param name="fileBrowserType">Type returned by <see cref="FindSimpleFileBrowserType"/>.</param>
		/// <param name="title">Dialog title text.</param>
		/// <param name="submitLabel">Submit button label.</param>
		/// <param name="onPicked">Callback invoked with selected path (empty on cancel).</param>
		/// <returns>Coroutine enumerator.</returns>
		public static IEnumerator OpenViaSimpleFileBrowserCoroutine(Type fileBrowserType, string title, string submitLabel, Action<string> onPicked)
		{
			if (fileBrowserType == null)
			{
				onPicked?.Invoke(string.Empty);
				yield break;
			}

			var pickModeType = fileBrowserType.GetNestedType("PickMode", BindingFlags.Public);
			if (pickModeType == null)
			{
				onPicked?.Invoke(string.Empty);
				yield break;
			}

			var waitMethod = fileBrowserType.GetMethod(
				"WaitForLoadDialog",
				BindingFlags.Public | BindingFlags.Static,
				null,
				new[] { pickModeType, typeof(bool), typeof(string), typeof(string), typeof(string), typeof(string) },
				null);

			if (waitMethod == null)
			{
				onPicked?.Invoke(string.Empty);
				yield break;
			}

			var pickMode = Enum.Parse(pickModeType, "Files");
			var routine = waitMethod.Invoke(null, new object[] { pickMode, false, null, null, title, submitLabel }) as IEnumerator;
			if (routine != null)
			{
				yield return routine;
			}

			var successProp = fileBrowserType.GetProperty("Success", BindingFlags.Public | BindingFlags.Static);
			var resultProp = fileBrowserType.GetProperty("Result", BindingFlags.Public | BindingFlags.Static);

			var isSuccess = successProp != null && successProp.GetValue(null) is bool value && value;
			if (!isSuccess)
			{
				onPicked?.Invoke(string.Empty);
				yield break;
			}

			var result = resultProp != null ? resultProp.GetValue(null) as string[] : null;
			onPicked?.Invoke(result != null && result.Length > 0 ? result[0] : string.Empty);
		}
	}
}
