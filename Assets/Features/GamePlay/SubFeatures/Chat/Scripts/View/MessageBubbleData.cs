using System;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Chat.View
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
	}
}
