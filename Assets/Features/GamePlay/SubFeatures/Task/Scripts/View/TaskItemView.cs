using TMPro;
using Features.GamePlay.SubFeatures.Task.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.Task.View
{
    /// <summary>
    /// Renders one task item row.
    /// </summary>
    public sealed class TaskItemView : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _labelText;
        [SerializeField]
        private TextMeshProUGUI _statusText;
        [SerializeField]
        private Slider _progressSlider;
        [SerializeField]
        private TextMeshProUGUI _remainingText;

        /// <summary>
        /// Task id key bound to this row, taken from GameObject name.
        /// </summary>
        public string TaskId => gameObject != null ? gameObject.name : string.Empty;

        /// <summary>
        /// Binds server task data to this row.
        /// </summary>
        /// <param name="task">Task item payload.</param>
        public void Bind(TaskTodayItemPayload task)
        {
            if (task == null)
            {
                Clear();
                return;
            }

            if (_labelText != null)
            {
                _labelText.text = string.IsNullOrWhiteSpace(task.Label) ? task.Id : task.Label;
            }

            if (_statusText != null)
            {
                _statusText.text = task.Completed ? "Đã hoàn thành" : "Chưa hoàn thành";
            }

            if (_progressSlider != null)
            {
                if (task.Target > 0)
                {
                    _progressSlider.minValue = 0f;
                    _progressSlider.maxValue = task.Target;
                    _progressSlider.value = Mathf.Clamp(task.Progress, 0, task.Target);
                }
                else
                {
                    _progressSlider.minValue = 0f;
                    _progressSlider.maxValue = 1f;
                    _progressSlider.value = task.Completed ? 1f : 0f;
                }
            }

            if (_remainingText != null)
            {
                _remainingText.text = task.Target > 0
                    ? task.Progress + "/" + task.Target + " (còn " + Mathf.Max(0, task.Remaining) + ")"
                    : "Còn " + Mathf.Max(0, task.Remaining);
            }
        }

        /// <summary>
        /// Clears task row values.
        /// </summary>
        public void Clear()
        {
            if (_labelText != null)
            {
                _labelText.text = string.Empty;
            }

            if (_statusText != null)
            {
                _statusText.text = string.Empty;
            }

            if (_remainingText != null)
            {
                _remainingText.text = string.Empty;
            }

            if (_progressSlider != null)
            {
                _progressSlider.minValue = 0f;
                _progressSlider.maxValue = 1f;
                _progressSlider.value = 0f;
            }
        }
    }
}