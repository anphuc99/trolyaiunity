using System;
using System.Threading.Tasks;
using Core.Infrastructure.Network;
using Features.GamePlay.SubFeatures.CreateSubjects.Events;
using Features.GamePlay.SubFeatures.CreateSubjects.Infrastructure;
using Features.GamePlay.SubFeatures.CreateSubjects.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.CreateSubjects.Model;
using Features.GamePlay.SubFeatures.CreateSubjects.Requests;
using Newtonsoft.Json;

namespace Features.GamePlay.SubFeatures.CreateSubjects.Controller
{
	/// <summary>
	/// Controller for CreateSubjects subfeature.
	/// Handles creation of new subjects by posting to the server API.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class CreateSubjectsController
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
			CreateSubjectsState.Reset();
		}

		/// <summary>
		/// Installs the CreateSubjects subfeature.
		/// </summary>
		public static void Install()
		{
			CreateSubjectsState.Reset();
			EventBus.Publish(CreateSubjectsEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls the CreateSubjects subfeature and clears state.
		/// </summary>
		public static void Uninstall()
		{
			CreateSubjectsState.Reset();
			EventBus.Publish(CreateSubjectsEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(CreateSubjectsParentSignals signals)
		{
			CreateSubjectsState.ParentSignals = signals;
		}

		/// <summary>
		/// Handles the submit request to create a new subject.
		/// </summary>
		/// <param name="payload">CreateSubjectPayload with name and description.</param>
		[Request(CreateSubjectsRequests.SubmitCreate)]
		public static void HandleSubmitCreate(object payload)
		{
			if (payload is not CreateSubjectPayload createPayload)
			{
				PublishCreateFailed("Dữ liệu tạo môn học không hợp lệ.");
				return;
			}

			var name = createPayload.Name?.Trim();
			if (string.IsNullOrWhiteSpace(name))
			{
				PublishCreateFailed("Tên môn học không được để trống.");
				return;
			}

			if (CreateSubjectsState.IsSubmitting)
			{
				return;
			}

			_ = SubmitCreateInternalAsync(createPayload);
		}

		/// <summary>
		/// Handles the cancel request to navigate back.
		/// </summary>
		[Request(CreateSubjectsRequests.Cancel)]
		public static void HandleCancel(object payload)
		{
			CreateSubjectsState.ParentSignals?.OnCancelled?.Invoke();
		}

		/// <summary>
		/// Submits the create subject request to the server asynchronously.
		/// </summary>
		/// <param name="payload">Subject creation data.</param>
		internal static async Task SubmitCreateInternalAsync(CreateSubjectPayload payload)
		{
			CreateSubjectsState.IsSubmitting = true;
			EventBus.Publish(CreateSubjectsEvents.CreateStarted, null);

			try
			{
				var responseJson = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.Subjects, payload);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishCreateFailed("Máy chủ không trả về dữ liệu.");
					return;
				}

				var created = ParseCreateResponse(responseJson);
				if (created == null || created.Id <= 0)
				{
					PublishCreateFailed("Dữ liệu phản hồi không hợp lệ.");
					return;
				}

				EventBus.Publish(CreateSubjectsEvents.CreateSucceeded, created);
				CreateSubjectsState.ParentSignals?.OnSubjectCreated?.Invoke(created.Id);
			}
			catch (Exception exception)
			{
				PublishCreateFailed("Không thể tạo môn học: " + exception.Message);
			}
			finally
			{
				CreateSubjectsState.IsSubmitting = false;
			}
		}

		/// <summary>
		/// Parses the server response JSON into a CreateSubjectResponsePayload.
		/// </summary>
		/// <param name="json">Raw JSON response.</param>
		/// <returns>Parsed response or null on failure.</returns>
		public static CreateSubjectResponsePayload ParseCreateResponse(string json)
		{
			if (string.IsNullOrWhiteSpace(json))
			{
				return null;
			}

			try
			{
				return JsonConvert.DeserializeObject<CreateSubjectResponsePayload>(json);
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Publishes a create-failed event with an error message.
		/// </summary>
		/// <param name="message">Error message.</param>
		private static void PublishCreateFailed(string message)
		{
			CreateSubjectsState.IsSubmitting = false;
			EventBus.Publish(CreateSubjectsEvents.CreateFailed, new CreateSubjectsErrorPayload { Message = message });
		}
	}
}
