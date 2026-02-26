using Core.Infrastructure.Network;
using Features.GamePlay.SubFeatures.Practice.Events;
using Features.GamePlay.SubFeatures.Practice.Infrastructure;
using Features.GamePlay.SubFeatures.Practice.Infrastructure.Attributes;
using Features.GamePlay.SubFeatures.Practice.Model;
using Features.GamePlay.SubFeatures.Practice.Requests;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Features.GamePlay.SubFeatures.Practice.Controller
{
	/// <summary>
	/// Controller for Practice.
	/// </summary>
	[Core.Infrastructure.Attributes.ControllerScope(Core.Infrastructure.Attributes.ControllerScopeKey.GamePlayGameplay)]
	public static class PracticeController
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
		/// Installs the subcontroller.
		/// </summary>
		public static void Install()
		{
			EventBus.Publish(PracticeEvents.Installed, null);
		}

		/// <summary>
		/// Uninstalls the subcontroller and clears bound parent signals.
		/// </summary>
		public static void Uninstall()
		{
			EventBus.Publish(PracticeEvents.Uninstalled, null);
		}

		/// <summary>
		/// Binds parent-provided signals for this subfeature.
		/// </summary>
		/// <param name="signals">Signals implemented by the parent feature.</param>
		public static void SetParentSignals(PracticeParentSignals signals)
		{
			PracticeState.ParentSignals = signals;
		}

		/// <summary>
		/// Sample request handler that echoes payload to a view event and parent signal.
		/// </summary>
		/// <param name="payload">Optional payload.</param>
		[Request(PracticeRequests.Echo)]
		public static void HandleEcho(object payload)
		{
			EventBus.Publish(PracticeEvents.Echoed, payload);
			PracticeState.ParentSignals?.OnEchoed?.Invoke(payload);
		}

		/// <summary>
		/// Loads the requested practice tab.
		/// </summary>
		/// <param name="payload">Tab request payload.</param>
		[Request(PracticeRequests.LoadTab)]
		public static void HandleLoadTab(PracticeTabRequestPayload payload)
		{
			if (payload == null)
			{
				EventBus.Publish(PracticeEvents.RequestFailed, new PracticeErrorPayload
				{
					Message = "Missing practice tab request payload."
				});
				return;
			}

			_ = LoadTabInternalAsync(payload.Tab);
		}

		/// <summary>
		/// Loads translation context for the given message id.
		/// </summary>
		/// <param name="payload">Context request payload.</param>
		[Request(PracticeRequests.LoadContext)]
		public static void HandleLoadContext(PracticeContextRequestPayload payload)
		{
			if (payload == null || string.IsNullOrWhiteSpace(payload.MessageId))
			{
				EventBus.Publish(PracticeEvents.RequestFailed, new PracticeErrorPayload
				{
					Message = "Message id is required to load context."
				});
				return;
			}

			_ = LoadContextInternalAsync(payload.MessageId.Trim());
		}

		/// <summary>
		/// Submits a translation review rating.
		/// </summary>
		/// <param name="payload">Review request payload.</param>
		[Request(PracticeRequests.SubmitReview)]
		public static void HandleSubmitReview(PracticeReviewRequestPayload payload)
		{
			if (payload == null)
			{
				EventBus.Publish(PracticeEvents.RequestFailed, new PracticeErrorPayload
				{
					Message = "Missing review payload."
				});
				return;
			}

			if (payload.Rating < 1 || payload.Rating > 4)
			{
				EventBus.Publish(PracticeEvents.RequestFailed, new PracticeErrorPayload
				{
					Message = "Rating must be between 1 and 4."
				});
				return;
			}

			if (payload.CardId == null && string.IsNullOrWhiteSpace(payload.MessageId))
			{
				EventBus.Publish(PracticeEvents.RequestFailed, new PracticeErrorPayload
				{
					Message = "Card id or message id is required for review."
				});
				return;
			}

			_ = SubmitReviewInternalAsync(payload);
		}

		private static async Task LoadTabInternalAsync(PracticeTabType tab)
		{
			try
			{
				List<PracticePromptItemPayload> items = tab switch
				{
					PracticeTabType.Learn => await LoadLearnItemsAsync(),
					PracticeTabType.Review => await LoadDueItemsAsync(),
					PracticeTabType.Starred => await LoadStarredItemsAsync(),
					PracticeTabType.Difficult => await LoadDifficultItemsAsync(),
					_ => new List<PracticePromptItemPayload>()
				};

				EventBus.Publish(PracticeEvents.TabLoaded, new PracticeTabResponsePayload
				{
					Tab = tab,
					Items = items
				});
			}
			catch (Exception exception)
			{
				EventBus.Publish(PracticeEvents.RequestFailed, new PracticeErrorPayload
				{
					Message = "Failed to load practice tab: " + exception.Message
				});
			}
		}

		private static async Task<List<PracticePromptItemPayload>> LoadDueItemsAsync()
		{
			var responseJson = await HttpClient.GetTaskAsync(NetworkEndpoints.TranslationDue);
			if (string.IsNullOrWhiteSpace(responseJson))
			{
				return new List<PracticePromptItemPayload>();
			}

			var response = JsonConvert.DeserializeObject<PracticeTranslationDueResponsePayload>(responseJson);
			return MapCardsToItems(response?.Cards);
		}

		private static async Task<List<PracticePromptItemPayload>> LoadStarredItemsAsync()
		{
			var cards = await LoadAllCardsAsync();
			return cards.Where(card => card?.Review != null && card.Review.IsStarred).ToList();
		}

		private static async Task<List<PracticePromptItemPayload>> LoadDifficultItemsAsync()
		{
			var cards = await LoadAllCardsAsync();
			return cards.Where(card => IsDifficultToday(card?.Review)).ToList();
		}

		private static async Task<List<PracticePromptItemPayload>> LoadLearnItemsAsync()
		{
			var responseJson = await HttpClient.GetTaskAsync(NetworkEndpoints.TranslationLearn);
			if (string.IsNullOrWhiteSpace(responseJson))
			{
				return new List<PracticePromptItemPayload>();
			}

			var response = JsonConvert.DeserializeObject<PracticeTranslationLearnResponsePayload>(responseJson);
			if (response?.Candidates == null || response.Candidates.Count == 0)
			{
				return new List<PracticePromptItemPayload>();
			}

			return response.Candidates
				.Where(candidate => candidate != null)
				.Select(candidate => new PracticePromptItemPayload
				{
					CardId = null,
					MessageId = candidate.MessageId,
					Content = candidate.Content,
					Translation = candidate.Translation,
					CharacterName = candidate.CharacterName,
					Review = null,
					IsLearnCandidate = true
				}).ToList();
		}

		private static async Task<List<PracticePromptItemPayload>> LoadAllCardsAsync()
		{
			var responseJson = await HttpClient.GetTaskAsync(NetworkEndpoints.Translation);
			if (string.IsNullOrWhiteSpace(responseJson))
			{
				return new List<PracticePromptItemPayload>();
			}

			var response = JsonConvert.DeserializeObject<PracticeTranslationListResponsePayload>(responseJson);
			return MapCardsToItems(response?.Cards);
		}

		private static List<PracticePromptItemPayload> MapCardsToItems(List<PracticeTranslationCardPayload> cards)
		{
			if (cards == null || cards.Count == 0)
			{
				return new List<PracticePromptItemPayload>();
			}

			return cards.Where(card => card != null)
				.Select(card => new PracticePromptItemPayload
				{
					CardId = card.Id,
					MessageId = card.MessageId,
					Content = card.Content,
					Translation = card.Translation,
					CharacterName = card.CharacterName,
					Review = card.Review,
					IsLearnCandidate = false
				}).ToList();
		}

		private static bool IsDifficultToday(PracticeTranslationReviewPayload review)
		{
			if (review?.ReviewHistory == null || review.ReviewHistory.Count == 0)
			{
				return false;
			}

			var today = DateTime.Now.Date;
			for (var i = 0; i < review.ReviewHistory.Count; i += 1)
			{
				var entry = review.ReviewHistory[i];
				if (entry == null || entry.Rating > 2)
				{
					continue;
				}

				if (!DateTime.TryParse(entry.Date, out var entryDate))
				{
					continue;
				}

				if (entryDate.Date == today)
				{
					return true;
				}
			}

			return false;
		}

		private static async Task LoadContextInternalAsync(string messageId)
		{
			try
			{
				var endpoint = NetworkEndpoints.TranslationContext + "/" + Uri.EscapeDataString(messageId);
				var responseJson = await HttpClient.GetTaskAsync(endpoint);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					EventBus.Publish(PracticeEvents.RequestFailed, new PracticeErrorPayload
					{
						Message = "Failed to load context."
					});
					return;
				}

				var response = JsonConvert.DeserializeObject<PracticeTranslationContextResponsePayload>(responseJson)
					?? new PracticeTranslationContextResponsePayload();
				EventBus.Publish(PracticeEvents.ContextLoaded, response);
			}
			catch (Exception exception)
			{
				EventBus.Publish(PracticeEvents.RequestFailed, new PracticeErrorPayload
				{
					Message = "Failed to load context: " + exception.Message
				});
			}
		}

		private static async Task SubmitReviewInternalAsync(PracticeReviewRequestPayload payload)
		{
			try
			{
				var request = new PracticeTranslationReviewRequestPayload
				{
					Rating = payload.Rating,
					CardId = payload.CardId,
					MessageId = string.IsNullOrWhiteSpace(payload.MessageId) ? null : payload.MessageId.Trim()
				};

				var responseJson = await HttpClient.PostJsonTaskAsync(NetworkEndpoints.TranslationReview, request);
				if (string.IsNullOrWhiteSpace(responseJson))
				{
					EventBus.Publish(PracticeEvents.RequestFailed, new PracticeErrorPayload
					{
						Message = "Failed to submit review."
					});
					return;
				}

				EventBus.Publish(PracticeEvents.ReviewSubmitted, payload);
			}
			catch (Exception exception)
			{
				EventBus.Publish(PracticeEvents.RequestFailed, new PracticeErrorPayload
				{
					Message = "Failed to submit review: " + exception.Message
				});
			}
		}
	}
}
