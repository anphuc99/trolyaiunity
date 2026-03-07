using Core.Infrastructure.Network;
using Features.GamePlay.SubFeatures.MyLog.Events;
using Features.GamePlay.SubFeatures.MyLog.Infrastructure;
using Features.GamePlay.SubFeatures.MyLog.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.MyLog.Model;
using Features.GamePlay.SubFeatures.MyLog.Requests;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Features.GamePlay.SubFeatures.MyLog.Controller
{
	/// <summary>
	/// Controller for MyLog.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class MyLogController
	{
		/// <summary>
		/// Called when the controller scope is entered.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerInit]
		public static void OnEnterScope()
		{
		}

		/// <summary>
		/// Called when the controller scope is exited.
		/// </summary>
		[Core.Infrastructure.Attributes.ControllerShutdown]
		public static void OnExitScope()
		{
			MyLogState.CachedList = new MyLogListResponsePayload();
			MyLogState.EditingLogId = null;
			MyLogState.ChatMenuId = null;
			MyLogState.JournalMenuId = null;
		}

		/// <summary>
		/// Installs the subcontroller.
		/// </summary>
		public static void Install()
		{
			RegisterChatMenu();
			RegisterJournalMenu();
			EventBus.Publish(MyLogEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls the subcontroller.
		/// </summary>
		public static void Uninstall()
		{
			UnregisterChatMenu();
			UnregisterJournalMenu();
			EventBus.Publish(MyLogEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(MyLogParentSignals signals)
		{
			if (signals == null)
			{
				UnregisterChatMenu();
				UnregisterJournalMenu();
			}

			MyLogState.ParentSignals = signals;
		}

		private static void RegisterChatMenu()
		{
			UnregisterChatMenu();

			var menuId = MyLogState.ParentSignals?.AddMenu?.Invoke("chat", HandleOpenChatMenu);
			if (string.IsNullOrWhiteSpace(menuId))
			{
				return;
			}

			MyLogState.ChatMenuId = menuId;
		}

		private static void UnregisterChatMenu()
		{
			if (string.IsNullOrWhiteSpace(MyLogState.ChatMenuId))
			{
				MyLogState.ChatMenuId = null;
				return;
			}

			MyLogState.ParentSignals?.RemoveMenu?.Invoke(MyLogState.ChatMenuId);
			MyLogState.ChatMenuId = null;
		}

		private static void RegisterJournalMenu()
		{
			UnregisterJournalMenu();

			var menuId = MyLogState.ParentSignals?.AddMenu?.Invoke("journal", HandleOpenJournalMenu);
			if (string.IsNullOrWhiteSpace(menuId))
			{
				return;
			}

			MyLogState.JournalMenuId = menuId;
		}

		private static void UnregisterJournalMenu()
		{
			if (string.IsNullOrWhiteSpace(MyLogState.JournalMenuId))
			{
				MyLogState.JournalMenuId = null;
				return;
			}

			MyLogState.ParentSignals?.RemoveMenu?.Invoke(MyLogState.JournalMenuId);
			MyLogState.JournalMenuId = null;
		}

		private static void HandleOpenChatMenu()
		{
			MyLogState.ParentSignals?.OpenChat?.Invoke();
		}

		private static void HandleOpenJournalMenu()
		{
			MyLogState.ParentSignals?.OpenJournal?.Invoke();
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(MyLogRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(MyLogEvents.Echoed, payload);
			MyLogState.ParentSignals?.OnEchoed?.Invoke(payload);
		}

		/// <summary>
		/// Loads all logs from server.
		/// </summary>
		[Request(MyLogRequests.LoadLogs)]
		public static void HandleLoadLogs(object payload)
		{
			_ = LoadLogsInternalAsync();
		}

		/// <summary>
		/// Creates a new log entry on server.
		/// </summary>
		/// <param name="payload">Create payload.</param>
		[Request(MyLogRequests.CreateLog)]
		public static void HandleCreateLog(MyLogCreateRequestPayload payload)
		{
			if (payload == null || string.IsNullOrWhiteSpace(payload.Content))
			{
				PublishError("Nội dung nhật ký không được để trống.");
				return;
			}

			_ = CreateLogInternalAsync(payload);
		}

		/// <summary>
		/// Updates an existing log entry on server.
		/// </summary>
		/// <param name="payload">Update payload.</param>
		[Request(MyLogRequests.UpdateLog)]
		public static void HandleUpdateLog(MyLogUpdateRequestPayload payload)
		{
			if (payload == null || payload.LogId <= 0)
			{
				PublishError("Id nhật ký không hợp lệ.");
				return;
			}

			if (string.IsNullOrWhiteSpace(payload.Content))
			{
				PublishError("Nội dung nhật ký không được để trống.");
				return;
			}

			_ = UpdateLogInternalAsync(payload);
		}

		/// <summary>
		/// Selects a log for editing and loads latest content from server.
		/// </summary>
		/// <param name="payload">Edit selection payload.</param>
		[Request(MyLogRequests.EditLog)]
		public static void HandleEditLog(MyLogEditRequestPayload payload)
		{
			if (payload == null || payload.LogId <= 0)
			{
				PublishError("Id nhật ký không hợp lệ.");
				return;
			}

			_ = LoadLogForEditInternalAsync(payload.LogId);
		}

		private static async Task LoadLogsInternalAsync()
		{
			try
			{
				var responseJson = await HttpClient.GetTaskAsync(NetworkEndpoints.MyLog);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Không tải được danh sách nhật ký từ server.");
					return;
				}

				var response = JsonConvert.DeserializeObject<MyLogListResponsePayload>(responseJson) ?? new MyLogListResponsePayload();
				response.Logs ??= new List<MyLogPayload>();

				MyLogState.CachedList = response;
				EventBus.Publish(MyLogEvents.LogsLoaded, response);
			}
			catch (Exception exception)
			{
				PublishError("Lỗi tải danh sách nhật ký: " + exception.Message);
			}
		}

		private static async Task CreateLogInternalAsync(MyLogCreateRequestPayload payload)
		{
			try
			{
				var responseJson = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.MyLog, payload);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Không tạo được nhật ký mới.");
					return;
				}

				var saved = JsonConvert.DeserializeObject<MyLogPayload>(responseJson);
				if (saved == null || saved.Id <= 0)
				{
					PublishError("Server trả về dữ liệu nhật ký không hợp lệ.");
					return;
				}

				MyLogState.EditingLogId = null;
				EventBus.Publish(MyLogEvents.LogSaved, new MyLogSavedPayload
				{
					LogId = saved.Id,
					IsUpdate = false
				});

				await LoadLogsInternalAsync();
			}
			catch (Exception exception)
			{
				PublishError("Lỗi tạo nhật ký: " + exception.Message);
			}
		}

		private static async Task UpdateLogInternalAsync(MyLogUpdateRequestPayload payload)
		{
			try
			{
				var endpoint = BuildMyLogByIdEndpoint(payload.LogId);
				var requestBody = new MyLogCreateRequestPayload
				{
					Content = payload.Content
				};
				var responseJson = await HttpClient.PutJsonTaskAsync(endpoint, requestBody);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Không chỉnh sửa được nhật ký.");
					return;
				}

				var saved = JsonConvert.DeserializeObject<MyLogPayload>(responseJson);
				if (saved == null || saved.Id <= 0)
				{
					PublishError("Server trả về dữ liệu nhật ký không hợp lệ.");
					return;
				}

				MyLogState.EditingLogId = null;
				EventBus.Publish(MyLogEvents.LogSaved, new MyLogSavedPayload
				{
					LogId = saved.Id,
					IsUpdate = true
				});

				await LoadLogsInternalAsync();
			}
			catch (Exception exception)
			{
				PublishError("Lỗi chỉnh sửa nhật ký: " + exception.Message);
			}
		}

		private static async Task LoadLogForEditInternalAsync(int logId)
		{
			try
			{
				var endpoint = BuildMyLogByIdEndpoint(logId);
				var responseJson = await HttpClient.GetTaskAsync(endpoint);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Không tải được nhật ký để chỉnh sửa.");
					return;
				}

				var log = JsonConvert.DeserializeObject<MyLogPayload>(responseJson);
				if (log == null || log.Id <= 0)
				{
					PublishError("Không tìm thấy nhật ký để chỉnh sửa.");
					return;
				}

				MyLogState.EditingLogId = log.Id;
				EventBus.Publish(MyLogEvents.LogEditLoaded, new MyLogEditPayload
				{
					LogId = log.Id,
					Content = log.Content
				});
			}
			catch (Exception exception)
			{
				PublishError("Lỗi tải nhật ký để chỉnh sửa: " + exception.Message);
			}
		}

		private static string BuildMyLogByIdEndpoint(int logId)
		{
			return NetworkEndpoints.MyLog + "/" + logId;
		}

		private static void PublishError(string message)
		{
			EventBus.Publish(MyLogEvents.RequestFailed, new MyLogErrorPayload
			{
				Message = message
			});
		}
	}
}
