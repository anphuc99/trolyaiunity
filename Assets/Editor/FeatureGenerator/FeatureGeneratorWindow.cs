using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EditorTools.FeatureGenerator
{
	/// <summary>
	/// Editor window that generates a feature folder structure and starter code.
	/// </summary>
	public sealed class FeatureGeneratorWindow : EditorWindow
	{
		private const string RootFolder = "Assets/Features";
		private const string SubFeatureFolderName = "SubFeatures";
		private const string WindowTitle = "Feature Generator";
		private const string MenuPath = "Tools/Feature Generator";

		private string _featureName = "Player";
		private bool _generateSubfeature;
		private int _parentFeatureIndex;

		[MenuItem(MenuPath)]
		private static void Open()
		{
			GetWindow<FeatureGeneratorWindow>(WindowTitle);
		}

		private void OnGUI()
		{
			EditorGUILayout.LabelField(_generateSubfeature ? "Create a new Subfeature" : "Create a new Feature", EditorStyles.boldLabel);
			EditorGUILayout.Space();

			_generateSubfeature = EditorGUILayout.ToggleLeft("Generate as Subfeature", _generateSubfeature);
			string parentFeatureName = null;
			if (_generateSubfeature)
			{
				var parentOptions = GetParentFeatureNames();
				if (parentOptions.Length == 0)
				{
					EditorGUILayout.HelpBox("No parent features found under Assets/Features.", MessageType.Warning);
				}
				else
				{
					_parentFeatureIndex = Mathf.Clamp(_parentFeatureIndex, 0, parentOptions.Length - 1);
					_parentFeatureIndex = EditorGUILayout.Popup("Parent Feature", _parentFeatureIndex, parentOptions);
					parentFeatureName = parentOptions[_parentFeatureIndex];
				}
			}

			_featureName = EditorGUILayout.TextField(_generateSubfeature ? "Subfeature Name" : "Feature Name", _featureName);

			EditorGUILayout.Space();
			var canGenerate = !_generateSubfeature || !string.IsNullOrWhiteSpace(parentFeatureName);
			using (new EditorGUI.DisabledScope(!canGenerate))
			{
				if (GUILayout.Button("Generate", GUILayout.Height(28f)))
				{
					if (_generateSubfeature)
					{
						GenerateSubfeature(parentFeatureName);
					}
					else
					{
						GenerateFeature();
					}
				}
			}
		}

		private void GenerateFeature()
		{
			var sanitizedName = SanitizeName(_featureName);
			if (string.IsNullOrWhiteSpace(sanitizedName))
			{
				EditorUtility.DisplayDialog(WindowTitle, "Feature name is invalid.", "OK");
				return;
			}

			if (!string.Equals(sanitizedName, _featureName, StringComparison.Ordinal))
			{
				EditorUtility.DisplayDialog(WindowTitle, $"Feature name was sanitized to '{sanitizedName}'.", "OK");
			}

			var featureRoot = $"{RootFolder}/{sanitizedName}";
			var featureExists = AssetDatabase.IsValidFolder(featureRoot);
			if (featureExists)
			{
				var proceed = EditorUtility.DisplayDialog(WindowTitle, "Feature already exists. Create missing folders/files?", "Yes", "No");
				if (!proceed)
				{
					return;
				}
			}

			EnsureFolderExists(RootFolder);
			CreateFeatureFolders(featureRoot);

			var featureNamespace = $"Features.{sanitizedName}";
			var keyBase = ToKeyBase(sanitizedName);
			var assemblyName = sanitizedName;

			CreateAsmdef(featureRoot, assemblyName, new[] { "Core" });
			CreateTestsAsmdef(featureRoot, assemblyName);
			CreateScene(featureRoot, sanitizedName);
			EnsureScopeKeyExists(sanitizedName + "Gameplay");

			CreateTextFileIfMissing($"{featureRoot}/Scripts/Requests/{sanitizedName}Requests.cs", BuildRequestsFile(featureNamespace, keyBase, sanitizedName));
			CreateTextFileIfMissing($"{featureRoot}/Scripts/Events/{sanitizedName}Events.cs", BuildEventsFile(featureNamespace, keyBase, sanitizedName));
			CreateTextFileIfMissing($"{featureRoot}/Scripts/Model/{sanitizedName}Model.cs", BuildModelFile(featureNamespace, sanitizedName));
			CreateTextFileIfMissing($"{featureRoot}/Scripts/Controller/{sanitizedName}Controller.cs", BuildControllerFile(featureNamespace, sanitizedName, sanitizedName + "Gameplay"));
			CreateTextFileIfMissing($"{featureRoot}/Scripts/View/{sanitizedName}View.cs", BuildViewFile(featureNamespace, sanitizedName, isSubfeature: false));
			CreateTextFileIfMissing($"{featureRoot}/Scripts/Infrastructure/RequestController.cs", BuildRequestControllerWrapper(featureNamespace));
			CreateTextFileIfMissing($"{featureRoot}/Scripts/Infrastructure/EventBus.cs", BuildEventBusWrapper(featureNamespace));
			CreateTextFileIfMissing($"{featureRoot}/Scripts/Infrastructure/ViewEventBinder.cs", BuildViewEventBinderWrapper(featureNamespace));
			CreateTextFileIfMissing($"{featureRoot}/Scripts/Infrastructure/ViewEventCache.cs", BuildViewEventCacheWrapper(featureNamespace));
			CreateTextFileIfMissing($"{featureRoot}/Scripts/Infrastructure/Attributes/RequestAttribute.cs", BuildRequestAttributeWrapper(featureNamespace));
			CreateTextFileIfMissing($"{featureRoot}/Scripts/Infrastructure/Attributes/OnEventAttribute.cs", BuildOnEventAttributeWrapper(featureNamespace));
			CreateTextFileIfMissing($"{featureRoot}/Tests/{sanitizedName}ControllerTests.cs", BuildTestsFile(featureNamespace, sanitizedName));

			AssetDatabase.Refresh();
			EditorUtility.DisplayDialog(WindowTitle, "Feature generated successfully.", "OK");
		}

		private void GenerateSubfeature(string parentFeatureName)
		{
			if (string.IsNullOrWhiteSpace(parentFeatureName))
			{
				EditorUtility.DisplayDialog(WindowTitle, "Parent feature is required for subfeatures.", "OK");
				return;
			}

			var sanitizedParentName = SanitizeName(parentFeatureName);
			if (!string.Equals(sanitizedParentName, parentFeatureName, StringComparison.Ordinal))
			{
				EditorUtility.DisplayDialog(WindowTitle, $"Parent feature name '{parentFeatureName}' is not a valid identifier.", "OK");
				return;
			}

			var sanitizedChildName = SanitizeName(_featureName);
			if (string.IsNullOrWhiteSpace(sanitizedChildName))
			{
				EditorUtility.DisplayDialog(WindowTitle, "Subfeature name is invalid.", "OK");
				return;
			}

			if (!string.Equals(sanitizedChildName, _featureName, StringComparison.Ordinal))
			{
				EditorUtility.DisplayDialog(WindowTitle, $"Subfeature name was sanitized to '{sanitizedChildName}'.", "OK");
			}

			var parentRoot = $"{RootFolder}/{parentFeatureName}";
			if (!AssetDatabase.IsValidFolder(parentRoot))
			{
				EditorUtility.DisplayDialog(WindowTitle, $"Parent feature '{parentFeatureName}' does not exist.", "OK");
				return;
			}

			var subfeatureRoot = $"{parentRoot}/{SubFeatureFolderName}/{sanitizedChildName}";
			var subfeatureExists = AssetDatabase.IsValidFolder(subfeatureRoot);
			if (subfeatureExists)
			{
				var proceed = EditorUtility.DisplayDialog(WindowTitle, "Subfeature already exists. Create missing folders/files?", "Yes", "No");
				if (!proceed)
				{
					return;
				}
			}

			EnsureFolderExists(parentRoot);
			CreateFeatureFolders(subfeatureRoot);

			var featureNamespace = $"Features.{sanitizedParentName}.{SubFeatureFolderName}.{sanitizedChildName}";
			var keyBase = $"{ToKeyBase(sanitizedParentName)}.{ToKeyBase(sanitizedChildName)}";
			var assemblyName = $"{sanitizedParentName}.{sanitizedChildName}";
			var scopeKeyName = sanitizedParentName + "Gameplay";

			CreateAsmdef(subfeatureRoot, assemblyName, new[] { "Core" });
			CreateTestsAsmdef(subfeatureRoot, assemblyName);

			CreateTextFileIfMissing($"{subfeatureRoot}/Scripts/Requests/{sanitizedChildName}Requests.cs", BuildSubfeatureRequestsFile(featureNamespace, keyBase, sanitizedChildName));
			CreateTextFileIfMissing($"{subfeatureRoot}/Scripts/Events/{sanitizedChildName}Events.cs", BuildEventsFile(featureNamespace, keyBase, sanitizedChildName));
			CreateTextFileIfMissing($"{subfeatureRoot}/Scripts/Model/{sanitizedChildName}Model.cs", BuildModelFile(featureNamespace, sanitizedChildName));
			CreateTextFileIfMissing($"{subfeatureRoot}/Scripts/Model/{sanitizedChildName}ParentSignals.cs", BuildParentSignalsFile(featureNamespace, sanitizedChildName));
			CreateTextFileIfMissing($"{subfeatureRoot}/Scripts/Model/{sanitizedChildName}State.cs", BuildStateFile(featureNamespace, sanitizedChildName));
			CreateTextFileIfMissing($"{subfeatureRoot}/Scripts/Controller/{sanitizedChildName}Controller.cs", BuildSubfeatureControllerFile(featureNamespace, sanitizedChildName, scopeKeyName));
			CreateTextFileIfMissing($"{subfeatureRoot}/Scripts/View/{sanitizedChildName}View.cs", BuildViewFile(featureNamespace, sanitizedChildName, isSubfeature: true));
			CreateTextFileIfMissing($"{subfeatureRoot}/Scripts/Infrastructure/RequestController.cs", BuildRequestControllerWrapper(featureNamespace));
			CreateTextFileIfMissing($"{subfeatureRoot}/Scripts/Infrastructure/EventBus.cs", BuildEventBusWrapper(featureNamespace));
			CreateTextFileIfMissing($"{subfeatureRoot}/Scripts/Infrastructure/ViewEventBinder.cs", BuildViewEventBinderWrapper(featureNamespace));
			CreateTextFileIfMissing($"{subfeatureRoot}/Scripts/Infrastructure/ViewEventCache.cs", BuildViewEventCacheWrapper(featureNamespace));
			CreateTextFileIfMissing($"{subfeatureRoot}/Scripts/Infrastructure/Attributes/RequestAttribute.cs", BuildRequestAttributeWrapper(featureNamespace));
			CreateTextFileIfMissing($"{subfeatureRoot}/Scripts/Infrastructure/Attributes/OnEventAttribute.cs", BuildOnEventAttributeWrapper(featureNamespace));
			CreateTextFileIfMissing($"{subfeatureRoot}/Tests/{sanitizedChildName}ControllerTests.cs", BuildTestsFile(featureNamespace, sanitizedChildName));

			AssetDatabase.Refresh();
			EditorUtility.DisplayDialog(WindowTitle, "Subfeature generated successfully.", "OK");
		}

		private static string SanitizeName(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return string.Empty;
			}

			var parts = Regex.Split(name.Trim(), "[^A-Za-z0-9]+");
			var builder = new StringBuilder();
			for (var i = 0; i < parts.Length; i++)
			{
				var part = parts[i];
				if (string.IsNullOrWhiteSpace(part))
				{
					continue;
				}

				builder.Append(char.ToUpperInvariant(part[0]));
				if (part.Length > 1)
				{
					builder.Append(part.Substring(1));
				}
			}

			var result = builder.ToString();
			if (string.IsNullOrWhiteSpace(result))
			{
				return string.Empty;
			}

			if (!char.IsLetter(result[0]) && result[0] != '_')
			{
				result = $"F{result}";
			}

			return result;
		}

		private static string ToKeyBase(string name)
		{
			var lower = Regex.Replace(name, "([a-z0-9])([A-Z])", "$1.$2").ToLowerInvariant();
			return Regex.Replace(lower, "[^a-z0-9.]+", ".").Trim('.');
		}

		private static string[] GetParentFeatureNames()
		{
			var featureRoot = Path.Combine(Application.dataPath, "Features");
			if (!Directory.Exists(featureRoot))
			{
				return Array.Empty<string>();
			}

			var directories = Directory.GetDirectories(featureRoot);
			if (directories.Length == 0)
			{
				return Array.Empty<string>();
			}

			var names = new List<string>(directories.Length);
			for (var i = 0; i < directories.Length; i++)
			{
				var name = Path.GetFileName(directories[i]);
				if (!string.IsNullOrWhiteSpace(name))
				{
					names.Add(name);
				}
			}

			names.Sort(StringComparer.Ordinal);
			return names.ToArray();
		}

		private static void EnsureFolderExists(string folderPath)
		{
			if (!AssetDatabase.IsValidFolder(folderPath))
			{
				var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
				var name = Path.GetFileName(folderPath);
				if (!string.IsNullOrWhiteSpace(parent))
				{
					AssetDatabase.CreateFolder(parent, name);
				}
			}
		}

		private static void CreateFolder(string root, string relative)
		{
			var parts = relative.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
			var current = root;
			for (var i = 0; i < parts.Length; i++)
			{
				var part = parts[i];
				var next = $"{current}/{part}";
				if (!AssetDatabase.IsValidFolder(next))
				{
					AssetDatabase.CreateFolder(current, part);
				}
				current = next;
			}
		}

		private static void CreateFeatureFolders(string featureRoot)
		{
			EnsureFolderExists(featureRoot);
			CreateFolder(featureRoot, "Scenes");
			CreateFolder(featureRoot, "Prefabs");
			CreateFolder(featureRoot, "Assets");
			CreateFolder(featureRoot, "_GamePlay/Scenes");
			CreateFolder(featureRoot, "_GamePlay/Prefabs");
			CreateFolder(featureRoot, "_GamePlay/Art");
			CreateFolder(featureRoot, "_GamePlay/Audio");
			CreateFolder(featureRoot, "Scripts/View");
			CreateFolder(featureRoot, "Scripts/Controller");
			CreateFolder(featureRoot, "Scripts/Model");
			CreateFolder(featureRoot, "Scripts/Requests");
			CreateFolder(featureRoot, "Scripts/Events");
			CreateFolder(featureRoot, "Scripts/Infrastructure/Attributes");
			CreateFolder(featureRoot, "Tests");
		}

		private static void CreateScene(string featureRoot, string featureName)
		{
			var scenePath = $"{featureRoot}/Scenes/{featureName}Gameplay.unity";
			var sceneFullPath = Path.Combine(Application.dataPath, scenePath.Substring("Assets/".Length));
			if (File.Exists(sceneFullPath))
			{
				return;
			}

			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			EditorSceneManager.SaveScene(scene, scenePath);
		}

		private static void CreateAsmdef(string featureRoot, string assemblyName, string[] references)
		{
			var referencesContent = BuildAsmdefReferences(references);
			var content = "{\n" +
				$"    \"name\": \"{assemblyName}\",\n" +
				"    \"rootNamespace\": \"\",\n" +
				$"    \"references\": {referencesContent},\n" +
				"    \"includePlatforms\": [],\n" +
				"    \"excludePlatforms\": [],\n" +
				"    \"allowUnsafeCode\": false,\n" +
				"    \"overrideReferences\": false,\n" +
				"    \"precompiledReferences\": [],\n" +
				"    \"autoReferenced\": true,\n" +
				"    \"defineConstraints\": [],\n" +
				"    \"versionDefines\": [],\n" +
				"    \"noEngineReferences\": false\n" +
				"}\n";

			CreateTextFileIfMissing($"{featureRoot}/{assemblyName}.asmdef", content);
		}

		private static void CreateTestsAsmdef(string featureRoot, string assemblyName)
		{
			var content = "{\n" +
				$"    \"name\": \"{assemblyName}.Tests\",\n" +
				"    \"rootNamespace\": \"\",\n" +
				$"    \"references\": [\"{assemblyName}\"],\n" +
				"    \"includePlatforms\": [\"Editor\"],\n" +
				"    \"excludePlatforms\": [],\n" +
				"    \"allowUnsafeCode\": false,\n" +
				"    \"overrideReferences\": false,\n" +
				"    \"precompiledReferences\": [],\n" +
				"    \"autoReferenced\": false,\n" +
				"    \"defineConstraints\": [],\n" +
				"    \"versionDefines\": [],\n" +
				"    \"noEngineReferences\": false,\n" +
				"    \"optionalUnityReferences\": [\"TestAssemblies\"]\n" +
				"}\n";

			CreateTextFileIfMissing($"{featureRoot}/Tests/{assemblyName}.Tests.asmdef", content);
		}

		private static string BuildAsmdefReferences(string[] references)
		{
			if (references == null || references.Length == 0)
			{
				return "[]";
			}

			var builder = new StringBuilder();
			builder.Append('[');
			for (var i = 0; i < references.Length; i++)
			{
				if (i > 0)
				{
					builder.Append(',');
				}
				builder.Append('"');
				builder.Append(references[i]);
				builder.Append('"');
			}
			builder.Append(']');
			return builder.ToString();
		}

		private static void CreateTextFile(string assetPath, string content)
		{
			var fullPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
			var directory = Path.GetDirectoryName(fullPath);
			if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			File.WriteAllText(fullPath, content);
		}

		private static void CreateTextFileIfMissing(string assetPath, string content)
		{
			var fullPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
			if (File.Exists(fullPath))
			{
				return;
			}

			CreateTextFile(assetPath, content);
		}

		private static void EnsureScopeKeyExists(string scopeKeyName)
		{
			if (string.IsNullOrWhiteSpace(scopeKeyName))
			{
				return;
			}

			var enumAssetPath = "Assets/Core/Infrastructure/Attributes/ControllerScopeKey.cs";
			var enumFullPath = Path.Combine(Application.dataPath, enumAssetPath.Substring("Assets/".Length));
			if (!File.Exists(enumFullPath))
			{
				return;
			}

			var content = File.ReadAllText(enumFullPath);
			if (content.Contains(scopeKeyName))
			{
				return;
			}

			var enumStart = content.IndexOf("public enum ControllerScopeKey", StringComparison.Ordinal);
			if (enumStart < 0)
			{
				return;
			}

			var enumClose = content.IndexOf("\t}", enumStart, StringComparison.Ordinal);
			if (enumClose < 0)
			{
				return;
			}

			var checkIndex = enumClose - 1;
			while (checkIndex > enumStart && char.IsWhiteSpace(content[checkIndex]))
			{
				checkIndex--;
			}

			var needsComma = checkIndex > enumStart && content[checkIndex] != ',';
			var nextValue = GetNextScopeKeyValue(content, enumStart, enumClose);
			var indent = "\t\t";

			// If we need a comma, insert it right after the last non-whitespace character
			if (needsComma)
			{
				content = content.Insert(checkIndex + 1, ",");
				// Recalculate enumClose since we inserted a character
				enumClose = content.IndexOf("\t}", enumStart, StringComparison.Ordinal);
			}

			var addition = Environment.NewLine + indent + scopeKeyName + " = " + nextValue;
			var updated = content.Insert(enumClose, addition + Environment.NewLine);
			File.WriteAllText(enumFullPath, updated);
		}

		private static int GetNextScopeKeyValue(string content, int enumStart, int enumClose)
		{
			var maxValue = -1;
			var enumBody = content.Substring(enumStart, enumClose - enumStart);
			var matches = Regex.Matches(enumBody, @"=\s*(\d+)");
			for (var i = 0; i < matches.Count; i++)
			{
				if (int.TryParse(matches[i].Groups[1].Value, out var value) && value > maxValue)
				{
					maxValue = value;
				}
			}

			return maxValue + 1;
		}

		private static string BuildRequestsFile(string ns, string keyBase, string featureName)
		{
			return "using System;\n\n" +
				$"namespace {ns}.Requests\n" +
				"{\n" +
				"\t/// <summary>\n" +
				"\t/// Request keys for this feature.\n" +
				"\t/// </summary>\n" +
				$"\tpublic static class {featureName}Requests\n" +
				"\t{\n" +
				$"\t\tpublic const string Echo = \"{keyBase}.echo.request\";\n" +
				"\t}\n" +
				"}\n";
		}

		private static string BuildSubfeatureRequestsFile(string ns, string keyBase, string featureName)
		{
			return "using System;\n\n" +
				$"namespace {ns}.Requests\n" +
				"{\n" +
				"\t/// <summary>\n" +
				"\t/// Request keys for this subfeature.\n" +
				"\t/// </summary>\n" +
				$"\tpublic static class {featureName}Requests\n" +
				"\t{\n" +
				$"\t\tpublic const string BindParentSignals = \"{keyBase}.bind-parent-signals.request\";\n" +
				$"\t\tpublic const string Echo = \"{keyBase}.echo.request\";\n" +
				"\t}\n" +
				"}\n";
		}

		private static string BuildEventsFile(string ns, string keyBase, string featureName)
		{
			return "using System;\n\n" +
				$"namespace {ns}.Events\n" +
				"{\n" +
				"\t/// <summary>\n" +
				"\t/// Event keys for this feature.\n" +
				"\t/// </summary>\n" +
				$"\tpublic static class {featureName}Events\n" +
				"\t{\n" +
				$"\t\tpublic const string Echoed = \"{keyBase}.echo.event\";\n" +
				"\t}\n" +
				"}\n";
		}

		private static string BuildModelFile(string ns, string featureName)
		{
			return $"namespace {ns}.Model\n" +
				"{\n" +
				"\t/// <summary>\n" +
				$"\t/// Data model for {featureName}.\n" +
				"\t/// </summary>\n" +
				$"\tpublic sealed class {featureName}Model\n" +
				"\t{\n" +
				"\t\t/// <summary>\n" +
				"\t\t/// Example data property.\n" +
				"\t\t/// </summary>\n" +
				"\t\tpublic string Value { get; set; }\n" +
				"\t}\n" +
				"}\n";
		}

		private static string BuildControllerFile(string ns, string featureName, string scopeKeyName)
		{
			return $"using {ns}.Events;\n" +
				$"using {ns}.Infrastructure;\n" +
				$"using {ns}.Infrastructure.Attributes;\n" +
				$"using {ns}.Requests;\n\n" +
				$"namespace {ns}.Controller\n" +
				"{\n" +
				"\t/// <summary>\n" +
				$"\t/// Controller for {featureName}.\n" +
				"\t/// </summary>\n" +
				$"\t[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.{scopeKeyName})]\n" +
				$"\tpublic static class {featureName}Controller\n" +
				"\t{\n" +
				"\t\t/// <summary>\n" +
				"\t\t/// Called when the controller scope is entered.\n" +
				"\t\t/// </summary>\n" +
				"\t\t[Core.Infrastructure.Attributes.ControllerInit]\n" +
				"\t\tpublic static void OnEnterScope()\n" +
				"\t\t{\n" +
				"\t\t}\n\n" +
				"\t\t/// <summary>\n" +
				"\t\t/// Called when the controller scope is exited.\n" +
				"\t\t/// </summary>\n" +
				"\t\t[Core.Infrastructure.Attributes.ControllerShutdown]\n" +
				"\t\tpublic static void OnExitScope()\n" +
				"\t\t{\n" +
				"\t\t}\n\n" +
				"\t\t/// <summary>\n" +
				"\t\t/// Sample request handler that echoes payload to a view event.\n" +
				"\t\t/// </summary>\n" +
				"\t\t/// <param name=\"payload\">Optional payload.</param>\n" +
				$"\t\t[Request({featureName}Requests.Echo)]\n" +
				"\t\tpublic static void HandleEcho(object payload)\n" +
				"\t\t{\n" +
				$"\t\t\tEventBus.Publish({featureName}Events.Echoed, payload);\n" +
				"\t\t}\n" +
				"\t}\n" +
				"}\n";
		}

		private static string BuildSubfeatureControllerFile(string ns, string featureName, string scopeKeyName)
		{
			return $"using {ns}.Events;\n" +
				$"using {ns}.Infrastructure;\n" +
				$"using {ns}.Infrastructure.Attributes;\n" +
				$"using {ns}.Model;\n" +
				$"using {ns}.Requests;\n\n" +
				$"namespace {ns}.Controller\n" +
				"{\n" +
				"\t/// <summary>\n" +
				$"\t/// Controller for {featureName}.\n" +
				"\t/// </summary>\n" +
				$"\t[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.{scopeKeyName})]\n" +
				$"\tpublic static class {featureName}Controller\n" +
				"\t{\n" +
				"\t\t/// <summary>\n" +
				"\t\t/// Called when the controller scope is entered.\n" +
				"\t\t/// </summary>\n" +
				"\t\t[Core.Infrastructure.Attributes.ControllerInit]\n" +
				"\t\tpublic static void OnEnterScope()\n" +
				"\t\t{\n" +
				"\t\t}\n\n" +
				"\t\t/// <summary>\n" +
				"\t\t/// Called when the controller scope is exited.\n" +
				"\t\t/// </summary>\n" +
				"\t\t[Core.Infrastructure.Attributes.ControllerShutdown]\n" +
				"\t\tpublic static void OnExitScope()\n" +
				"\t\t{\n" +
				"\t\t}\n\n" +
				"\t\t/// <summary>\n" +
				"\t\t/// Binds parent-provided signals for this subfeature.\n" +
				"\t\t/// </summary>\n" +
				"\t\t/// <param name=\"payload\">Expected type: " + featureName + "ParentSignals.</param>\n" +
				$"\t\t[Request({featureName}Requests.BindParentSignals)]\n" +
				"\t\tpublic static void BindParentSignals(object payload)\n" +
				"\t\t{\n" +
				"\t\t\t" + featureName + "State.ParentSignals = payload as " + featureName + "ParentSignals;\n" +
				"\t\t}\n\n" +
				"\t\t/// <summary>\n" +
				"\t\t/// Sample request handler that echoes payload to a view event and parent signal.\n" +
				"\t\t/// </summary>\n" +
				"\t\t/// <param name=\"payload\">Optional payload.</param>\n" +
				$"\t\t[Request({featureName}Requests.Echo)]\n" +
				"\t\tpublic static void HandleEcho(object payload)\n" +
				"\t\t{\n" +
				$"\t\t\tEventBus.Publish({featureName}Events.Echoed, payload);\n" +
				"\t\t\t" + featureName + "State.ParentSignals?.OnEchoed?.Invoke(payload);\n" +
				"\t\t}\n" +
				"\t}\n" +
				"}\n";
		}

		private static string BuildViewFile(string ns, string featureName, bool isSubfeature)
		{
			var modelUsing = isSubfeature ? $"using {ns}.Model;\n" : string.Empty;
			var subfeatureFields = isSubfeature
				? "\t\tprivate " + featureName + "ParentSignals _parentSignals;\n\n" +
				  "\t\t/// <summary>\n" +
				  "\t\t/// Injects parent-provided signal handlers for this subfeature.\n" +
				  "\t\t/// </summary>\n" +
				  "\t\t/// <param name=\"signals\">Signals implemented by the parent feature.</param>\n" +
				  "\t\tpublic void SetParentSignals(" + featureName + "ParentSignals signals)\n" +
				  "\t\t{\n" +
				  "\t\t\t_parentSignals = signals;\n" +
				  "\t\t\tTryBindParentSignals();\n" +
				  "\t\t}\n\n" +
				  "\t\t/// <summary>\n" +
				  "\t\t/// Ensures parent signals are bound after scope activation.\n" +
				  "\t\t/// </summary>\n" +
				  "\t\tprotected override void OnEnabled()\n" +
				  "\t\t{\n" +
				  "\t\t\tbase.OnEnabled();\n" +
				  "\t\t\tTryBindParentSignals();\n" +
				  "\t\t}\n\n" +
				  "\t\tprivate void TryBindParentSignals()\n" +
				  "\t\t{\n" +
				  "\t\t\tif (_parentSignals == null)\n" +
				  "\t\t\t{\n" +
				  "\t\t\t\treturn;\n" +
				  "\t\t\t}\n\n" +
				  "\t\t\tSendRequest(" + featureName + "Requests.BindParentSignals, _parentSignals);\n" +
				  "\t\t}\n\n"
				: string.Empty;

			return "using Core.Infrastructure.Views;\n" +
				modelUsing +
				$"using {ns}.Events;\n" +
				$"using {ns}.Infrastructure.Attributes;\n" +
				$"using {ns}.Requests;\n" +
				"using UnityEngine;\n\n" +
				$"namespace {ns}.View\n" +
				"{\n" +
				"\t/// <summary>\n" +
				$"\t/// View for {featureName}.\n" +
				"\t/// </summary>\n" +
				$"\tpublic sealed class {featureName}View : BaseView\n" +
				"\t{\n" +
				"\t\t[SerializeField]\n" +
				"\t\tprivate string _message = \"Hello\";\n\n" +
				subfeatureFields +
				"\t\t/// <summary>\n" +
				"\t\t/// Example method to send a request.\n" +
				"\t\t/// </summary>\n" +
				"\t\tpublic void SendEcho()\n" +
				"\t\t{\n" +
				$"\t\t\tSendRequest({featureName}Requests.Echo, _message);\n" +
				"\t\t}\n\n" +
				"\t\t/// <summary>\n" +
				"\t\t/// Example event handler (auto-bound).\n" +
				"\t\t/// </summary>\n" +
				"\t\t/// <param name=\"payload\">Payload from controller.</param>\n" +
				$"\t\t[OnEvent({featureName}Events.Echoed)]\n" +
				"\t\tprivate void OnEchoed(object payload)\n" +
				"\t\t{\n" +
				$"\t\t\tDebug.Log(\"[{featureName}View] Echoed: \" + payload, this);\n" +
				"\t\t}\n" +
				"\t}\n" +
				"}\n";
		}

		private static string BuildParentSignalsFile(string ns, string featureName)
		{
			return "using System;\n\n" +
				$"namespace {ns}.Model\n" +
				"{\n" +
				"\t/// <summary>\n" +
				"\t/// Parent-provided callbacks for child-to-parent signaling.\n" +
				"\t/// </summary>\n" +
				$"\tpublic sealed class {featureName}ParentSignals\n" +
				"\t{\n" +
				"\t\t/// <summary>\n" +
				"\t\t/// Optional callback invoked when the child echoes a payload.\n" +
				"\t\t/// </summary>\n" +
				"\t\tpublic Action<object> OnEchoed { get; set; }\n\n" +
				"\t\t/// <summary>\n" +
				"\t\t/// Optional callback for retrieving data from the parent.\n" +
				"\t\t/// </summary>\n" +
				"\t\tpublic Func<string> GetParentStatus { get; set; }\n" +
				"\t}\n" +
				"}\n";
		}

		private static string BuildStateFile(string ns, string featureName)
		{
			return $"namespace {ns}.Model\n" +
				"{\n" +
				"\t/// <summary>\n" +
				"\t/// Holds subfeature state, including parent signal bindings.\n" +
				"\t/// </summary>\n" +
				$"\tpublic static class {featureName}State\n" +
				"\t{\n" +
				$"\t\tpublic static {featureName}ParentSignals ParentSignals {{ get; set; }}\n" +
				"\t}\n" +
				"}\n";
		}

		private static string BuildRequestControllerWrapper(string ns)
		{
			return "using Core.Infrastructure.Requests;\n\n" +
				$"namespace {ns}.Infrastructure\n" +
				"{\n" +
				"\t/// <summary>\n" +
				"\t/// Feature-level request gateway that forwards to core infrastructure.\n" +
				"\t/// </summary>\n" +
				"\tpublic static class RequestController\n" +
				"\t{\n" +
				"\t\t/// <summary>\n" +
				"\t\t/// Executes a request by key.\n" +
				"\t\t/// </summary>\n" +
				"\t\t/// <param name=\"key\">Request key.</param>\n" +
				"\t\t/// <param name=\"payload\">Optional payload.</param>\n" +
				"\t\tpublic static void Execute(string key, object payload = null)\n" +
				"\t\t{\n" +
				"\t\t\tCore.Infrastructure.Requests.RequestController.Execute(key, payload);\n" +
				"\t\t}\n\n" +
				"\t\t/// <summary>\n" +
				"\t\t/// Executes a request by key and returns a typed result.\n" +
				"\t\t/// </summary>\n" +
				"\t\t/// <typeparam name=\"T\">Expected return type.</typeparam>\n" +
				"\t\t/// <param name=\"key\">Request key.</param>\n" +
				"\t\t/// <param name=\"payload\">Optional payload.</param>\n" +
				"\t\t/// <returns>The typed result or default when unavailable/mismatched.</returns>\n" +
				"\t\tpublic static T Execute<T>(string key, object payload = null)\n" +
				"\t\t{\n" +
				"\t\t\treturn Core.Infrastructure.Requests.RequestController.Execute<T>(key, payload);\n" +
				"\t\t}\n\n" +
				"\t\t/// <summary>\n" +
				"\t\t/// Ensures request bindings are initialized.\n" +
				"\t\t/// </summary>\n" +
				"\t\tpublic static void Initialize()\n" +
				"\t\t{\n" +
				"\t\t\tCore.Infrastructure.Requests.RequestController.Initialize();\n" +
				"\t\t}\n" +
				"\t}\n" +
				"}\n";
		}

		private static string BuildEventBusWrapper(string ns)
		{
			return "using System;\n\n" +
				$"namespace {ns}.Infrastructure\n" +
				"{\n" +
				"\t/// <summary>\n" +
				"\t/// Feature-level event bus that forwards to core infrastructure.\n" +
				"\t/// </summary>\n" +
				"\tpublic static class EventBus\n" +
				"\t{\n" +
				"\t\tpublic static void Publish(string key, object payload = null)\n" +
				"\t\t{\n" +
				"\t\t\tCore.Infrastructure.Events.EventBus.Publish(key, payload);\n" +
				"\t\t}\n\n" +
				"\t\tpublic static void Subscribe(string key, Action<object> handler)\n" +
				"\t\t{\n" +
				"\t\t\tCore.Infrastructure.Events.EventBus.Subscribe(key, handler);\n" +
				"\t\t}\n\n" +
				"\t\tpublic static void Unsubscribe(string key, Action<object> handler)\n" +
				"\t\t{\n" +
				"\t\t\tCore.Infrastructure.Events.EventBus.Unsubscribe(key, handler);\n" +
				"\t\t}\n" +
				"\t}\n" +
				"}\n";
		}

		private static string BuildViewEventBinderWrapper(string ns)
		{
			return "using System;\n" +
				"using UnityEngine;\n\n" +
				$"namespace {ns}.Infrastructure\n" +
				"{\n" +
				"\t/// <summary>\n" +
				"\t/// Feature-level view event binder that forwards to core infrastructure.\n" +
				"\t/// </summary>\n" +
				"\tpublic sealed class ViewEventBinder\n" +
				"\t{\n" +
				"\t\tprivate readonly Core.Infrastructure.Views.ViewEventBinder _inner;\n\n" +
				"\t\tpublic ViewEventBinder(MonoBehaviour view)\n" +
				"\t\t{\n" +
				"\t\t\t_inner = new Core.Infrastructure.Views.ViewEventBinder(view);\n" +
				"\t\t}\n\n" +
				"\t\tpublic void Bind()\n" +
				"\t\t{\n" +
				"\t\t\t_inner.Bind();\n" +
				"\t\t}\n\n" +
				"\t\tpublic void Unbind()\n" +
				"\t\t{\n" +
				"\t\t\t_inner.Unbind();\n" +
				"\t\t}\n" +
				"\t}\n" +
				"}\n";
		}

		private static string BuildViewEventCacheWrapper(string ns)
		{
			return "using System;\n\n" +
				$"namespace {ns}.Infrastructure\n" +
				"{\n" +
				"\t/// <summary>\n" +
				"\t/// Feature-level cache access to core view event cache.\n" +
				"\t/// </summary>\n" +
				"\tpublic static class ViewEventCache\n" +
				"\t{\n" +
				"\t\tpublic static Core.Infrastructure.Views.ViewEventDescriptor[] GetOrCreate(Type viewType)\n" +
				"\t\t{\n" +
				"\t\t\treturn Core.Infrastructure.Views.ViewEventCache.GetOrCreate(viewType);\n" +
				"\t\t}\n" +
				"\t}\n" +
				"}\n";
		}

		private static string BuildRequestAttributeWrapper(string ns)
		{
			return "using System;\n\n" +
				$"namespace {ns}.Infrastructure.Attributes\n" +
				"{\n" +
				"\t/// <summary>\n" +
				"\t/// Feature-level request attribute that forwards to core infrastructure.\n" +
				"\t/// </summary>\n" +
				"\tpublic sealed class RequestAttribute : Core.Infrastructure.Attributes.RequestAttribute\n" +
				"\t{\n" +
				"\t\tpublic RequestAttribute(string key) : base(key)\n" +
				"\t\t{\n" +
				"\t\t}\n" +
				"\t}\n" +
				"}\n";
		}

		private static string BuildOnEventAttributeWrapper(string ns)
		{
			return "using System;\n\n" +
				$"namespace {ns}.Infrastructure.Attributes\n" +
				"{\n" +
				"\t/// <summary>\n" +
				"\t/// Feature-level event attribute that forwards to core infrastructure.\n" +
				"\t/// </summary>\n" +
				"\tpublic sealed class OnEventAttribute : Core.Infrastructure.Attributes.OnEventAttribute\n" +
				"\t{\n" +
				"\t\tpublic OnEventAttribute(string key) : base(key)\n" +
				"\t\t{\n" +
				"\t\t}\n" +
				"\t}\n" +
				"}\n";
		}

		private static string BuildTestsFile(string ns, string featureName)
		{
			return "using NUnit.Framework;\n\n" +
				$"namespace {ns}.Tests\n" +
				"{\n" +
				"\t/// <summary>\n" +
				$"\t/// Basic tests for {featureName} controller.\n" +
				"\t/// </summary>\n" +
				"\tpublic sealed class " + featureName + "ControllerTests\n" +
				"\t{\n" +
				"\t\t[Test]\n" +
				"\t\tpublic void PlaceholderTest()\n" +
				"\t\t{\n" +
				"\t\t\tAssert.Pass(\"Generated test placeholder.\");\n" +
				"\t\t}\n" +
				"\t}\n" +
				"}\n";
		}
	}
}
