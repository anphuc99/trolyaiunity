using Core.Infrastructure.Views;
using Features.GamePlay.SubFeatures.MyLog.Model;
using Features.GamePlay.SubFeatures.MyLog.Events;
using Features.GamePlay.SubFeatures.MyLog.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.MyLog.Requests;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.GamePlay.SubFeatures.MyLog.View
{
	/// <summary>
	/// View for MyLog.
	/// </summary>
	public sealed class MyLogView : BaseView
	{
		private const string CreateTitleText = "Viết nhật ký mới";
		private const string EditTitleText = "Chỉnh sửa nhật ký";
		private const string CreateButtonText = "Lưu nhật ký";
		private const string EditButtonText = "Chỉnh sửa nhật ký";

		[Header("Tạo nhật ký mới")]
		[SerializeField]
		private TextMeshProUGUI _title;
		[SerializeField]
		private TMP_InputField _inputNewLog;
		[SerializeField]
		private Button _btnCreateNewLog;
		[SerializeField]
		private TMP_Text _btnCreateNewLogText;

		[Header("Danh sách nhật ký")]
		[SerializeField]
		private MyLogItemView _itemViewPrefab;
		[SerializeField]
		private Transform _itemViewContainer;

		private readonly List<MyLogItemView> _spawnedItems = new List<MyLogItemView>();
		private int? _editingLogId;

		protected override void OnEnabled()
		{
			EnsureBindings();
			BindCreateButton();
			SetCreateMode();
			SendRequest(MyLogRequests.LoadLogs);
		}

		/// <summary>
		/// Shows this subfeature view when installed.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(MyLogEvents.Installed)]
		private void OnInstalled(object payload)
		{
			gameObject.SetActive(true);
			EnsureBindings();
			BindCreateButton();
			SetCreateMode();
			SendRequest(MyLogRequests.LoadLogs);
		}

		/// <summary>
		/// Hides this subfeature view when uninstalled.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[OnEvent(MyLogEvents.Uninstalled)]
		private void OnUninstalled(object payload)
		{
			ClearItems();
			gameObject.SetActive(false);
		}

		/// <summary>
		/// Renders list of logs into the UI container.
		/// </summary>
		/// <param name="payload">List payload.</param>
		[OnEvent(MyLogEvents.LogsLoaded)]
		private void OnLogsLoaded(object payload)
		{
			if (payload is not MyLogListResponsePayload response)
			{
				return;
			}

			EnsureBindings();
			RenderLogs(response.Logs);
		}

		/// <summary>
		/// Loads selected log into edit UI.
		/// </summary>
		/// <param name="payload">Edit payload.</param>
		[OnEvent(MyLogEvents.LogEditLoaded)]
		private void OnLogEditLoaded(object payload)
		{
			if (payload is not MyLogEditPayload editPayload)
			{
				return;
			}

			EnsureBindings();
			SetEditMode(editPayload);
		}

		/// <summary>
		/// Resets UI to create mode after save.
		/// </summary>
		/// <param name="payload">Saved payload.</param>
		[OnEvent(MyLogEvents.LogSaved)]
		private void OnLogSaved(object payload)
		{
			EnsureBindings();
			SetCreateMode();
		}

		/// <summary>
		/// Logs request failures.
		/// </summary>
		/// <param name="payload">Error payload.</param>
		[OnEvent(MyLogEvents.RequestFailed)]
		private void OnRequestFailed(object payload)
		{
			var message = (payload as MyLogErrorPayload)?.Message;
			if (string.IsNullOrWhiteSpace(message))
			{
				message = "MyLog request failed.";
			}

			Debug.LogWarning("[MyLogView] " + message, this);
		}

		private void EnsureBindings()
		{
			if (_btnCreateNewLogText == null && _btnCreateNewLog != null)
			{
				_btnCreateNewLogText = _btnCreateNewLog.GetComponentInChildren<TMP_Text>(true);
			}
		}

		private void BindCreateButton()
		{
			if (_btnCreateNewLog == null)
			{
				return;
			}

			_btnCreateNewLog.onClick.RemoveAllListeners();
			_btnCreateNewLog.onClick.AddListener(HandleCreateOrUpdateClicked);
		}

		private void HandleCreateOrUpdateClicked()
		{
			if (_editingLogId.HasValue)
			{
				SendRequest(MyLogRequests.UpdateLog, new MyLogUpdateRequestPayload
				{
					LogId = _editingLogId.Value,
					Content = _inputNewLog != null ? _inputNewLog.text : string.Empty
				});
			}
			else
			{
				SendRequest(MyLogRequests.CreateLog, new MyLogCreateRequestPayload
				{
					Content = _inputNewLog != null ? _inputNewLog.text : string.Empty
				});
			}
		}

		private void RenderLogs(List<MyLogPayload> logs)
		{
			ClearItems();

			if (_itemViewContainer == null || _itemViewPrefab == null)
			{
				return;
			}

			_itemViewPrefab.gameObject.SetActive(false);
			if (logs == null || logs.Count == 0)
			{
				return;
			}

			for (var i = 0; i < logs.Count; i++)
			{
				var log = logs[i];
				if (log == null)
				{
					continue;
				}

				var instance = Instantiate(_itemViewPrefab, _itemViewContainer);
				instance.name = "MyLogItem-" + log.Id;
				instance.gameObject.SetActive(true);
				instance.Bind(log.Id, log.Content);
				instance.EditRequested += HandleEditRequested;
				_spawnedItems.Add(instance);
			}
		}

		private void ClearItems()
		{
			for (var i = 0; i < _spawnedItems.Count; i++)
			{
				if (_spawnedItems[i] != null)
				{
					Destroy(_spawnedItems[i].gameObject);
				}
			}

			_spawnedItems.Clear();
		}

		private void HandleEditRequested(MyLogItemView item)
		{
			if (item == null || item.LogId <= 0)
			{
				return;
			}

			SendRequest(MyLogRequests.EditLog, new MyLogEditRequestPayload
			{
				LogId = item.LogId
			});
		}

		private void SetCreateMode()
		{
			_editingLogId = null;
			SetTitleAndButton(CreateTitleText, CreateButtonText);

			if (_inputNewLog != null)
			{
				_inputNewLog.text = string.Empty;
			}
		}

		private void SetEditMode(MyLogEditPayload payload)
		{
			_editingLogId = payload.LogId;
			SetTitleAndButton(EditTitleText, EditButtonText);

			if (_inputNewLog != null)
			{
				_inputNewLog.text = payload.Content ?? string.Empty;
			}
		}

		private void SetTitleAndButton(string title, string buttonText)
		{
			if (_title != null)
			{
				_title.text = title ?? string.Empty;
			}

			if (_btnCreateNewLogText != null)
			{
				_btnCreateNewLogText.text = buttonText ?? string.Empty;
			}
		}
	}
}
