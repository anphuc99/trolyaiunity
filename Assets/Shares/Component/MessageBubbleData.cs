using System;
using UnityEngine;

namespace Share.Components
{
	/// <summary>
	/// Sender type for chat message bubble.
	/// </summary>
	public enum MessageBubbleType
	{
		Character = 0,
		User = 1,
	}

	/// <summary>
	/// View data for one chat message bubble.
	/// </summary>
	[Serializable]
	public sealed class MessageBubbleData
	{
		[SerializeField]
		private string _messageId;

		[SerializeField]
		private MessageBubbleType _type = MessageBubbleType.Character;

		[SerializeField]
		private string _senderName;

		[SerializeField]
		[TextArea(1, 6)]
		private string _message;

		[SerializeField]
		private Sprite _avatar;

		[SerializeField]
		private string _tone;

		[SerializeField]
		private string _originalMessage;

		[SerializeField]
		[TextArea(1, 6)]
		private string _translation;

		[SerializeField]
		private bool _isTranslationExpanded;

		[SerializeField]
		private bool _isTtsReloading;

		[SerializeField]
		private bool _isTtsPlaying;

		[SerializeField]
		private int _messageIndex = -1;

		/// <summary>
		/// Unique message identifier.
		/// </summary>
		public string MessageId
		{
			get => _messageId;
			set => _messageId = value;
		}

		/// <summary>
		/// Bubble type.
		/// </summary>
		public MessageBubbleType Type
		{
			get => _type;
			set => _type = value;
		}

		/// <summary>
		/// Sender display name.
		/// </summary>
		public string SenderName
		{
			get => _senderName;
			set => _senderName = value;
		}

		/// <summary>
		/// Message text.
		/// </summary>
		public string Message
		{
			get => _message;
			set => _message = value;
		}

		/// <summary>
		/// Sender avatar.
		/// </summary>
		public Sprite Avatar
		{
			get => _avatar;
			set => _avatar = value;
		}

		/// <summary>
		/// Optional tone hint for text-to-speech playback.
		/// </summary>
		public string Tone
		{
			get => _tone;
			set => _tone = value;
		}

		/// <summary>
		/// Original message text before translation expansion.
		/// </summary>
		public string OriginalMessage
		{
			get => _originalMessage;
			set => _originalMessage = value;
		}

		/// <summary>
		/// Translation text for this message.
		/// </summary>
		public string Translation
		{
			get => _translation;
			set => _translation = value;
		}

		/// <summary>
		/// Indicates whether translation is currently expanded.
		/// </summary>
		public bool IsTranslationExpanded
		{
			get => _isTranslationExpanded;
			set => _isTranslationExpanded = value;
		}

		/// <summary>
		/// Indicates whether this message is currently force-reloading TTS.
		/// </summary>
		public bool IsTtsReloading
		{
			get => _isTtsReloading;
			set => _isTtsReloading = value;
		}

		/// <summary>
		/// Indicates whether TTS audio is currently playing for this message.
		/// </summary>
		public bool IsTtsPlaying
		{
			get => _isTtsPlaying;
			set => _isTtsPlaying = value;
		}

		/// <summary>
		/// Runtime index of message in current container list.
		/// </summary>
		public int MessageIndex
		{
			get => _messageIndex;
			set => _messageIndex = value;
		}
	}
}
