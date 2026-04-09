using Features.GamePlay.SubFeatures.PracticeVocabulary.Events;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Infrastructure;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Model;
using Features.GamePlay.SubFeatures.PracticeVocabulary.Requests;
using Core.Infrastructure.Network;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Features.GamePlay.SubFeatures.PracticeVocabulary.Controller
{
	/// <summary>
	/// Controller for PracticeVocabulary.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class PracticeVocabularyController
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
		}

		/// <summary>
		/// Installs this subfeature.
		/// </summary>
		public static void Install()
		{
			EventBus.Publish(PracticeVocabularyEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls this subfeature.
		/// </summary>
		public static void Uninstall()
		{
			EventBus.Publish(PracticeVocabularyEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(PracticeVocabularyParentSignals signals)
		{
			PracticeVocabularyState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(PracticeVocabularyRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(PracticeVocabularyEvents.Echoed, payload);
			PracticeVocabularyState.ParentSignals?.OnEchoed?.Invoke(payload);
		}

		/// <summary>
		/// Loads vocabularies that are currently due for FSRS review.
		/// </summary>
		/// <param name="payload">Unused payload.</param>
		[Request(PracticeVocabularyRequests.LoadDueReviews)]
		public static void HandleLoadDueReviews(object payload)
		{
			_ = LoadDueReviewsInternalAsync();
		}

		/// <summary>
		/// Submits an FSRS rating for one vocabulary item.
		/// </summary>
		/// <param name="payload">Review payload.</param>
		[Request(PracticeVocabularyRequests.SubmitReview)]
		public static void HandleSubmitReview(PracticeVocabularyReviewRequestPayload payload)
		{
			if (payload == null)
			{
				PublishError("Missing vocabulary review payload.");
				return;
			}

			if (string.IsNullOrWhiteSpace(payload.VocabularyId))
			{
				PublishError("Vocabulary id is required for review.");
				return;
			}

			if (payload.Rating < 1 || payload.Rating > 4)
			{
				PublishError("Rating must be between 1 and 4.");
				return;
			}

			_ = SubmitReviewInternalAsync(payload);
		}

		/// <summary>
		/// Calls server due endpoint and publishes parsed response.
		/// </summary>
		/// <returns>Awaitable task.</returns>
		private static async Task LoadDueReviewsInternalAsync()
		{
			try
			{
				var responseJson = await HttpClient.GetTaskAsync(NetworkEndpoints.VocabularyDue);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Empty due vocabulary response from server.");
					return;
				}

				var response = JsonConvert.DeserializeObject<PracticeVocabularyDueListResponsePayload>(responseJson)
					?? new PracticeVocabularyDueListResponsePayload();

				if (response.Vocabularies == null)
				{
					response.Vocabularies = new List<PracticeVocabularyItemPayload>();
				}

				if (response.Total < response.Vocabularies.Count)
				{
					response.Total = response.Vocabularies.Count;
				}

				EventBus.Publish(PracticeVocabularyEvents.DueReviewsLoaded, response);
			}
			catch (Exception exception)
			{
				PublishError("Failed to load due vocabularies: " + exception.Message);
			}
		}

		/// <summary>
		/// Calls server review endpoint and publishes completion payload.
		/// </summary>
		/// <param name="payload">Validated review payload.</param>
		/// <returns>Awaitable task.</returns>
		private static async Task SubmitReviewInternalAsync(PracticeVocabularyReviewRequestPayload payload)
		{
			try
			{
				var safeVocabularyId = payload.VocabularyId.Trim();
				var endpoint = NetworkEndpoints.VocabularyReview + "/" + Uri.EscapeDataString(safeVocabularyId) + "/review";
				var body = new { rating = payload.Rating };

				var responseJson = await HttpClient.PostJsonTaskAsync(endpoint, body);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					PublishError("Empty vocabulary review response from server.");
					return;
				}

				var review = JsonConvert.DeserializeObject<PracticeVocabularyReviewPayload>(responseJson);
				if (review == null)
				{
					PublishError("Failed to parse vocabulary review response.");
					return;
				}

				EventBus.Publish(PracticeVocabularyEvents.ReviewSubmitted, new PracticeVocabularyReviewSubmittedPayload
				{
					VocabularyId = safeVocabularyId,
					Rating = payload.Rating,
					Review = review
				});
			}
			catch (Exception exception)
			{
				PublishError("Failed to submit vocabulary review: " + exception.Message);
			}
		}

		/// <summary>
		/// Publishes controller-level request failure.
		/// </summary>
		/// <param name="message">Error message.</param>
		private static void PublishError(string message)
		{
			EventBus.Publish(PracticeVocabularyEvents.RequestFailed, new PracticeVocabularyErrorPayload
			{
				Message = message
			});
		}
	}
}
