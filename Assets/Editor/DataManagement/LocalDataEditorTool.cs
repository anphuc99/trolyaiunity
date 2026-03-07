using UnityEditor;
using UnityEngine;

namespace Core.Editor.DataManagement
{
	/// <summary>
	/// Editor tool to manage local persistent data.
	/// </summary>
	public static class LocalDataEditorTool
	{
		/// <summary>
		/// Clears all PlayerPrefs data.
		/// </summary>
		[MenuItem("Tools/Data Management/Clear All PlayerPrefs")]
		public static void ClearAllPlayerPrefs()
		{
			if (EditorUtility.DisplayDialog("Clear All PlayerPrefs",
				"Are you sure you want to delete all PlayerPrefs? This cannot be undone.",
				"Yes", "No"))
			{
				PlayerPrefs.DeleteAll();
				PlayerPrefs.Save();
				Debug.Log("[LocalDataEditorTool] All PlayerPrefs have been cleared.");
			}
		}
	}
}
