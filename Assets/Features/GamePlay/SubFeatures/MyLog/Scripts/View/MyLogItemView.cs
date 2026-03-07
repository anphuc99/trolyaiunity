using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Features.GamePlay.SubFeatures.MyLog.View
{
    /// <summary>
    /// View for one MyLog item entry.
    /// </summary>
    public class MyLogItemView : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _dateText;

        [SerializeField]
        private TextMeshProUGUI _contentText;

        [SerializeField]
        private Button _editButton;

        public int LogId { get; private set; }

        /// <summary>
        /// Raised when user requests edit for this item.
        /// </summary>
        public event Action<MyLogItemView> EditRequested;

        private void Awake()
        {
            if (_editButton != null)
            {
                _editButton.onClick.RemoveAllListeners();
                _editButton.onClick.AddListener(HandleEditClicked);
            }
        }

        private void OnDestroy()
        {
            if (_editButton != null)
            {
                _editButton.onClick.RemoveListener(HandleEditClicked);
            }
        }

        /// <summary>
        /// Binds log data to this item view.
        /// </summary>
        /// <param name="logId">Log id.</param>
        /// <param name="content">Log content.</param>
        /// <param name="createdAt">Log created datetime in ISO string format.</param>
        public void Bind(int logId, string content, string createdAt)
        {
            LogId = logId;

            if (_dateText != null)
            {
                _dateText.text = FormatDate(createdAt);
            }

            if (_contentText != null)
            {
                _contentText.text = content ?? string.Empty;
            }
        }

        private static string FormatDate(string createdAt)
        {
            if (string.IsNullOrWhiteSpace(createdAt))
            {
                return string.Empty;
            }

            if (DateTime.TryParse(createdAt, out var parsedDate))
            {
                return parsedDate.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            }

            return createdAt;
        }

        private void HandleEditClicked()
        {
            EditRequested?.Invoke(this);
        }
    }

}