using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.Task.Events;
using Features.GamePlay.SubFeatures.Task.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Task.Model;
using Features.GamePlay.SubFeatures.Task.Requests;
using TMPro;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Task.View
{
	/// <summary>
	/// View for Task.
	/// </summary>
	public sealed class TaskView : BaseView
	{
		[SerializeField]
		private TextMeshProUGUI _date;
		[SerializeField]
		private TextMeshProUGUI _totalCount;
		[SerializeField]
		private TaskContainer _taskContainer;

		/// <summary>
		/// Shows this subfeature view when its controller is installed.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(TaskEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
			EnsureDependencies();
			SendRequest(TaskRequests.LoadToday, null);
		}

		/// <summary>
		/// Hides this subfeature view when its controller is uninstalled.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(TaskEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			ClearViewState();
			gameObject.SetActive(false);
		}

		protected override void OnEnabled()
		{
			EnsureDependencies();
		}

		/// <summary>
		/// Handles daily tasks loaded from server.
		/// </summary>
		/// <param name="payload">Task today view payload.</param>
		[OnEvent(TaskEvents.TodayLoaded)]
		private void OnTodayLoaded(object payload)
		{
			if (payload is not TaskTodayViewPayload taskPayload)
			{
				return;
			}

			if (_date != null)
			{
				_date.text = string.IsNullOrWhiteSpace(taskPayload.Date) ? string.Empty : taskPayload.Date;
			}

			if (_totalCount != null)
			{
				_totalCount.text = "Tổng: " + taskPayload.CompletedCount + "/" + taskPayload.TotalCount;
			}

			_taskContainer?.BindTasks(taskPayload.Tasks);
		}

		/// <summary>
		/// Logs task request failures.
		/// </summary>
		/// <param name="payload">Task error payload.</param>
		[OnEvent(TaskEvents.RequestFailed)]
		private void OnRequestFailed(object payload)
		{
			var error = payload as TaskErrorPayload;
			Debug.LogWarning("[TaskView] Request failed: " + (error?.Message ?? "Unknown error"), this);
		}

		private void EnsureDependencies()
		{
			if (_taskContainer == null)
			{
				_taskContainer = GetComponentInChildren<TaskContainer>(true);
			}
		}

		private void ClearViewState()
		{
			if (_date != null)
			{
				_date.text = string.Empty;
			}

			if (_totalCount != null)
			{
				_totalCount.text = string.Empty;
			}

			_taskContainer?.BindTasks(null);
		}
	}
}
