using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;

namespace EditorTools.UIGenerator
{
    /// <summary>
    /// Custom editor for Image component that adds AI UI Generation button.
    /// </summary>
    [CustomEditor(typeof(Image))]
    [CanEditMultipleObjects]
    public sealed class ImageExtensionEditor : ImageEditor
    {
        private static readonly GUIContent GenUIButtonContent = new GUIContent(
            "Gen UI with AI",
            "Open AI UI Generator window to generate or edit UI images");

        public override void OnInspectorGUI()
        {
            // Draw the default Image inspector
            base.OnInspectorGUI();

            EditorGUILayout.Space(10);

            // Draw separator
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Draw AI Generation section
            EditorGUILayout.LabelField("AI Generation", EditorStyles.boldLabel);

            // Gen UI button
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button(GenUIButtonContent, GUILayout.Height(30)))
            {
                var image = target as Image;
                UIGeneratorWindow.Open(image);
            }
            GUI.backgroundColor = Color.white;

            // Quick actions if sprite exists
            var targetImage = target as Image;
            if (targetImage != null && targetImage.sprite != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Edit Current Sprite", GUILayout.Height(25)))
                {
                    var window = EditorWindow.GetWindow<UIGeneratorWindow>("AI UI Generator");
                    // The window will use the current sprite as starting point
                    UIGeneratorWindow.Open(targetImage);
                }

                if (GUILayout.Button("Remove Background", GUILayout.Height(25)))
                {
                    // Open background removal tool with current sprite
                    var bgWindow = EditorWindow.GetWindow<BackgroundRemoval.BackgroundRemovalWindow>("Background Removal Tool");
                    // Note: Would need to add method to set texture in BackgroundRemovalWindow
                }

                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
