using Features.GamePlay.SubFeatures.Journal.Infrastructure;
using Features.GamePlay.SubFeatures.Journal.Model;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.Journal.View
{
	/// <summary>
	/// Manages the floating overlay UI displayed when the user activates floating mode
	/// during journal auto-play. Shows the current message bubble, playback progress,
	/// and transport controls (prev/play-pause/next/stop/close).
	/// <para>
	/// This is a plain <see cref="MonoBehaviour"/>, NOT a <see cref="Core.Infrastructure.Views.BaseView"/>.
	/// It is owned and driven by <see cref="JournalView"/> to respect the one-BaseView-per-Controller rule.
	/// </para>
	/// </summary>
	public sealed class JournalOverlayManager : MonoBehaviour, IDragHandler, IBeginDragHandler
	{
		// --- Serialized UI references (wired in prefab / inspector) ---

		[Header("Root")]
		[SerializeField] private Canvas _overlayCanvas;
		[SerializeField] private RectTransform _overlayRoot;

		[Header("Message display")]
		[SerializeField] private TMP_Text _senderNameText;
		[SerializeField] private TMP_Text _messageText;

		[Header("Progress")]
		[SerializeField] private TMP_Text _progressText;

		[Header("Transport controls")]
		[SerializeField] private Button _previousButton;
		[SerializeField] private Button _playPauseButton;
		[SerializeField] private Button _nextButton;
		[SerializeField] private Button _stopButton;
		[SerializeField] private Button _closeButton;

		[Header("Play/Pause icons")]
		[SerializeField] private GameObject _playIcon;
		[SerializeField] private GameObject _pauseIcon;

		// --- Drag state ---
		private Vector2 _dragOffset;

		// --- Callbacks (set by JournalView) ---

		/// <summary>Invoked when user presses Previous.</summary>
		public System.Action OnPreviousClicked { get; set; }

		/// <summary>Invoked when user presses Play/Pause toggle.</summary>
		public System.Action OnPlayPauseClicked { get; set; }

		/// <summary>Invoked when user presses Next.</summary>
		public System.Action OnNextClicked { get; set; }

		/// <summary>Invoked when user presses Stop.</summary>
		public System.Action OnStopClicked { get; set; }

		/// <summary>Invoked when user presses Close (exit floating mode).</summary>
		public System.Action OnCloseClicked { get; set; }

		// --- Overlay dimensions (pixels) ---
		private const int OverlayWidth = 420;
		private const int OverlayHeight = 220;
		private const int ScreenMargin = 20;

		// ================================================================
		// MonoBehaviour lifecycle
		// ================================================================

		private void Awake()
		{
			WireButtons();
			Hide();
		}

		// ================================================================
		// Public API (called by JournalView)
		// ================================================================

		/// <summary>
		/// Shows the overlay and enters floating mode (always-on-top, borderless, resized).
		/// </summary>
		public void Show()
		{
			if (_overlayCanvas != null)
			{
				_overlayCanvas.gameObject.SetActive(true);
			}

			NativeWindowManager.SaveWindowState();
			NativeWindowManager.SetBorderless(true);

			// Position bottom-right of the primary display.
			var screenWidth = Display.main.systemWidth;
			var screenHeight = Display.main.systemHeight;
			var x = screenWidth - OverlayWidth - ScreenMargin;
			var y = screenHeight - OverlayHeight - ScreenMargin;

			NativeWindowManager.ResizeAndPosition(x, y, OverlayWidth, OverlayHeight);
			NativeWindowManager.SetAlwaysOnTop(true);
		}

		/// <summary>
		/// Hides the overlay and restores the original window state.
		/// </summary>
		public void Hide()
		{
			if (_overlayCanvas != null)
			{
				_overlayCanvas.gameObject.SetActive(false);
			}

			if (NativeWindowManager.HasSavedState)
			{
				NativeWindowManager.RestoreWindowState();
			}
		}

		/// <summary>
		/// Updates the overlay UI with the current playback message.
		/// </summary>
		/// <param name="payload">Current message payload from controller.</param>
		public void UpdateCurrentMessage(JournalPlaybackMessageChangedPayload payload)
		{
			if (payload == null)
			{
				return;
			}

			if (_senderNameText != null && payload.CurrentItem != null)
			{
				_senderNameText.text = payload.CurrentItem.SenderName ?? string.Empty;
			}

			if (_messageText != null && payload.CurrentItem != null)
			{
				_messageText.text = payload.CurrentItem.Text ?? string.Empty;
			}

			if (_progressText != null)
			{
				_progressText.text = (payload.CurrentIndex + 1) + " / " + payload.TotalCount;
			}
		}

		/// <summary>
		/// Updates the play/pause button visual state.
		/// </summary>
		/// <param name="isPaused">True when playback is paused.</param>
		public void SetPausedState(bool isPaused)
		{
			if (_playIcon != null)
			{
				_playIcon.SetActive(isPaused);
			}

			if (_pauseIcon != null)
			{
				_pauseIcon.SetActive(!isPaused);
			}
		}

		// ================================================================
		// IDragHandler — allow dragging the overlay window via the panel
		// ================================================================

		/// <summary>
		/// Captures offset at drag start.
		/// </summary>
		public void OnBeginDrag(PointerEventData eventData)
		{
			if (_overlayRoot == null)
			{
				return;
			}

			RectTransformUtility.ScreenPointToLocalPointInRectangle(
				_overlayRoot, eventData.position, eventData.pressEventCamera, out _dragOffset);
		}

		/// <summary>
		/// Moves the overlay panel within the canvas.
		/// </summary>
		public void OnDrag(PointerEventData eventData)
		{
			if (_overlayRoot == null)
			{
				return;
			}

			_overlayRoot.position = (Vector2)Input.mousePosition - _dragOffset;
		}

		// ================================================================
		// Internals
		// ================================================================

		/// <summary>
		/// Wires button click listeners to the callback delegates.
		/// </summary>
		private void WireButtons()
		{
			if (_previousButton != null)
			{
				_previousButton.onClick.AddListener(() => OnPreviousClicked?.Invoke());
			}

			if (_playPauseButton != null)
			{
				_playPauseButton.onClick.AddListener(() => OnPlayPauseClicked?.Invoke());
			}

			if (_nextButton != null)
			{
				_nextButton.onClick.AddListener(() => OnNextClicked?.Invoke());
			}

			if (_stopButton != null)
			{
				_stopButton.onClick.AddListener(() => OnStopClicked?.Invoke());
			}

			if (_closeButton != null)
			{
				_closeButton.onClick.AddListener(() => OnCloseClicked?.Invoke());
			}
		}
	}
}
