using Features.GamePlay.SubFeatures.CreateSubjects.Controller;
using Features.GamePlay.SubFeatures.CreateSubjects.Events;
using Features.GamePlay.SubFeatures.CreateSubjects.Model;
using NUnit.Framework;

namespace Features.GamePlay.SubFeatures.CreateSubjects.Tests
{
	/// <summary>
	/// Tests for CreateSubjectsController logic and event flow.
	/// </summary>
	public sealed class CreateSubjectsControllerTests
	{
		[TearDown]
		public void TearDown()
		{
			CreateSubjectsController.SetParentSignals(null);
			CreateSubjectsState.Reset();
		}

		[Test]
		public void Install_PublishesInstalledEvent()
		{
			var installed = false;
			void Handler(object payload)
			{
				installed = true;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(CreateSubjectsEvents.Installed, Handler);
			try
			{
				CreateSubjectsController.Install();
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(CreateSubjectsEvents.Installed, Handler);
			}

			Assert.IsTrue(installed);
		}

		[Test]
		public void Install_ResetsIsSubmitting()
		{
			CreateSubjectsState.IsSubmitting = true;

			CreateSubjectsController.Install();

			Assert.IsFalse(CreateSubjectsState.IsSubmitting);
		}

		[Test]
		public void Uninstall_PublishesUninstalledEvent()
		{
			var uninstalled = false;
			void Handler(object payload)
			{
				uninstalled = true;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(CreateSubjectsEvents.Uninstalled, Handler);
			try
			{
				CreateSubjectsController.Uninstall();
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(CreateSubjectsEvents.Uninstalled, Handler);
			}

			Assert.IsTrue(uninstalled);
		}

		[Test]
		public void Uninstall_ResetsState()
		{
			CreateSubjectsState.IsSubmitting = true;

			CreateSubjectsController.Uninstall();

			Assert.IsFalse(CreateSubjectsState.IsSubmitting);
		}

		[Test]
		public void HandleSubmitCreate_PublishesCreateFailed_WhenPayloadIsNull()
		{
			string errorMessage = null;
			void Handler(object payload)
			{
				errorMessage = (payload as CreateSubjectsErrorPayload)?.Message;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(CreateSubjectsEvents.CreateFailed, Handler);
			try
			{
				CreateSubjectsController.HandleSubmitCreate(null);
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(CreateSubjectsEvents.CreateFailed, Handler);
			}

			Assert.IsNotNull(errorMessage);
		}

		[Test]
		public void HandleSubmitCreate_PublishesCreateFailed_WhenNameIsEmpty()
		{
			string errorMessage = null;
			void Handler(object payload)
			{
				errorMessage = (payload as CreateSubjectsErrorPayload)?.Message;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(CreateSubjectsEvents.CreateFailed, Handler);
			try
			{
				CreateSubjectsController.HandleSubmitCreate(new CreateSubjectPayload { Name = "", Description = "test" });
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(CreateSubjectsEvents.CreateFailed, Handler);
			}

			Assert.IsNotNull(errorMessage);
		}

		[Test]
		public void HandleSubmitCreate_PublishesCreateFailed_WhenNameIsWhitespace()
		{
			string errorMessage = null;
			void Handler(object payload)
			{
				errorMessage = (payload as CreateSubjectsErrorPayload)?.Message;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(CreateSubjectsEvents.CreateFailed, Handler);
			try
			{
				CreateSubjectsController.HandleSubmitCreate(new CreateSubjectPayload { Name = "   ", Description = "test" });
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(CreateSubjectsEvents.CreateFailed, Handler);
			}

			Assert.IsNotNull(errorMessage);
		}

		[Test]
		public void HandleCancel_InvokesParentOnCancelled()
		{
			var cancelledCalled = false;
			CreateSubjectsController.SetParentSignals(new CreateSubjectsParentSignals
			{
				OnCancelled = () => cancelledCalled = true,
			});

			CreateSubjectsController.HandleCancel(null);

			Assert.IsTrue(cancelledCalled);
		}

		[Test]
		public void HandleCancel_DoesNotThrow_WhenNoParentSignals()
		{
			CreateSubjectsController.SetParentSignals(null);

			Assert.DoesNotThrow(() => CreateSubjectsController.HandleCancel(null));
		}

		[Test]
		public void HandleCancel_DoesNotThrow_WhenOnCancelledIsNull()
		{
			CreateSubjectsController.SetParentSignals(new CreateSubjectsParentSignals
			{
				OnCancelled = null,
			});

			Assert.DoesNotThrow(() => CreateSubjectsController.HandleCancel(null));
		}

		[Test]
		public void ParseCreateResponse_ReturnsValidPayload()
		{
			var json = "{\"id\":1,\"name\":\"Toán\",\"description\":\"Toán học\"}";

			var result = CreateSubjectsController.ParseCreateResponse(json);

			Assert.IsNotNull(result);
			Assert.AreEqual(1, result.Id);
			Assert.AreEqual("Toán", result.Name);
			Assert.AreEqual("Toán học", result.Description);
		}

		[Test]
		public void ParseCreateResponse_ReturnsPayload_WithNullDescription()
		{
			var json = "{\"id\":2,\"name\":\"Lý\",\"description\":null}";

			var result = CreateSubjectsController.ParseCreateResponse(json);

			Assert.IsNotNull(result);
			Assert.AreEqual(2, result.Id);
			Assert.AreEqual("Lý", result.Name);
			Assert.IsNull(result.Description);
		}

		[Test]
		public void ParseCreateResponse_ReturnsNull_WhenJsonIsNull()
		{
			var result = CreateSubjectsController.ParseCreateResponse(null);

			Assert.IsNull(result);
		}

		[Test]
		public void ParseCreateResponse_ReturnsNull_WhenJsonIsEmpty()
		{
			var result = CreateSubjectsController.ParseCreateResponse("");

			Assert.IsNull(result);
		}

		[Test]
		public void ParseCreateResponse_ReturnsNull_WhenJsonIsInvalid()
		{
			var result = CreateSubjectsController.ParseCreateResponse("not json");

			Assert.IsNull(result);
		}

		[Test]
		public void SetParentSignals_StoresInState()
		{
			var signals = new CreateSubjectsParentSignals
			{
				OnSubjectCreated = _ => { },
				OnCancelled = () => { },
			};

			CreateSubjectsController.SetParentSignals(signals);

			Assert.AreSame(signals, CreateSubjectsState.ParentSignals);
		}

		[Test]
		public void SetParentSignals_AcceptsNull()
		{
			CreateSubjectsController.SetParentSignals(new CreateSubjectsParentSignals());
			CreateSubjectsController.SetParentSignals(null);

			Assert.IsNull(CreateSubjectsState.ParentSignals);
		}

		[Test]
		public void OnExitScope_ResetsState()
		{
			CreateSubjectsState.IsSubmitting = true;

			CreateSubjectsController.OnExitScope();

			Assert.IsFalse(CreateSubjectsState.IsSubmitting);
		}

		[Test]
		public void HandleSubmitCreate_DoesNothing_WhenAlreadySubmitting()
		{
			CreateSubjectsState.IsSubmitting = true;
			var failedCalled = false;
			void Handler(object payload)
			{
				failedCalled = true;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(CreateSubjectsEvents.CreateStarted, Handler);
			try
			{
				CreateSubjectsController.HandleSubmitCreate(new CreateSubjectPayload { Name = "Test", Description = "Desc" });
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(CreateSubjectsEvents.CreateStarted, Handler);
			}

			Assert.IsFalse(failedCalled);
		}
	}
}
