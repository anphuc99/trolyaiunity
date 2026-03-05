using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Infrastructure.Network;
using Features.GamePlay.SubFeatures.MyLog.Controller;
using Features.GamePlay.SubFeatures.MyLog.Events;
using Features.GamePlay.SubFeatures.MyLog.Model;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Features.GamePlay.SubFeatures.MyLog.Tests
{
	/// <summary>
	/// Tests for MyLog controller request and event flow.
	/// </summary>
	public sealed class MyLogControllerTests
	{
		[TearDown]
		public void TearDown()
		{
			FakeServer.ResetToDefaults();
			MyLogState.CachedList = new MyLogListResponsePayload();
			MyLogState.EditingLogId = null;
			MyLogController.SetParentSignals(null);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleLoadLogs_FromServer_PublishesLogsLoaded()
		{
			FakeServer.Register("GET", NetworkEndpoints.MyLog,
				_ => "{\"logs\":[{\"id\":1,\"content\":\"Nhật ký 1\"}],\"total\":1,\"page\":1,\"limit\":20,\"hasMore\":false}"
			);

			MyLogListResponsePayload loadedPayload = null;
			void Handler(object payload)
			{
				loadedPayload = payload as MyLogListResponsePayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(MyLogEvents.LogsLoaded, Handler);
			try
			{
				MyLogController.HandleLoadLogs(null);
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(MyLogEvents.LogsLoaded, Handler);
			}

			Assert.IsNotNull(loadedPayload);
			Assert.AreEqual(1, loadedPayload.Logs.Count);
			Assert.AreEqual("Nhật ký 1", loadedPayload.Logs[0].Content);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleCreateLog_ValidPayload_CallsServerAndPublishes()
		{
			FakeServer.Register("POST", NetworkEndpoints.MyLog,
				_ => "{\"id\":2,\"content\":\"Nội dung mới\"}"
			);
			FakeServer.Register("GET", NetworkEndpoints.MyLog,
				_ => "{\"logs\":[{\"id\":2,\"content\":\"Nội dung mới\"}],\"total\":1,\"page\":1,\"limit\":20,\"hasMore\":false}"
			);

			MyLogSavedPayload savedPayload = null;
			MyLogListResponsePayload loadedPayload = null;

			void SavedHandler(object payload)
			{
				savedPayload = payload as MyLogSavedPayload;
			}

			void LoadedHandler(object payload)
			{
				loadedPayload = payload as MyLogListResponsePayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(MyLogEvents.LogSaved, SavedHandler);
			Core.Infrastructure.Events.EventBus.Subscribe(MyLogEvents.LogsLoaded, LoadedHandler);
			try
			{
				MyLogController.HandleCreateLog(new MyLogCreateRequestPayload
				{
					Content = "Nội dung mới"
				});
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(MyLogEvents.LogSaved, SavedHandler);
				Core.Infrastructure.Events.EventBus.Unsubscribe(MyLogEvents.LogsLoaded, LoadedHandler);
			}

			Assert.IsNotNull(savedPayload);
			Assert.IsFalse(savedPayload.IsUpdate);
			Assert.IsNotNull(loadedPayload);
			Assert.AreEqual(1, loadedPayload.Logs.Count);
			Assert.AreEqual("Nội dung mới", loadedPayload.Logs[0].Content);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleEditLog_FromServer_PublishesEditPayload()
		{
			FakeServer.Register("GET", NetworkEndpoints.MyLog + "/9",
				_ => "{\"id\":9,\"content\":\"Cần chỉnh sửa\"}"
			);

			MyLogEditPayload editPayload = null;
			void Handler(object payload)
			{
				editPayload = payload as MyLogEditPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(MyLogEvents.LogEditLoaded, Handler);
			try
			{
				MyLogController.HandleEditLog(new MyLogEditRequestPayload { LogId = 9 });
				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(MyLogEvents.LogEditLoaded, Handler);
			}

			Assert.IsNotNull(editPayload);
			Assert.AreEqual(9, editPayload.LogId);
			Assert.AreEqual("Cần chỉnh sửa", editPayload.Content);
		}

		[Test]
		public void HandleUpdateLog_InvalidId_PublishesError()
		{
			MyLogErrorPayload errorPayload = null;
			void Handler(object payload)
			{
				errorPayload = payload as MyLogErrorPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(MyLogEvents.RequestFailed, Handler);
			try
			{
				MyLogController.HandleUpdateLog(new MyLogUpdateRequestPayload
				{
					LogId = -1,
					Content = "Invalid"
				});
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(MyLogEvents.RequestFailed, Handler);
			}

			Assert.IsNotNull(errorPayload);
		}

		[UnityTest]
		public System.Collections.IEnumerator HandleUpdateLog_ValidPayload_CallsServerAndPublishes()
		{
			FakeServer.Register("PUT", NetworkEndpoints.MyLog + "/3",
				_ => "{\"id\":3,\"content\":\"Đã chỉnh sửa\"}"
			);
			FakeServer.Register("GET", NetworkEndpoints.MyLog,
				_ => "{\"logs\":[{\"id\":3,\"content\":\"Đã chỉnh sửa\"}],\"total\":1,\"page\":1,\"limit\":20,\"hasMore\":false}"
			);

			MyLogSavedPayload savedPayload = null;
			void Handler(object payload)
			{
				savedPayload = payload as MyLogSavedPayload;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(MyLogEvents.LogSaved, Handler);
			try
			{
				MyLogController.HandleUpdateLog(new MyLogUpdateRequestPayload
				{
					LogId = 3,
					Content = "Đã chỉnh sửa"
				});

				yield return AwaitTask(Task.Delay(100));
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(MyLogEvents.LogSaved, Handler);
			}

			Assert.IsNotNull(savedPayload);
			Assert.IsTrue(savedPayload.IsUpdate);
			Assert.AreEqual(3, savedPayload.LogId);
		}

		private static System.Collections.IEnumerator AwaitTask(Task task)
		{
			while (!task.IsCompleted)
			{
				yield return null;
			}
		}
	}
}
