#if UNITY_EDITOR
using Features.GamePlay.SubFeatures.Chat.View;
using UnityEditor;
using UnityEngine;

namespace Editor
{
	/// <summary>
	/// Custom inspector for VirtualizedChatMessageContainer.
	/// </summary>
	[CustomEditor(typeof(VirtualizedChatMessageContainer))]
	public sealed class VirtualizedChatMessageContainerEditor : UnityEditor.Editor
	{
		/// <summary>
		/// Draws default inspector with a cheat action button.
		/// </summary>
		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();
			EditorGUILayout.Space(8f);

			var container = (VirtualizedChatMessageContainer)target;
			if (container == null)
			{
				return;
			}

			if (GUILayout.Button("Cheat Add Message"))
			{
				Undo.RecordObject(container, "Cheat Add Message");
				container.CheatAddMessage();
				EditorUtility.SetDirty(container);
			}
		}
	}
}
#endif
