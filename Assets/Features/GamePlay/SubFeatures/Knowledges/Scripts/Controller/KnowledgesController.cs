using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Infrastructure.Network;
using Core.Infrastructure.State;
using Features.GamePlay.SubFeatures.Knowledges.Events;
using Features.GamePlay.SubFeatures.Knowledges.Infrastructure;
using Features.GamePlay.SubFeatures.Knowledges.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Knowledges.Model;
using Features.GamePlay.SubFeatures.Knowledges.Requests;
using Newtonsoft.Json;

namespace Features.GamePlay.SubFeatures.Knowledges.Controller
{
	/// <summary>
	/// Controller for Knowledges subfeature.
	/// Loads knowledges from the server based on the selected subject ID from GlobalVariables.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class KnowledgesController
	{
		private const string SubjectIdGlobalKey = "global.subjects.selected.id";

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
			UnregisterBackMenu();
			KnowledgesState.Reset();
		}

		/// <summary>
		/// Installs the Knowledges subfeature.
		/// Reads the subject ID from GlobalVariables and loads knowledges.
		/// </summary>
		public static void Install()
		{
			KnowledgesState.Reset();
			KnowledgesState.CurrentSubjectId = GlobalVariables.GetOrDefault<int>(SubjectIdGlobalKey, 0);
			RegisterBackMenu();
			EventBus.Publish(KnowledgesEvents.Installed, null);
			HandleLoadKnowledges();
		}

		/// <summary>
		/// Uninstalls the Knowledges subfeature and clears cached state.
		/// </summary>
		public static void Uninstall()
		{
			UnregisterBackMenu();
			KnowledgesState.Reset();
			EventBus.Publish(KnowledgesEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(KnowledgesParentSignals signals)
		{
			KnowledgesState.ParentSignals = signals;
		}

		/// <summary>
		/// Loads knowledges list from the server for the current subject.
		/// </summary>
		[Request(KnowledgesRequests.LoadKnowledges)]
		public static void HandleLoadKnowledges()
		{
			_ = LoadKnowledgesInternalAsync();
		}

		/// <summary>
		/// Handles navigation back to subjects.
		/// </summary>
		/// <param name="payload">Unused.</param>
		[Request(KnowledgesRequests.BackToSubjects)]
		public static void HandleBackToSubjects(object payload)
		{
			KnowledgesState.ParentSignals?.OnBackToSubjects?.Invoke();
		}

		/// <summary>
		/// Handles starting learning mode with the current subject.
		/// </summary>
		/// <param name="payload">Unused.</param>
		[Request(KnowledgesRequests.StartLearning)]
		public static void HandleStartLearning(object payload)
		{
			KnowledgesState.ParentSignals?.OnStartLearning?.Invoke();
		}

		/// <summary>
		/// Registers the back navigation menu item.
		/// </summary>
		private static void RegisterBackMenu()
		{
			if (!string.IsNullOrWhiteSpace(KnowledgesState.BackMenuId))
			{
				return;
			}

			var addMenu = KnowledgesState.ParentSignals?.AddMenu;
			if (addMenu == null)
			{
				return;
			}

			KnowledgesState.BackMenuId = addMenu("Quay lại", OnBackMenuClicked);
		}

		/// <summary>
		/// Unregisters the back navigation menu item.
		/// </summary>
		private static void UnregisterBackMenu()
		{
			if (string.IsNullOrWhiteSpace(KnowledgesState.BackMenuId))
			{
				return;
			}

			KnowledgesState.ParentSignals?.RemoveMenu?.Invoke(KnowledgesState.BackMenuId);
			KnowledgesState.BackMenuId = null;
		}

		/// <summary>
		/// Handles back menu click.
		/// </summary>
		private static void OnBackMenuClicked()
		{
			KnowledgesState.ParentSignals?.OnBackToSubjects?.Invoke();
		}

		/// <summary>
		/// Internal async method to load knowledges from the server.
		/// </summary>
		internal static async Task LoadKnowledgesInternalAsync()
		{
			var subjectId = KnowledgesState.CurrentSubjectId;
			if (subjectId <= 0)
			{
				PublishLoadFailed("Không có môn học được chọn.");
				return;
			}

			try
			{
				var url = NetworkEndpoints.Knowledges + "?subjectId=" + subjectId;
				var responseJson = await HttpClient.GetTaskAsync(url);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishLoadFailed("Máy chủ không trả về dữ liệu.");
					return;
				}

				var knowledges = ParseKnowledgesList(responseJson);
				if (knowledges == null)
				{
					PublishLoadFailed("Dữ liệu phản hồi không hợp lệ.");
					return;
				}

				KnowledgesState.CachedKnowledges = knowledges;
				EventBus.Publish(KnowledgesEvents.KnowledgesLoaded, knowledges);
			}
			catch (Exception exception)
			{
				PublishLoadFailed("Không thể tải danh sách kiến thức: " + exception.Message);
			}
		}

		/// <summary>
		/// Parses the server response JSON into a list of KnowledgeItemPayload.
		/// Supports both array format and wrapped {knowledges:[...]} format.
		/// </summary>
		/// <param name="json">Raw JSON response.</param>
		/// <returns>Parsed list or null on failure.</returns>
		public static List<KnowledgeItemPayload> ParseKnowledgesList(string json)
		{
			if (string.IsNullOrWhiteSpace(json))
			{
				return null;
			}

			try
			{
				// Try wrapped format first
				var wrapped = JsonConvert.DeserializeObject<KnowledgesResponsePayload>(json);
				if (wrapped?.Knowledges != null)
				{
					return wrapped.Knowledges;
				}
			}
			catch
			{
				// Ignore and try array format
			}

			try
			{
				// Try direct array format
				var list = JsonConvert.DeserializeObject<List<KnowledgeItemPayload>>(json);
				return list;
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Publishes a load failed event with the given message.
		/// </summary>
		/// <param name="message">Error message.</param>
		private static void PublishLoadFailed(string message)
		{
			EventBus.Publish(KnowledgesEvents.KnowledgesLoadFailed, new KnowledgesErrorPayload
			{
				Message = message,
			});
		}
	}
}
