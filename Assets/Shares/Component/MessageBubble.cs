using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Share.Components
{
	/// <summary>
	/// Message bubble presenter for either user or character prefab.
	/// </summary>
	public sealed class MessageBubble : MonoBehaviour
	{
		[SerializeField]
		private MessageBubbleType _bubbleType = MessageBubbleType.Character;

		[SerializeField]
		private RectTransform _rootRect;

		[SerializeField]
		private TMP_Text _nameText;

		[SerializeField]
		private TMP_Text _messageText;

		[SerializeField]
		private Image _avatarImage;

		[SerializeField]
		private ChatMessageAutoResize _messageAutoResize;

		[SerializeField]
		private Button _speakerButton;

		[SerializeField]
		private Button _translateButton;

		[SerializeField]
		[Min(0.1f)]
		private float _speakerLongPressSeconds = 0.45f;

		private string _messageId;
		private MessageBubbleData _boundData;
		private Action<MessageBubbleData> _onSpeakerClicked;
		private Action<MessageBubbleData> _onSpeakerLongPressed;
		private Action<MessageBubbleData> _onTranslateClicked;
		private Coroutine _speakerLongPressCoroutine;
		private bool _suppressNextSpeakerClick;

		/// <summary>
		/// Type that this prefab instance represents.
		/// </summary>
		public MessageBubbleType BubbleType => _bubbleType;

		/// <summary>
		/// Backing message id currently displayed.
		/// </summary>
		public string MessageId => _messageId;

		/// <summary>
		/// Root rect transform of this bubble.
		/// </summary>
		public RectTransform RootRect => _rootRect;

		private void Reset()
		{
			if (_rootRect == null)
			{
				_rootRect = transform as RectTransform;
			}
		}

		private void Awake()
		{
			if (_speakerButton != null)
			{
				_speakerButton.onClick.AddListener(HandleSpeakerClicked);
				SetupSpeakerLongPressEvents();
			}

			if (_translateButton != null)
			{
				_translateButton.onClick.AddListener(HandleTranslateClicked);
			}
		}

		private void OnDestroy()
		{
			CancelSpeakerLongPress();

			if (_speakerButton != null)
			{
				_speakerButton.onClick.RemoveListener(HandleSpeakerClicked);
			}

			if (_translateButton != null)
			{
				_translateButton.onClick.RemoveListener(HandleTranslateClicked);
			}
		}

		/// <summary>
		/// Configures callback for speaker-button click.
		/// </summary>
		/// <param name="onSpeakerClicked">Callback invoked with currently bound message data.</param>
		public void SetSpeakerClickHandler(Action<MessageBubbleData> onSpeakerClicked)
		{
			_onSpeakerClicked = onSpeakerClicked;
		}

		/// <summary>
		/// Configures callback for speaker-button long press.
		/// </summary>
		/// <param name="onSpeakerLongPressed">Callback invoked with currently bound message data.</param>
		public void SetSpeakerLongPressHandler(Action<MessageBubbleData> onSpeakerLongPressed)
		{
			_onSpeakerLongPressed = onSpeakerLongPressed;
		}

		/// <summary>
		/// Configures callback for translate-button click.
		/// </summary>
		/// <param name="onTranslateClicked">Callback invoked with currently bound message data.</param>
		public void SetTranslateClickHandler(Action<MessageBubbleData> onTranslateClicked)
		{
			_onTranslateClicked = onTranslateClicked;
		}

		/// <summary>
		/// Binds message data and applies width-dependent layout.
		/// </summary>
		/// <param name="data">Message data.</param>
		/// <param name="viewportWidth">Available viewport width (for measurement context).</param>
		public void ApplyData(MessageBubbleData data, float viewportWidth)
		{
			if (data == null)
			{
				return;
			}

			_boundData = data;
			_messageId = data.MessageId ?? string.Empty;

			if (_nameText != null)
			{
				_nameText.text = data.SenderName ?? string.Empty;
				_nameText.gameObject.SetActive(!string.IsNullOrWhiteSpace(_nameText.text));
			}

			if (_messageText != null)
			{
				_messageText.text = data.Message ?? string.Empty;
			}

			if (_avatarImage != null)
			{
				_avatarImage.sprite = data.Avatar;
				_avatarImage.enabled = data.Avatar != null;
			}

			if (_messageAutoResize != null)
			{
				_messageAutoResize.RefreshLayout();
			}

			if (_speakerButton != null)
			{
				_speakerButton.gameObject.SetActive(!data.IsTtsReloading);
				_speakerButton.interactable = !string.IsNullOrWhiteSpace(data.Message);
			}

			if (_translateButton != null)
			{
				_translateButton.interactable = !string.IsNullOrWhiteSpace(data.Translation);
			}

			ForceRebuild();
		}

		/// <summary>
		/// Measures preferred height for provided data in current prefab layout.
		/// </summary>
		/// <param name="data">Message data.</param>
		/// <param name="viewportWidth">Available viewport width.</param>
		/// <returns>Preferred bubble height.</returns>
		public float MeasureHeight(MessageBubbleData data, float viewportWidth)
		{
			ApplyData(data, viewportWidth);
			if (_rootRect == null)
			{
				return 0f;
			}

			// Multiple rebuild passes to ensure layout is stable
			for (var i = 0; i < 2; i++)
			{
				ForceRebuild();
			}

			return Mathf.Max(1f, CalculateTotalHeight());
		}

		/// <summary>
		/// Calculates actual height by measuring world bounds of all descendants.
		/// </summary>
		/// <returns>Total height encompassing all visible elements in local units.</returns>
		private float CalculateTotalHeight()
		{
			if (_rootRect == null)
			{
				return 0f;
			}

			var minY = float.MaxValue;
			var maxY = float.MinValue;
			var corners = new Vector3[4];

			CollectBoundsRecursively(_rootRect, corners, ref minY, ref maxY);

			if (minY == float.MaxValue || maxY == float.MinValue)
			{
				return _rootRect.rect.height;
			}

			// Convert world height to local units by using root's lossyScale
			var worldHeight = Mathf.Abs(maxY - minY);
			var scaleY = _rootRect.lossyScale.y;
			if (Mathf.Approximately(scaleY, 0f))
			{
				return _rootRect.rect.height;
			}

			return worldHeight / Mathf.Abs(scaleY);
		}

		/// <summary>
		/// Recursively collects world bounds of all active RectTransform descendants.
		/// </summary>
		/// <param name="rect">Current RectTransform to process.</param>
		/// <param name="corners">Reusable corners array.</param>
		/// <param name="minY">Reference to minimum Y value.</param>
		/// <param name="maxY">Reference to maximum Y value.</param>
		private void CollectBoundsRecursively(RectTransform rect, Vector3[] corners, ref float minY, ref float maxY)
		{
			if (rect == null || !rect.gameObject.activeInHierarchy)
			{
				return;
			}

			rect.GetWorldCorners(corners);
			for (var i = 0; i < 4; i++)
			{
				if (corners[i].y < minY)
				{
					minY = corners[i].y;
				}

				if (corners[i].y > maxY)
				{
					maxY = corners[i].y;
				}
			}

			for (var i = 0; i < rect.childCount; i++)
			{
				var child = rect.GetChild(i) as RectTransform;
				CollectBoundsRecursively(child, corners, ref minY, ref maxY);
			}
		}

		private void ForceRebuild()
		{
			if (_rootRect == null)
			{
				return;
			}

			Canvas.ForceUpdateCanvases();
			LayoutRebuilder.ForceRebuildLayoutImmediate(_rootRect);
		}

		private void HandleSpeakerClicked()
		{
			if (_suppressNextSpeakerClick)
			{
				_suppressNextSpeakerClick = false;
				return;
			}

			if (_boundData == null)
			{
				return;
			}

			_onSpeakerClicked?.Invoke(_boundData);
		}

		private void SetupSpeakerLongPressEvents()
		{
			if (_speakerButton == null)
			{
				return;
			}

			var trigger = _speakerButton.GetComponent<EventTrigger>();
			if (trigger == null)
			{
				trigger = _speakerButton.gameObject.AddComponent<EventTrigger>();
			}

			if (trigger.triggers == null)
			{
				trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();
			}

			AddEventTrigger(trigger, EventTriggerType.PointerDown, OnSpeakerPointerDown);
			AddEventTrigger(trigger, EventTriggerType.PointerUp, OnSpeakerPointerUpOrExit);
			AddEventTrigger(trigger, EventTriggerType.PointerExit, OnSpeakerPointerUpOrExit);
		}

		private void AddEventTrigger(EventTrigger trigger, EventTriggerType eventType, Action<BaseEventData> handler)
		{
			for (var i = 0; i < trigger.triggers.Count; i++)
			{
				if (trigger.triggers[i].eventID == eventType)
				{
					trigger.triggers[i].callback.AddListener(eventData => handler?.Invoke(eventData));
					return;
				}
			}

			var entry = new EventTrigger.Entry
			{
				eventID = eventType,
				callback = new EventTrigger.TriggerEvent()
			};
			entry.callback.AddListener(eventData => handler?.Invoke(eventData));
			trigger.triggers.Add(entry);
		}

		private void OnSpeakerPointerDown(BaseEventData eventData)
		{
			if (_speakerLongPressCoroutine != null)
			{
				StopCoroutine(_speakerLongPressCoroutine);
			}

			_speakerLongPressCoroutine = StartCoroutine(DetectSpeakerLongPress());
		}

		private void OnSpeakerPointerUpOrExit(BaseEventData eventData)
		{
			CancelSpeakerLongPress();
		}

		private System.Collections.IEnumerator DetectSpeakerLongPress()
		{
			yield return new WaitForSeconds(_speakerLongPressSeconds);

			_speakerLongPressCoroutine = null;
			if (_boundData == null)
			{
				yield break;
			}

			if (_boundData.IsTtsReloading)
			{
				yield break;
			}

			_suppressNextSpeakerClick = true;
			_onSpeakerLongPressed?.Invoke(_boundData);
		}

		private void CancelSpeakerLongPress()
		{
			if (_speakerLongPressCoroutine == null)
			{
				return;
			}

			StopCoroutine(_speakerLongPressCoroutine);
			_speakerLongPressCoroutine = null;
		}

		private void HandleTranslateClicked()
		{
			if (_boundData == null)
			{
				return;
			}

			_onTranslateClicked?.Invoke(_boundData);
		}

        
	}
}
