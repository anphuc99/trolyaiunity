using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Editor.JournalOverlaySceneBuilder
{
	/// <summary>
	/// Editor utility that builds the JournalOverlay scene UI hierarchy.
	/// Run via menu: TrolyAI > Build JournalOverlay Scene UI.
	/// After running, save the scene in the editor.
	/// </summary>
	public static class JournalOverlaySceneBuilder
	{
		private const string ScenePath =
			"Assets/Features/JournalOverlay/Scenes/JournalOverlayGameplay.unity";

		/// <summary>
		/// Opens the JournalOverlayGameplay scene and populates it with the
		/// required Canvas, text displays, transport controls, and an AudioSource.
		/// Finally wires all SerializeField references on <c>JournalOverlayView</c>.
		/// </summary>
		[MenuItem("TrolyAI/Build JournalOverlay Scene UI")]
		public static void Build()
		{
			// Open the target scene.
			var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
			if (!scene.IsValid())
			{
				Debug.LogError("[JournalOverlaySceneBuilder] Could not open scene at " + ScenePath);
				return;
			}

			// ─── Root Canvas ───────────────────────────────────────────
			var canvasGo = new GameObject("OverlayCanvas");
			SceneManager.MoveGameObjectToScene(canvasGo, scene);

			var canvas = canvasGo.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 100;

			var scaler = canvasGo.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(420, 300);
			scaler.matchWidthOrHeight = 0.5f;

			canvasGo.AddComponent<GraphicRaycaster>();

			// ─── Background panel ──────────────────────────────────────
			var bgGo = CreateUIElement("Background", canvasGo.transform);
			var bgImage = bgGo.AddComponent<Image>();
			bgImage.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);
			StretchFill(bgGo);

			// ─── Top bar (sender name + close button) ──────────────────
			var topBar = CreateUIElement("TopBar", bgGo.transform);
			SetAnchors(topBar, new Vector2(0, 1), new Vector2(1, 1));
			var topRect = topBar.GetComponent<RectTransform>();
			topRect.pivot = new Vector2(0.5f, 1f);
			topRect.offsetMin = new Vector2(8, -40);
			topRect.offsetMax = new Vector2(-8, 0);
			var topLayout = topBar.AddComponent<HorizontalLayoutGroup>();
			topLayout.childAlignment = TextAnchor.MiddleLeft;
			topLayout.spacing = 4;
			topLayout.padding = new RectOffset(4, 4, 2, 2);
			topLayout.childForceExpandWidth = false;
			topLayout.childForceExpandHeight = true;

			// Sender name text
			var senderNameGo = CreateTextElement("SenderNameText", topBar.transform, "Sender", 16,
				TextAlignmentOptions.MidlineLeft, new Color(0.95f, 0.8f, 0.3f));
			var senderLayout = senderNameGo.AddComponent<LayoutElement>();
			senderLayout.flexibleWidth = 1;

			// Close button
			var closeBtn = CreateButtonElement("CloseButton", topBar.transform, "X",
				new Color(0.8f, 0.2f, 0.2f, 1f), 40, 30);

			// ─── Message area ──────────────────────────────────────────
			var messageArea = CreateUIElement("MessageArea", bgGo.transform);
			SetAnchors(messageArea, new Vector2(0, 0), new Vector2(1, 1));
			var msgRect = messageArea.GetComponent<RectTransform>();
			msgRect.offsetMin = new Vector2(8, 75);    // leave room for controls
			msgRect.offsetMax = new Vector2(-8, -44);   // leave room for top bar

			var msgLayout = messageArea.AddComponent<VerticalLayoutGroup>();
			msgLayout.childAlignment = TextAnchor.UpperLeft;
			msgLayout.spacing = 4;
			msgLayout.padding = new RectOffset(4, 4, 4, 4);
			msgLayout.childForceExpandWidth = true;
			msgLayout.childForceExpandHeight = false;

			// Message text
			var messageGo = CreateTextElement("MessageText", messageArea.transform,
				"Message text will appear here...", 14,
				TextAlignmentOptions.TopLeft, Color.white);
			var msgElem = messageGo.AddComponent<LayoutElement>();
			msgElem.flexibleHeight = 1;

			// Translation text
			var translationGo = CreateTextElement("TranslationText", messageArea.transform,
				"", 12, TextAlignmentOptions.TopLeft, new Color(0.7f, 0.7f, 0.85f));
			var transElem = translationGo.AddComponent<LayoutElement>();
			transElem.preferredHeight = 30;

			// ─── Bottom bar (transport controls) ───────────────────────
			var bottomBar = CreateUIElement("BottomBar", bgGo.transform);
			SetAnchors(bottomBar, new Vector2(0, 0), new Vector2(1, 0));
			var botRect = bottomBar.GetComponent<RectTransform>();
			botRect.pivot = new Vector2(0.5f, 0f);
			botRect.offsetMin = new Vector2(8, 8);
			botRect.offsetMax = new Vector2(-8, 68);

			var botLayout = bottomBar.AddComponent<HorizontalLayoutGroup>();
			botLayout.childAlignment = TextAnchor.MiddleCenter;
			botLayout.spacing = 8;
			botLayout.padding = new RectOffset(4, 4, 4, 4);
			botLayout.childForceExpandWidth = false;
			botLayout.childForceExpandHeight = true;

			// Previous button
			var prevBtn = CreateButtonElement("PreviousButton", bottomBar.transform, "<<",
				new Color(0.25f, 0.25f, 0.35f, 1f), 48, 40);

			// Play/Pause button (contains two icon children)
			var playPauseBtn = CreateButtonElement("PlayPauseButton", bottomBar.transform, "",
				new Color(0.25f, 0.25f, 0.35f, 1f), 48, 40);

			// Play icon child
			var playIcon = CreateTextElement("PlayIcon", playPauseBtn.transform, "▶", 18,
				TextAlignmentOptions.Center, Color.white);
			StretchFill(playIcon);
			playIcon.SetActive(false); // hidden by default (playing state shows pause)

			// Pause icon child
			var pauseIcon = CreateTextElement("PauseIcon", playPauseBtn.transform, "⏸", 18,
				TextAlignmentOptions.Center, Color.white);
			StretchFill(pauseIcon);

			// Next button
			var nextBtn = CreateButtonElement("NextButton", bottomBar.transform, ">>",
				new Color(0.25f, 0.25f, 0.35f, 1f), 48, 40);

			// Stop button
			var stopBtn = CreateButtonElement("StopButton", bottomBar.transform, "■",
				new Color(0.6f, 0.15f, 0.15f, 1f), 48, 40);

			// Progress text
			var progressGo = CreateTextElement("ProgressText", bottomBar.transform, "0 / 0", 12,
				TextAlignmentOptions.Center, new Color(0.7f, 0.7f, 0.7f));
			var progLayout = progressGo.AddComponent<LayoutElement>();
			progLayout.preferredWidth = 60;

			// ─── Loading indicator (centered, hidden by default) ───────
			var loadingGo = CreateTextElement("LoadingIndicator", bgGo.transform, "Loading...", 16,
				TextAlignmentOptions.Center, Color.white);
			var loadRect = loadingGo.GetComponent<RectTransform>();
			loadRect.anchorMin = new Vector2(0.2f, 0.3f);
			loadRect.anchorMax = new Vector2(0.8f, 0.7f);
			loadRect.offsetMin = Vector2.zero;
			loadRect.offsetMax = Vector2.zero;
			loadingGo.SetActive(false);

			// ─── AudioSource (on the root view object) ─────────────────
			var viewGo = new GameObject("JournalOverlayView");
			SceneManager.MoveGameObjectToScene(viewGo, scene);
			var audioSource = viewGo.AddComponent<AudioSource>();
			audioSource.playOnAwake = false;

			// Add the view component and wire references via SerializedObject.
			var viewComponent = viewGo.AddComponent<Features.JournalOverlay.View.JournalOverlayView>();
			var so = new SerializedObject(viewComponent);
			so.FindProperty("_senderNameText").objectReferenceValue = senderNameGo.GetComponent<TMP_Text>();
			so.FindProperty("_messageText").objectReferenceValue = messageGo.GetComponent<TMP_Text>();
			so.FindProperty("_translationText").objectReferenceValue = translationGo.GetComponent<TMP_Text>();
			so.FindProperty("_progressText").objectReferenceValue = progressGo.GetComponent<TMP_Text>();
			so.FindProperty("_previousButton").objectReferenceValue = prevBtn.GetComponent<Button>();
			so.FindProperty("_playPauseButton").objectReferenceValue = playPauseBtn.GetComponent<Button>();
			so.FindProperty("_nextButton").objectReferenceValue = nextBtn.GetComponent<Button>();
			so.FindProperty("_stopButton").objectReferenceValue = stopBtn.GetComponent<Button>();
			so.FindProperty("_closeButton").objectReferenceValue = closeBtn.GetComponent<Button>();
			so.FindProperty("_playIcon").objectReferenceValue = playIcon;
			so.FindProperty("_pauseIcon").objectReferenceValue = pauseIcon;
			so.FindProperty("_loadingIndicator").objectReferenceValue = loadingGo;
			so.FindProperty("_voiceAudioSource").objectReferenceValue = audioSource;
			so.ApplyModifiedProperties();

			// ─── EventSystem (required for UI interaction) ─────────────
			if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
			{
				var eventSystemGo = new GameObject("EventSystem");
				SceneManager.MoveGameObjectToScene(eventSystemGo, scene);
				eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
				eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
			}

			// ─── Save ──────────────────────────────────────────────────
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);

			Debug.Log("[JournalOverlaySceneBuilder] Scene UI built and saved successfully.");
		}

		// ================================================================
		// Helper methods
		// ================================================================

		/// <summary>Creates a bare UI RectTransform object.</summary>
		private static GameObject CreateUIElement(string name, Transform parent)
		{
			var go = new GameObject(name, typeof(RectTransform));
			go.transform.SetParent(parent, false);
			return go;
		}

		/// <summary>Creates a TMP_Text element.</summary>
		private static GameObject CreateTextElement(
			string name, Transform parent, string text, float fontSize,
			TextAlignmentOptions alignment, Color color)
		{
			var go = new GameObject(name, typeof(RectTransform));
			go.transform.SetParent(parent, false);

			var tmp = go.AddComponent<TextMeshProUGUI>();
			tmp.text = text;
			tmp.fontSize = fontSize;
			tmp.alignment = alignment;
			tmp.color = color;
			tmp.enableAutoSizing = false;

			return go;
		}

		/// <summary>Creates a simple button with a text label.</summary>
		private static GameObject CreateButtonElement(
			string name, Transform parent, string label, Color bgColor,
			float width, float height)
		{
			var go = new GameObject(name, typeof(RectTransform));
			go.transform.SetParent(parent, false);

			var img = go.AddComponent<Image>();
			img.color = bgColor;

			go.AddComponent<Button>();

			var layoutElem = go.AddComponent<LayoutElement>();
			layoutElem.preferredWidth = width;
			layoutElem.preferredHeight = height;

			if (!string.IsNullOrEmpty(label))
			{
				var textGo = new GameObject("Label", typeof(RectTransform));
				textGo.transform.SetParent(go.transform, false);
				StretchFill(textGo);

				var tmp = textGo.AddComponent<TextMeshProUGUI>();
				tmp.text = label;
				tmp.fontSize = 14;
				tmp.alignment = TextAlignmentOptions.Center;
				tmp.color = Color.white;
			}

			return go;
		}

		/// <summary>Stretches a RectTransform to fill its parent.</summary>
		private static void StretchFill(GameObject go)
		{
			var rect = go.GetComponent<RectTransform>();
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
		}

		/// <summary>Sets anchor min/max on a RectTransform.</summary>
		private static void SetAnchors(GameObject go, Vector2 min, Vector2 max)
		{
			var rect = go.GetComponent<RectTransform>();
			rect.anchorMin = min;
			rect.anchorMax = max;
		}
	}
}
