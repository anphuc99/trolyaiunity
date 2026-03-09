using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Infrastructure.Network;
using Features.GamePlay.SubFeatures.Subjects.Events;
using Features.GamePlay.SubFeatures.Subjects.Infrastructure;
using Features.GamePlay.SubFeatures.Subjects.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Subjects.Model;
using Features.GamePlay.SubFeatures.Subjects.Requests;
using Newtonsoft.Json;

namespace Features.GamePlay.SubFeatures.Subjects.Controller
{
	/// <summary>
	/// Controller for Subjects subfeature.
	/// Loads subjects from the server and allows the user to select one to open Knowledges.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class SubjectsController
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
			SubjectsState.Reset();
		}

		/// <summary>
		/// Installs the Subjects subfeature and loads subjects from the server.
		/// </summary>
		public static void Install()
		{
			EventBus.Publish(SubjectsEvents.Installed, null);
			HandleLoadSubjects();
		}

		/// <summary>
		/// Uninstalls the Subjects subfeature and clears cached state.
		/// </summary>
		public static void Uninstall()
		{
			SubjectsState.Reset();
			EventBus.Publish(SubjectsEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(SubjectsParentSignals signals)
		{
			SubjectsState.ParentSignals = signals;
		}

		/// <summary>
		/// Loads subjects list from the server.
		/// </summary>
		[Request(SubjectsRequests.LoadSubjects)]
		public static void HandleLoadSubjects()
		{
			_ = LoadSubjectsInternalAsync();
		}

		/// <summary>
		/// Handles subject selection and signals the parent to open Knowledges.
		/// </summary>
		/// <param name="payload">SubjectItemPayload of the selected subject.</param>
		[Request(SubjectsRequests.SelectSubject)]
		public static void HandleSelectSubject(object payload)
		{
			if (payload is not SubjectItemPayload subject || subject.Id <= 0)
			{
				return;
			}

			SubjectsState.ParentSignals?.OnSubjectSelected?.Invoke(subject.Id);
		}

		/// <summary>
		/// Loads subjects from the server asynchronously and publishes results.
		/// </summary>
		internal static async Task LoadSubjectsInternalAsync()
		{
			EventBus.Publish(SubjectsEvents.SubjectsLoadStarted, null);

			try
			{
				var responseJson = await HttpClient.GetTaskAsync(NetworkEndpoints.Subjects);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishLoadFailed("Máy chủ không trả về dữ liệu môn học.");
					return;
				}

				var subjects = ParseSubjectsList(responseJson);
				if (subjects == null)
				{
					PublishLoadFailed("Dữ liệu môn học không hợp lệ.");
					return;
				}

				SubjectsState.CachedSubjects = subjects;
				EventBus.Publish(SubjectsEvents.SubjectsLoaded, subjects);
			}
			catch (Exception exception)
			{
				PublishLoadFailed("Không thể tải danh sách môn học: " + exception.Message);
			}
		}

		/// <summary>
		/// Parses the server response into a list of subject items.
		/// Supports both a plain array and a { subjects: [...] } wrapper.
		/// </summary>
		/// <param name="json">Raw JSON response from the server.</param>
		/// <returns>Parsed list or null on failure.</returns>
		public static List<SubjectItemPayload> ParseSubjectsList(string json)
		{
			if (string.IsNullOrWhiteSpace(json))
			{
				return null;
			}

			try
			{
				var trimmed = json.TrimStart();
				if (trimmed.StartsWith("[", StringComparison.Ordinal))
				{
					return JsonConvert.DeserializeObject<List<SubjectItemPayload>>(json);
				}

				var wrapped = JsonConvert.DeserializeObject<SubjectsResponsePayload>(json);
				return wrapped?.Subjects;
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Publishes a load-failed event with an error message.
		/// </summary>
		/// <param name="message">Error message.</param>
		private static void PublishLoadFailed(string message)
		{
			EventBus.Publish(SubjectsEvents.SubjectsLoadFailed, new SubjectsErrorPayload { Message = message });
		}
	}
}
