using System.Collections.Generic;
using Features.GamePlay.SubFeatures.Subjects.Controller;
using Features.GamePlay.SubFeatures.Subjects.Events;
using Features.GamePlay.SubFeatures.Subjects.Model;
using NUnit.Framework;

namespace Features.GamePlay.SubFeatures.Subjects.Tests
{
	/// <summary>
	/// Tests for SubjectsController logic and event flow.
	/// </summary>
	public sealed class SubjectsControllerTests
	{
		[TearDown]
		public void TearDown()
		{
			SubjectsController.SetParentSignals(null);
			SubjectsState.Reset();
		}

		[Test]
		public void Install_PublishesInstalledEvent()
		{
			var installed = false;
			void Handler(object payload)
			{
				installed = true;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(SubjectsEvents.Installed, Handler);
			try
			{
				SubjectsController.Install();
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(SubjectsEvents.Installed, Handler);
			}

			Assert.IsTrue(installed);
		}

		[Test]
		public void Uninstall_PublishesUninstalledEvent()
		{
			var uninstalled = false;
			void Handler(object payload)
			{
				uninstalled = true;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(SubjectsEvents.Uninstalled, Handler);
			try
			{
				SubjectsController.Uninstall();
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(SubjectsEvents.Uninstalled, Handler);
			}

			Assert.IsTrue(uninstalled);
		}

		[Test]
		public void Uninstall_ClearsCachedSubjects()
		{
			SubjectsState.CachedSubjects = new List<SubjectItemPayload>
			{
				new SubjectItemPayload { Id = 1, Name = "Toán" }
			};

			SubjectsController.Uninstall();

			Assert.IsNotNull(SubjectsState.CachedSubjects);
			Assert.AreEqual(0, SubjectsState.CachedSubjects.Count);
		}

		[Test]
		public void HandleSelectSubject_InvokesParentSignal_WhenValidSubject()
		{
			var selectedId = 0;
			SubjectsController.SetParentSignals(new SubjectsParentSignals
			{
				OnSubjectSelected = id => selectedId = id,
			});

			SubjectsController.HandleSelectSubject(new SubjectItemPayload { Id = 5, Name = "Toán" });

			Assert.AreEqual(5, selectedId);
		}

		[Test]
		public void HandleSelectSubject_DoesNothing_WhenSubjectIdIsZero()
		{
			var selectedId = -1;
			SubjectsController.SetParentSignals(new SubjectsParentSignals
			{
				OnSubjectSelected = id => selectedId = id,
			});

			SubjectsController.HandleSelectSubject(new SubjectItemPayload { Id = 0, Name = "Invalid" });

			Assert.AreEqual(-1, selectedId);
		}

		[Test]
		public void HandleSelectSubject_DoesNothing_WhenPayloadIsNull()
		{
			var selectedId = -1;
			SubjectsController.SetParentSignals(new SubjectsParentSignals
			{
				OnSubjectSelected = id => selectedId = id,
			});

			SubjectsController.HandleSelectSubject(null);

			Assert.AreEqual(-1, selectedId);
		}

		[Test]
		public void HandleSelectSubject_DoesNotThrow_WhenNoParentSignals()
		{
			SubjectsController.SetParentSignals(null);

			Assert.DoesNotThrow(() =>
			{
				SubjectsController.HandleSelectSubject(new SubjectItemPayload { Id = 1, Name = "Test" });
			});
		}

		[Test]
		public void ParseSubjectsList_ParsesJsonArray()
		{
			var json = "[{\"id\":1,\"name\":\"Toán\",\"description\":\"Math\"},{\"id\":2,\"name\":\"Lý\",\"description\":\"Physics\"}]";

			var result = SubjectsController.ParseSubjectsList(json);

			Assert.IsNotNull(result);
			Assert.AreEqual(2, result.Count);
			Assert.AreEqual(1, result[0].Id);
			Assert.AreEqual("Toán", result[0].Name);
			Assert.AreEqual("Math", result[0].Description);
			Assert.AreEqual(2, result[1].Id);
			Assert.AreEqual("Lý", result[1].Name);
		}

		[Test]
		public void ParseSubjectsList_ParsesWrappedResponse()
		{
			var json = "{\"subjects\":[{\"id\":3,\"name\":\"Hóa\",\"description\":\"Chemistry\"}]}";

			var result = SubjectsController.ParseSubjectsList(json);

			Assert.IsNotNull(result);
			Assert.AreEqual(1, result.Count);
			Assert.AreEqual(3, result[0].Id);
			Assert.AreEqual("Hóa", result[0].Name);
		}

		[Test]
		public void ParseSubjectsList_ReturnsNull_WhenJsonIsEmpty()
		{
			var result = SubjectsController.ParseSubjectsList("");

			Assert.IsNull(result);
		}

		[Test]
		public void ParseSubjectsList_ReturnsNull_WhenJsonIsNull()
		{
			var result = SubjectsController.ParseSubjectsList(null);

			Assert.IsNull(result);
		}

		[Test]
		public void ParseSubjectsList_ReturnsNull_WhenJsonIsInvalid()
		{
			var result = SubjectsController.ParseSubjectsList("not json at all");

			Assert.IsNull(result);
		}

		[Test]
		public void ParseSubjectsList_ReturnsEmptyList_WhenEmptyArray()
		{
			var result = SubjectsController.ParseSubjectsList("[]");

			Assert.IsNotNull(result);
			Assert.AreEqual(0, result.Count);
		}

		[Test]
		public void SetParentSignals_StoresInState()
		{
			var signals = new SubjectsParentSignals
			{
				OnSubjectSelected = _ => { },
			};

			SubjectsController.SetParentSignals(signals);

			Assert.AreSame(signals, SubjectsState.ParentSignals);
		}

		[Test]
		public void SetParentSignals_AcceptsNull()
		{
			SubjectsController.SetParentSignals(new SubjectsParentSignals());
			SubjectsController.SetParentSignals(null);

			Assert.IsNull(SubjectsState.ParentSignals);
		}

		[Test]
		public void OnExitScope_ResetsState()
		{
			SubjectsState.CachedSubjects = new List<SubjectItemPayload>
			{
				new SubjectItemPayload { Id = 1, Name = "Test" }
			};

			SubjectsController.OnExitScope();

			Assert.IsNotNull(SubjectsState.CachedSubjects);
			Assert.AreEqual(0, SubjectsState.CachedSubjects.Count);
		}
	}
}
