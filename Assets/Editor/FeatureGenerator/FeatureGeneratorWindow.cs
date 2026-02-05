using System;
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
		private const string WindowTitle = "Feature Generator";
		private const string MenuPath = "Tools/Feature Generator";

		private string _featureName = "Player";

		[MenuItem(MenuPath)]
		private static void Open()
		{
			GetWindow<FeatureGeneratorWindow>(WindowTitle);
		}

		private void OnGUI()
		{
			EditorGUILayout.LabelField("Create a new Feature", EditorStyles.boldLabel);
			EditorGUILayout.Space();
			_featureName = EditorGUILayout.TextField("Feature Name", _featureName);

			EditorGUILayout.Space();
			if (GUILayout.Button("Generate", GUILayout.Height(28f)))
			{
				GenerateFeature();
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
			if (AssetDatabase.IsValidFolder(featureRoot))
			{
				EditorUtility.DisplayDialog(WindowTitle, "Feature already exists.", "OK");
				return;
			}

			EnsureFolderExists(RootFolder);
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

			var featureNamespace = $"Features.{sanitizedName}";
			var keyBase = ToKeyBase(sanitizedName);

			CreateAsmdef(featureRoot, sanitizedName);
			CreateTestsAsmdef(featureRoot, sanitizedName);
			CreateScene(featureRoot, sanitizedName);

			CreateTextFile($"{featureRoot}/Scripts/Requests/{sanitizedName}Requests.cs", BuildRequestsFile(featureNamespace, keyBase, sanitizedName));
			CreateTextFile($"{featureRoot}/Scripts/Events/{sanitizedName}Events.cs", BuildEventsFile(featureNamespace, keyBase, sanitizedName));
			CreateTextFile($"{featureRoot}/Scripts/Model/{sanitizedName}Model.cs", BuildModelFile(featureNamespace, sanitizedName));
			CreateTextFile($"{featureRoot}/Scripts/Controller/{sanitizedName}Controller.cs", BuildControllerFile(featureNamespace, sanitizedName));
			CreateTextFile($"{featureRoot}/Scripts/View/{sanitizedName}View.cs", BuildViewFile(featureNamespace, sanitizedName));
			CreateTextFile($"{featureRoot}/Scripts/Infrastructure/RequestController.cs", BuildRequestControllerWrapper(featureNamespace));
			CreateTextFile($"{featureRoot}/Scripts/Infrastructure/EventBus.cs", BuildEventBusWrapper(featureNamespace));
			CreateTextFile($"{featureRoot}/Scripts/Infrastructure/ViewEventBinder.cs", BuildViewEventBinderWrapper(featureNamespace));
			CreateTextFile($"{featureRoot}/Scripts/Infrastructure/ViewEventCache.cs", BuildViewEventCacheWrapper(featureNamespace));
			CreateTextFile($"{featureRoot}/Scripts/Infrastructure/Attributes/RequestAttribute.cs", BuildRequestAttributeWrapper(featureNamespace));
			CreateTextFile($"{featureRoot}/Scripts/Infrastructure/Attributes/OnEventAttribute.cs", BuildOnEventAttributeWrapper(featureNamespace));
			CreateTextFile($"{featureRoot}/Tests/{sanitizedName}ControllerTests.cs", BuildTestsFile(featureNamespace, sanitizedName));

			AssetDatabase.Refresh();
			EditorUtility.DisplayDialog(WindowTitle, "Feature generated successfully.", "OK");
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

		private static void CreateScene(string featureRoot, string featureName)
		{
			var scenePath = $"{featureRoot}/_GamePlay/Scenes/{featureName}Gameplay.unity";
			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			EditorSceneManager.SaveScene(scene, scenePath);
		}

		private static void CreateAsmdef(string featureRoot, string featureName)
		{
			var content = "{\n" +
				$"    \"name\": \"{featureName}\",\n" +
				"    \"rootNamespace\": \"\",\n" +
				"    \"references\": [\"Core\"],\n" +
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

			CreateTextFile($"{featureRoot}/{featureName}.asmdef", content);
		}

		private static void CreateTestsAsmdef(string featureRoot, string featureName)
		{
			var content = "{\n" +
				$"    \"name\": \"{featureName}.Tests\",\n" +
				"    \"rootNamespace\": \"\",\n" +
				$"    \"references\": [\"{featureName}\"],\n" +
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

			CreateTextFile($"{featureRoot}/Tests/{featureName}.Tests.asmdef", content);
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

		private static string BuildControllerFile(string ns, string featureName)
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
				$"\tpublic static class {featureName}Controller\n" +
				"\t{\n" +
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

		private static string BuildViewFile(string ns, string featureName)
		{
			return "using Core.Infrastructure.Views;\n" +
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
				"\t\tpublic static void Execute(string key, object payload = null)\n" +
				"\t\t{\n" +
				"\t\t\tCore.Infrastructure.Requests.RequestController.Execute(key, payload);\n" +
				"\t\t}\n\n" +
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
