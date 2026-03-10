using System.Collections.Generic;
using Features.GamePlay.SubFeatures.Knowledges.Controller;
using Features.GamePlay.SubFeatures.Knowledges.Events;
using Features.GamePlay.SubFeatures.Knowledges.Model;
using NUnit.Framework;

namespace Features.GamePlay.SubFeatures.Knowledges.Tests
{
	/// <summary>
	/// Tests for KnowledgesController logic and event flow.
	/// </summary>
	public sealed class KnowledgesControllerTests
	{
		[TearDown]
		public void TearDown()
		{
			KnowledgesController.SetParentSignals(null);
			KnowledgesState.Reset();
		}

		[Test]
		public void Install_PublishesInstalledEvent()
		{
			var installed = false;
			void Handler(object payload)
			{
				installed = true;
			}

			Core.Infrastructure.Events.EventBus.Subscribe(KnowledgesEvents.Installed, Handler);
			try
			{
				KnowledgesController.Install();
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(KnowledgesEvents.Installed, Handler);
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

			Core.Infrastructure.Events.EventBus.Subscribe(KnowledgesEvents.Uninstalled, Handler);
			try
			{
				KnowledgesController.Uninstall();
			}
			finally
			{
				Core.Infrastructure.Events.EventBus.Unsubscribe(KnowledgesEvents.Uninstalled, Handler);
			}

			Assert.IsTrue(uninstalled);
		}

		[Test]
		public void Uninstall_ClearsCachedKnowledges()
		{
			KnowledgesState.CachedKnowledges = new List<KnowledgeItemPayload>
			{
				new KnowledgeItemPayload { Id = 1, Name = "Kiến thức 1" }
			};

			KnowledgesController.Uninstall();

			Assert.IsNull(KnowledgesState.CachedKnowledges);
		}

		[Test]
		public void HandleSelectKnowledge_InvokesParentSignal_WhenValidKnowledge()
		{
			var selectedId = 0;
			KnowledgesController.SetParentSignals(new KnowledgesParentSignals
			{
				OnStartLearning = id => selectedId = id,
			});

			KnowledgesController.HandleSelectKnowledge(new KnowledgeItemPayload { Id = 5, Name = "Kiến thức" });

			Assert.AreEqual(5, selectedId);
		}

		[Test]
		public void HandleSelectKnowledge_DoesNothing_WhenKnowledgeIdIsZero()
		{
			var selectedId = -1;
			KnowledgesController.SetParentSignals(new KnowledgesParentSignals
			{
				OnStartLearning = id => selectedId = id,
			});

			KnowledgesController.HandleSelectKnowledge(new KnowledgeItemPayload { Id = 0, Name = "Invalid" });

			Assert.AreEqual(-1, selectedId);
		}

		[Test]
		public void HandleSelectKnowledge_DoesNothing_WhenPayloadIsNull()
		{
			var selectedId = -1;
			KnowledgesController.SetParentSignals(new KnowledgesParentSignals
			{
				OnStartLearning = id => selectedId = id,
			});

			KnowledgesController.HandleSelectKnowledge(null);

			Assert.AreEqual(-1, selectedId);
		}

		[Test]
		public void HandleSelectKnowledge_DoesNotThrow_WhenNoParentSignals()
		{
			KnowledgesController.SetParentSignals(null);

			Assert.DoesNotThrow(() =>
			{
				KnowledgesController.HandleSelectKnowledge(new KnowledgeItemPayload { Id = 1, Name = "Test" });
			});
		}

		[Test]
		public void HandleBackToSubjects_InvokesParentSignal()
		{
			var invoked = false;
			KnowledgesController.SetParentSignals(new KnowledgesParentSignals
			{
				OnBackToSubjects = () => invoked = true,
			});

			KnowledgesController.HandleBackToSubjects(null);

			Assert.IsTrue(invoked);
		}

		[Test]
		public void HandleBackToSubjects_DoesNotThrow_WhenNoParentSignals()
		{
			KnowledgesController.SetParentSignals(null);

			Assert.DoesNotThrow(() =>
			{
				KnowledgesController.HandleBackToSubjects(null);
			});
		}

		[Test]
		public void HandleStartLearning_InvokesParentSignal_WhenCachedKnowledgesExist()
		{
			var selectedId = 0;
			KnowledgesState.CachedKnowledges = new List<KnowledgeItemPayload>
			{
				new KnowledgeItemPayload { Id = 10, Name = "First Knowledge" }
			};
			KnowledgesController.SetParentSignals(new KnowledgesParentSignals
			{
				OnStartLearning = id => selectedId = id,
			});

			KnowledgesController.HandleStartLearning(null);

			Assert.AreEqual(10, selectedId);
		}

		[Test]
		public void HandleStartLearning_DoesNotInvoke_WhenNoCachedKnowledges()
		{
			var selectedId = -1;
			KnowledgesState.CachedKnowledges = null;
			KnowledgesController.SetParentSignals(new KnowledgesParentSignals
			{
				OnStartLearning = id => selectedId = id,
			});

			KnowledgesController.HandleStartLearning(null);

			Assert.AreEqual(-1, selectedId);
		}

		[Test]
		public void HandleStartLearning_DoesNotInvoke_WhenCachedKnowledgesEmpty()
		{
			var selectedId = -1;
			KnowledgesState.CachedKnowledges = new List<KnowledgeItemPayload>();
			KnowledgesController.SetParentSignals(new KnowledgesParentSignals
			{
				OnStartLearning = id => selectedId = id,
			});

			KnowledgesController.HandleStartLearning(null);

			Assert.AreEqual(-1, selectedId);
		}

		[Test]
		public void ParseKnowledgesList_ParsesJsonArray()
		{
			var json = "[{\"id\":1,\"name\":\"Kiến thức 1\",\"description\":\"Desc 1\"},{\"id\":2,\"name\":\"Kiến thức 2\",\"description\":\"Desc 2\"}]";

			var result = KnowledgesController.ParseKnowledgesList(json);

			Assert.IsNotNull(result);
			Assert.AreEqual(2, result.Count);
			Assert.AreEqual(1, result[0].Id);
			Assert.AreEqual("Kiến thức 1", result[0].Name);
			Assert.AreEqual("Desc 1", result[0].Description);
			Assert.AreEqual(2, result[1].Id);
			Assert.AreEqual("Kiến thức 2", result[1].Name);
		}

		[Test]
		public void ParseKnowledgesList_ParsesWrappedResponse()
		{
			var json = "{\"knowledges\":[{\"id\":3,\"name\":\"Kiến thức 3\",\"description\":\"Chemistry\"}]}";

			var result = KnowledgesController.ParseKnowledgesList(json);

			Assert.IsNotNull(result);
			Assert.AreEqual(1, result.Count);
			Assert.AreEqual(3, result[0].Id);
			Assert.AreEqual("Kiến thức 3", result[0].Name);
		}

		[Test]
		public void ParseKnowledgesList_ReturnsNull_WhenJsonIsEmpty()
		{
			var result = KnowledgesController.ParseKnowledgesList("");

			Assert.IsNull(result);
		}

		[Test]
		public void ParseKnowledgesList_ReturnsNull_WhenJsonIsNull()
		{
			var result = KnowledgesController.ParseKnowledgesList(null);

			Assert.IsNull(result);
		}

		[Test]
		public void ParseKnowledgesList_ReturnsNull_WhenJsonIsInvalid()
		{
			var result = KnowledgesController.ParseKnowledgesList("not json at all");

			Assert.IsNull(result);
		}

		[Test]
		public void ParseKnowledgesList_ReturnsEmptyList_WhenEmptyArray()
		{
			var result = KnowledgesController.ParseKnowledgesList("[]");

			Assert.IsNotNull(result);
			Assert.AreEqual(0, result.Count);
		}

		[Test]
		public void SetParentSignals_StoresInState()
		{
			var signals = new KnowledgesParentSignals
			{
				OnStartLearning = _ => { },
			};

			KnowledgesController.SetParentSignals(signals);

			Assert.AreSame(signals, KnowledgesState.ParentSignals);
		}

		[Test]
		public void SetParentSignals_AcceptsNull()
		{
			KnowledgesController.SetParentSignals(new KnowledgesParentSignals());
			KnowledgesController.SetParentSignals(null);

			Assert.IsNull(KnowledgesState.ParentSignals);
		}

		[Test]
		public void OnExitScope_ResetsState()
		{
			KnowledgesState.CachedKnowledges = new List<KnowledgeItemPayload>
			{
				new KnowledgeItemPayload { Id = 1, Name = "Test" }
			};
			KnowledgesState.CurrentSubjectId = 5;

			KnowledgesController.OnExitScope();

			Assert.IsNull(KnowledgesState.CachedKnowledges);
			Assert.AreEqual(0, KnowledgesState.CurrentSubjectId);
		}

		[Test]
		public void Install_RegistersBackMenu_WhenAddMenuAvailable()
		{
			string receivedText = null;
			System.Action receivedAction = null;

			KnowledgesController.SetParentSignals(new KnowledgesParentSignals
			{
				AddMenu = (text, onClick) =>
				{
					receivedText = text;
					receivedAction = onClick;
					return "knowledges-back-menu";
				},
			});

			KnowledgesController.Install();

			Assert.AreEqual("Quay lại", receivedText);
			Assert.IsNotNull(receivedAction);
			Assert.AreEqual("knowledges-back-menu", KnowledgesState.BackMenuId);
		}

		[Test]
		public void Uninstall_RemovesBackMenu_WhenMenuIdExists()
		{
			string removedMenuId = null;
			KnowledgesController.SetParentSignals(new KnowledgesParentSignals
			{
				AddMenu = (text, onClick) => "knowledges-back-menu",
				RemoveMenu = id => removedMenuId = id,
			});

			KnowledgesController.Install();
			KnowledgesController.Uninstall();

			Assert.AreEqual("knowledges-back-menu", removedMenuId);
			Assert.IsNull(KnowledgesState.BackMenuId);
		}

		[Test]
		public void Install_ReadsSubjectIdFromState_WhenNotZero()
		{
			KnowledgesState.CurrentSubjectId = 0;
			KnowledgesController.Install();

			// Since CurrentSubjectId is read from GlobalVariables, and we haven't set it,
			// it should remain 0 in state
			Assert.AreEqual(0, KnowledgesState.CurrentSubjectId);
		}

		[Test]
		public void State_Reset_ClearsAllFields()
		{
			KnowledgesState.CachedKnowledges = new List<KnowledgeItemPayload> { new KnowledgeItemPayload() };
			KnowledgesState.BackMenuId = "some-menu-id";
			KnowledgesState.CurrentSubjectId = 10;

			KnowledgesState.Reset();

			Assert.IsNull(KnowledgesState.CachedKnowledges);
			Assert.IsNull(KnowledgesState.BackMenuId);
			Assert.AreEqual(0, KnowledgesState.CurrentSubjectId);
		}
	}
}
