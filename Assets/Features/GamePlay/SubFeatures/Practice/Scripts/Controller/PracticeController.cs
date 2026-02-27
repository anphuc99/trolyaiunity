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
				List<PracticePromptItemPayload> items = new List<PracticePromptItemPayload>();
				var learnedCount = 0;
				var candidateCount = 0;

				switch (tab)
				{
					case PracticeTabType.Learn:
						var learnResult = await LoadLearnCandidatesAsync();
						items = learnResult.Items;
						candidateCount = learnResult.CandidateCount;
						learnedCount = await LoadLearnedCountAsync();
						break;
					case PracticeTabType.Review:
						items = await LoadDueItemsAsync();
						learnedCount = await LoadLearnedCountAsync();
						candidateCount = await LoadLearnCandidateCountAsync();
						break;
					case PracticeTabType.Starred:
						var starredCards = await LoadAllCardsAsync();
						learnedCount = starredCards.Count;
						items = starredCards.Where(card => card?.Review != null && card.Review.IsStarred).ToList();
						candidateCount = await LoadLearnCandidateCountAsync();
						break;
					case PracticeTabType.Difficult:
						var difficultCards = await LoadAllCardsAsync();
						learnedCount = difficultCards.Count;
						items = difficultCards.Where(card => IsDifficultToday(card?.Review)).ToList();
						candidateCount = await LoadLearnCandidateCountAsync();
						break;
					default:
						learnedCount = await LoadLearnedCountAsync();
						candidateCount = await LoadLearnCandidateCountAsync();
						break;
				}

				var summary = BuildTabSummary(tab, learnedCount, candidateCount);

				EventBus.Publish(PracticeEvents.TabLoaded, new PracticeTabResponsePayload
				{
					Tab = tab,
					Summary = summary,
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

		/// <summary>
		/// Loads learn candidates and maps them into prompt items.
		/// </summary>
		/// <returns>Learn candidate items with the total candidate count.</returns>
		private static async Task<PracticeLearnCandidatesResult> LoadLearnCandidatesAsync()
		{
			var responseJson = await HttpClient.GetTaskAsync(NetworkEndpoints.TranslationLearn);
			if (string.IsNullOrWhiteSpace(responseJson))
			{
				return new PracticeLearnCandidatesResult();
			}

			var response = JsonConvert.DeserializeObject<PracticeTranslationLearnResponsePayload>(responseJson);
			if (response?.Candidates == null || response.Candidates.Count == 0)
			{
				return new PracticeLearnCandidatesResult();
			}

			var items = response.Candidates
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

			return new PracticeLearnCandidatesResult
			{
				Items = items,
				CandidateCount = response.Candidates.Count
			};
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

		/// <summary>
		/// Loads total learned card count for summary display.
		/// </summary>
		/// <returns>Total number of learned cards.</returns>
		private static async Task<int> LoadLearnedCountAsync()
		{
			var responseJson = await HttpClient.GetTaskAsync(NetworkEndpoints.Translation);
			if (string.IsNullOrWhiteSpace(responseJson))
			{
				return 0;
			}

			var response = JsonConvert.DeserializeObject<PracticeTranslationListResponsePayload>(responseJson);
			return response?.Cards?.Count ?? 0;
		}

		/// <summary>
		/// Loads total learn candidate count for summary display.
		/// </summary>
		/// <returns>Total number of learn candidates.</returns>
		private static async Task<int> LoadLearnCandidateCountAsync()
		{
			var responseJson = await HttpClient.GetTaskAsync(NetworkEndpoints.TranslationLearn);
			if (string.IsNullOrWhiteSpace(responseJson))
			{
				return 0;
			}

			var response = JsonConvert.DeserializeObject<PracticeTranslationLearnResponsePayload>(responseJson);
			return response?.Candidates?.Count ?? 0;
		}

		/// <summary>
		/// Builds the tab header summary for the current tab.
		/// </summary>
		/// <param name="tab">Active tab.</param>
		/// <param name="learnedCount">Number of learned cards.</param>
		/// <param name="candidateCount">Number of learn candidates.</param>
		/// <returns>Summary payload for the UI header.</returns>
		private static PracticeTabSummaryPayload BuildTabSummary(PracticeTabType tab, int learnedCount, int candidateCount)
		{
			var safeLearned = Math.Max(0, learnedCount);
			var safeCandidates = Math.Max(0, candidateCount);
			return new PracticeTabSummaryPayload
			{
				TabLabel = GetTabLabel(tab),
				LearnedCount = safeLearned,
				TotalCount = safeLearned + safeCandidates
			};
		}

		/// <summary>
		/// Gets the display label for a practice tab.
		/// </summary>
		/// <param name="tab">Target tab.</param>
		/// <returns>Localized tab label.</returns>
		private static string GetTabLabel(PracticeTabType tab)
		{
			return tab switch
			{
				PracticeTabType.Review => "Ôn tập",
				PracticeTabType.Difficult => "Từ khó",
				PracticeTabType.Starred => "Từ sao",
				PracticeTabType.Learn => "Học",
				_ => "Luyện tập"
			};
		}

		/// <summary>
		/// Result payload for learn candidate loading.
		/// </summary>
		private sealed class PracticeLearnCandidatesResult
		{
			public List<PracticePromptItemPayload> Items { get; set; } = new List<PracticePromptItemPayload>();

			public int CandidateCount { get; set; }
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
