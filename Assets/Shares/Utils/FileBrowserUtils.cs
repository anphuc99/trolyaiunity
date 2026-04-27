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

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
		public class OpenFileName
		{
			public int structSize = 0;
			public IntPtr dlgOwner = IntPtr.Zero;
			public IntPtr instance = IntPtr.Zero;
			public string filter = null;
			public string customFilter = null;
			public int maxCustFilter = 0;
			public int filterIndex = 0;
			public string file = null;
			public int maxFile = 0;
			public string fileTitle = null;
			public int maxFileTitle = 0;
			public string initialDir = null;
			public string title = null;
			public int flags = 0;
			public short fileOffset = 0;
			public short fileExtension = 0;
			public string defExt = null;
			public IntPtr custData = IntPtr.Zero;
			public IntPtr hook = IntPtr.Zero;
			public string templateName = null;
			public IntPtr reservedPtr = IntPtr.Zero;
			public int reservedInt = 0;
			public int flagsEx = 0;
		}

		[System.Runtime.InteropServices.DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
		public static extern bool GetOpenFileName([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] OpenFileName ofn);

		public static string OpenWindowsFileExplorer(string title)
		{
			var ofn = new OpenFileName();
			ofn.structSize = System.Runtime.InteropServices.Marshal.SizeOf(ofn);
			ofn.filter = "Image Files\0*.png;*.jpg;*.jpeg;*.webp\0All Files\0*.*\0";
			ofn.file = new string(new char[256]);
			ofn.maxFile = ofn.file.Length;
			ofn.fileTitle = new string(new char[64]);
			ofn.maxFileTitle = ofn.fileTitle.Length;
			ofn.title = title;
			// OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR
			ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008;

			if (GetOpenFileName(ofn))
			{
				return ofn.file;
			}
			return string.Empty;
		}
#endif
	}
}
