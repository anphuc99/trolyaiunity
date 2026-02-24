using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.Chat.View
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

		private string _messageId;
		private MessageBubbleData _boundData;
		private Action<MessageBubbleData> _onSpeakerClicked;

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
			}
		}

		private void OnDestroy()
		{
			if (_speakerButton != null)
			{
				_speakerButton.onClick.RemoveListener(HandleSpeakerClicked);
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
				_speakerButton.interactable = !string.IsNullOrWhiteSpace(data.Message);
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
			if (_boundData == null)
			{
				return;
			}

			_onSpeakerClicked?.Invoke(_boundData);
		}

        
	}
}
