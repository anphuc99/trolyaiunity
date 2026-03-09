using Core.Infrastructure.Views;
using Core.Infrastructure.Network;
using Core.Infrastructure.Scenes;
using Features.JournalOverlay.Events;
using Features.JournalOverlay.Infrastructure;
using Features.JournalOverlay.Infrastructure.Attributes;
using Features.JournalOverlay.Model;
using Features.JournalOverlay.Requests;
using Share.Utils;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Features.JournalOverlay.View
{
	/// <summary>
	/// View for JournalOverlay.
	/// Manages the overlay UI, audio playback, and window manipulation (borderless, always-on-top).
	/// On <see cref="OnEnabled"/>, automatically starts playback by sending <see cref="JournalOverlayRequests.StartPlayback"/>.
	/// </summary>
	public sealed class JournalOverlayView : BaseView
	{
		// --- UI references (wired in scene) ---

		[Header("Message display")]
		[SerializeField] private TMP_Text _senderNameText;
		[SerializeField] private TMP_Text _messageText;
		[SerializeField] private TMP_Text _translationText;

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

		[Header("Loading")]
		[SerializeField] private GameObject _loadingIndicator;

		[Header("Audio")]
		[SerializeField] private AudioSource _voiceAudioSource;

		// --- Private state ---
		private Coroutine _autoPlayCoroutine;
		private bool _isAutoPlaying;
		private NetworkSettings _networkSettings;
		private string _normalizedServerBaseUrl;

		// ================================================================
		// BaseView lifecycle
		// ================================================================

		/// <summary>
		/// Called when the scope activates. Wires buttons and requests playback start.
		/// </summary>
		protected override void OnEnabled()
		{
			base.OnEnabled();
			WireButtons();

			// Enter floating mode immediately.
			EnterFloatingMode();

			// Start playback (controller will load data from server).
			SendRequest(JournalOverlayRequests.StartPlayback);
		}

		// ================================================================
		// Event handlers
		// ================================================================

		/// <summary>
		/// Shows or hides the loading indicator.
		/// </summary>
		/// <param name="payload">Boolean: true = loading, false = done.</param>
		[OnEvent(JournalOverlayEvents.Loading)]
		private void OnLoading(object payload)
		{
			if (payload is bool isLoading && _loadingIndicator != null)
			{
				_loadingIndicator.SetActive(isLoading);
			}
		}

		/// <summary>
		/// Handles playback started — shows controls, hides loading.
		/// </summary>
		/// <param name="payload">Playback state payload.</param>
		[OnEvent(JournalOverlayEvents.PlaybackStarted)]
		private void OnPlaybackStarted(object payload)
		{
			if (_loadingIndicator != null)
			{
				_loadingIndicator.SetActive(false);
			}

			UpdatePauseIcon(false);
		}

		/// <summary>
		/// Handles playback stopped — stops audio, shows completion state.
		/// </summary>
		/// <param name="payload">Playback state payload.</param>
		[OnEvent(JournalOverlayEvents.PlaybackStopped)]
		private void OnPlaybackStopped(object payload)
		{
			StopAutoPlayCoroutine();
			if (_voiceAudioSource != null)
			{
				_voiceAudioSource.Stop();
			}

			UpdateProgress(0, 0);
			if (_senderNameText != null) _senderNameText.text = string.Empty;
			if (_messageText != null) _messageText.text = "Playback finished";
			if (_translationText != null) _translationText.text = string.Empty;
		}

		/// <summary>
		/// Handles playback paused.
		/// </summary>
		/// <param name="payload">Playback state payload.</param>
		[OnEvent(JournalOverlayEvents.PlaybackPaused)]
		private void OnPlaybackPaused(object payload)
		{
			if (_voiceAudioSource != null)
			{
				_voiceAudioSource.Pause();
			}

			UpdatePauseIcon(true);
		}

		/// <summary>
		/// Handles playback resumed.
		/// </summary>
		/// <param name="payload">Playback state payload.</param>
		[OnEvent(JournalOverlayEvents.PlaybackResumed)]
		private void OnPlaybackResumed(object payload)
		{
			if (_voiceAudioSource != null)
			{
				_voiceAudioSource.UnPause();
			}

			UpdatePauseIcon(false);
		}

		/// <summary>
		/// Handles the current playback message changing. Downloads and plays audio.
		/// </summary>
		/// <param name="payload">Playback message changed payload.</param>
		[OnEvent(JournalOverlayEvents.PlaybackMessageChanged)]
		private void OnPlaybackMessageChanged(object payload)
		{
			if (payload is not OverlayPlaybackMessagePayload messagePayload)
			{
				return;
			}

			// Update UI with message content.
			if (_senderNameText != null && messagePayload.CurrentItem != null)
			{
				_senderNameText.text = messagePayload.CurrentItem.SenderName ?? string.Empty;
			}

			if (_messageText != null && messagePayload.CurrentItem != null)
			{
				_messageText.text = messagePayload.CurrentItem.Text ?? string.Empty;
			}

			if (_translationText != null && messagePayload.CurrentItem != null)
			{
				_translationText.text = messagePayload.CurrentItem.Translation ?? string.Empty;
			}

			UpdateProgress(messagePayload.CurrentIndex, messagePayload.TotalCount);

			// Stop previous auto-play coroutine and start new one.
			StopAutoPlayCoroutine();
			_autoPlayCoroutine = StartCoroutine(AutoPlayMessageAsync(messagePayload));
		}

		/// <summary>
		/// Handles errors — shows error text.
		/// </summary>
		/// <param name="payload">Error payload.</param>
		[OnEvent(JournalOverlayEvents.RequestFailed)]
		private void OnRequestFailed(object payload)
		{
			var message = (payload as OverlayErrorPayload)?.Message;
			if (string.IsNullOrWhiteSpace(message))
			{
				message = "An error occurred.";
			}

			Debug.LogError("[JournalOverlayView] " + message, this);
			if (_messageText != null)
			{
				_messageText.text = message;
			}
		}

		/// <summary>
		/// Handles close requested — restores window and unloads scene.
		/// </summary>
		/// <param name="payload">Unused.</param>
		[OnEvent(JournalOverlayEvents.CloseRequested)]
		private void OnCloseRequested(object payload)
		{
			StopAutoPlayCoroutine();
			if (_voiceAudioSource != null)
			{
				_voiceAudioSource.Stop();
			}

			ExitFloatingMode();

			// Unload this additive scene to return to GamePlay.
			LoadScene.UnloadByScope(Core.Infrastructure.Attributes.ControllerScopeKey.JournalOverlayGameplay);
		}

		// ================================================================
		// Auto-play coroutine
		// ================================================================

		/// <summary>
		/// Downloads and plays audio for a single playback queue message, then
		/// sends AdvancePlayback to move to the next.
		/// </summary>
		/// <param name="messagePayload">Current message data with audio URL.</param>
		private IEnumerator AutoPlayMessageAsync(OverlayPlaybackMessagePayload messagePayload)
		{
			_isAutoPlaying = true;

			if (string.IsNullOrWhiteSpace(messagePayload.AudioUrl))
			{
				// No audio available — skip to next immediately.
				_isAutoPlaying = false;
				SendRequest(JournalOverlayRequests.AdvancePlayback);
				yield break;
			}

			var resolvedUrl = AudioUrlUtils.ResolveAudioUrl(messagePayload.AudioUrl, GetNormalizedServerBaseUrl());
			if (string.IsNullOrWhiteSpace(resolvedUrl))
			{
				_isAutoPlaying = false;
				SendRequest(JournalOverlayRequests.AdvancePlayback);
				yield break;
			}

			using var audioRequest = UnityWebRequestMultimedia.GetAudioClip(resolvedUrl, AudioUrlUtils.ResolveAudioType(resolvedUrl));
			yield return audioRequest.SendWebRequest();

			if (audioRequest.result != UnityWebRequest.Result.Success)
			{
				Debug.LogWarning("[JournalOverlayView] Audio download failed: " + audioRequest.error, this);
				_isAutoPlaying = false;
				SendRequest(JournalOverlayRequests.AdvancePlayback);
				yield break;
			}

			var clip = DownloadHandlerAudioClip.GetContent(audioRequest);
			if (clip == null)
			{
				_isAutoPlaying = false;
				SendRequest(JournalOverlayRequests.AdvancePlayback);
				yield break;
			}

			EnsureAudioSource();
			_voiceAudioSource.Stop();
			_voiceAudioSource.clip = clip;
			_voiceAudioSource.Play();

			// Wait until clip finishes (respecting pause state).
			while (_voiceAudioSource.isPlaying || JournalOverlayState.IsPaused)
			{
				yield return null;
			}

			_isAutoPlaying = false;
			SendRequest(JournalOverlayRequests.AdvancePlayback);
		}

		/// <summary>
		/// Stops and clears the auto-play coroutine if running.
		/// </summary>
		private void StopAutoPlayCoroutine()
		{
			if (_autoPlayCoroutine != null)
			{
				StopCoroutine(_autoPlayCoroutine);
				_autoPlayCoroutine = null;
			}
			_isAutoPlaying = false;
		}

		// ================================================================
		// Floating window management
		// ================================================================

		/// <summary>
		/// Saves the current window state and enters floating overlay mode
		/// (borderless, always-on-top, resized to a compact overlay).
		/// </summary>
		private void EnterFloatingMode()
		{
			const int overlayWidth = 420;
			const int overlayHeight = 300;
			const int screenMargin = 20;

			NativeWindowManager.SaveWindowState();
			NativeWindowManager.SetBorderless(true);

			var screenWidth = Display.main.systemWidth;
			var screenHeight = Display.main.systemHeight;
			var x = screenWidth - overlayWidth - screenMargin;
			var y = screenHeight - overlayHeight - screenMargin;

			NativeWindowManager.ResizeAndPosition(x, y, overlayWidth, overlayHeight);
			NativeWindowManager.SetAlwaysOnTop(true);
		}

		/// <summary>
		/// Restores the window to its original state.
		/// </summary>
		private void ExitFloatingMode()
		{
			if (NativeWindowManager.HasSavedState)
			{
				NativeWindowManager.RestoreWindowState();
			}
		}

		// ================================================================
		// UI helpers
		// ================================================================

		/// <summary>
		/// Wires button click listeners.
		/// </summary>
		private void WireButtons()
		{
			if (_previousButton != null)
			{
				_previousButton.onClick.RemoveAllListeners();
				_previousButton.onClick.AddListener(() => SendRequest(JournalOverlayRequests.PreviousMessage));
			}

			if (_playPauseButton != null)
			{
				_playPauseButton.onClick.RemoveAllListeners();
				_playPauseButton.onClick.AddListener(HandlePlayPauseClicked);
			}

			if (_nextButton != null)
			{
				_nextButton.onClick.RemoveAllListeners();
				_nextButton.onClick.AddListener(() => SendRequest(JournalOverlayRequests.NextMessage));
			}

			if (_stopButton != null)
			{
				_stopButton.onClick.RemoveAllListeners();
				_stopButton.onClick.AddListener(() => SendRequest(JournalOverlayRequests.StopPlayback));
			}

			if (_closeButton != null)
			{
				_closeButton.onClick.RemoveAllListeners();
				_closeButton.onClick.AddListener(() => SendRequest(JournalOverlayRequests.Close));
			}
		}

		/// <summary>
		/// Toggles between pause and resume based on current state.
		/// </summary>
		private void HandlePlayPauseClicked()
		{
			if (JournalOverlayState.IsPaused)
			{
				SendRequest(JournalOverlayRequests.ResumePlayback);
			}
			else
			{
				SendRequest(JournalOverlayRequests.PausePlayback);
			}
		}

		/// <summary>
		/// Updates the play/pause icon visibility.
		/// </summary>
		/// <param name="isPaused">True when paused (show play icon).</param>
		private void UpdatePauseIcon(bool isPaused)
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

		/// <summary>
		/// Updates the progress text display.
		/// </summary>
		/// <param name="currentIndex">Zero-based current index.</param>
		/// <param name="total">Total items.</param>
		private void UpdateProgress(int currentIndex, int total)
		{
			if (_progressText != null)
			{
				_progressText.text = total > 0
					? (currentIndex + 1) + " / " + total
					: string.Empty;
			}
		}

		// ================================================================
		// Audio URL resolution
		// ================================================================

		/// <summary>
		/// Returns the cached normalized server base URL (without trailing /api).
		/// </summary>
		private string GetNormalizedServerBaseUrl()
		{
			if (_normalizedServerBaseUrl == null)
			{
				if (_networkSettings == null)
				{
					_networkSettings = Resources.Load<NetworkSettings>("NetworkSettings");
				}

				_normalizedServerBaseUrl = AudioUrlUtils.NormalizeServerBaseUrl(
					_networkSettings != null ? _networkSettings.BaseUrl : null) ?? string.Empty;
			}

			return string.IsNullOrWhiteSpace(_normalizedServerBaseUrl) ? null : _normalizedServerBaseUrl;
		}

		/// <summary>
		/// Ensures the AudioSource component is available.
		/// </summary>
		private void EnsureAudioSource()
		{
			if (_voiceAudioSource != null)
			{
				return;
			}

			_voiceAudioSource = GetComponent<AudioSource>();
			if (_voiceAudioSource == null)
			{
				_voiceAudioSource = gameObject.AddComponent<AudioSource>();
			}
		}
	}
}
