using System.Collections.Generic;
using Features.GamePlay.SubFeatures.Task.Model;
using UnityEngine;


namespace Features.GamePlay.SubFeatures.Task.View
{
    /// <summary>
    /// Container that binds server tasks to item rows.
    /// </summary>
    public sealed class TaskContainer : MonoBehaviour
    {
        [SerializeField]
        private List<TaskItemView> _taskItemViews = new List<TaskItemView>();

        private void Awake()
        {
            EnsureTaskItems();
        }

        private void OnValidate()
        {
            EnsureTaskItems();
        }

        /// <summary>
        /// Renders tasks to rows by matching task id with TaskItemView GameObject name.
        /// </summary>
        /// <param name="tasks">Task list from server.</param>
        public void BindTasks(List<TaskTodayItemPayload> tasks)
        {
            EnsureTaskItems();

            var byId = new Dictionary<string, TaskTodayItemPayload>(System.StringComparer.OrdinalIgnoreCase);
            if (tasks != null)
            {
                for (var i = 0; i < tasks.Count; i += 1)
                {
                    var task = tasks[i];
                    if (task == null || string.IsNullOrWhiteSpace(task.Id))
                    {
                        continue;
                    }

                    byId[task.Id.Trim()] = task;
                }
            }

            for (var i = 0; i < _taskItemViews.Count; i += 1)
            {
                var itemView = _taskItemViews[i];
                if (itemView == null)
                {
                    continue;
                }

                var key = itemView.TaskId;
                if (!string.IsNullOrWhiteSpace(key) && byId.TryGetValue(key.Trim(), out var matchedTask))
                {
                    itemView.Bind(matchedTask);
                }
                else
                {
                    itemView.Clear();
                }
            }
        }

        private void EnsureTaskItems()
        {
            if (_taskItemViews == null)
            {
                _taskItemViews = new List<TaskItemView>();
            }

            _taskItemViews.Clear();
            _taskItemViews.AddRange(GetComponentsInChildren<TaskItemView>(true));
        }
    }
}