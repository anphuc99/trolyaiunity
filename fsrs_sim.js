/**
 * Simulation: daily review load over 180 days with 10 new words/day.
 *
 * Uses the same REVIEW_CYCLE from fsrs.service.ts.
 * Each new word is due immediately on its learning day (step 0),
 * then scheduled according to the cycle after each review.
 *
 * Usage:  node fsrs_sim.js
 */

const REVIEW_CYCLE = [
  1, 1, 1, 1, 1,
  2, 1, 1,
  4, 1, 1,
  8, 1, 1,
  20, 1, 1, 1,
  60, 1, 1, 1,
  150, 1, 1, 1
];

const TOTAL_DAYS = 180;
const NEW_WORDS_PER_DAY = 10;

// Each word is tracked as { cycleStep, nextReviewDay }
// cycleStep = number of reviews completed so far
// nextReviewDay = the day (0-indexed) when the next review is due

const allWords = [];

// reviewsPerDay[d] = number of reviews the user must do on day d
const reviewsPerDay = new Array(TOTAL_DAYS * 2).fill(0); // extra buffer for reviews beyond 180

for (let day = 0; day < TOTAL_DAYS; day++) {
  // 1. Learn 10 new words — first review is due immediately (same day).
  for (let w = 0; w < NEW_WORDS_PER_DAY; w++) {
    allWords.push({ cycleStep: 0, nextReviewDay: day });
  }

  // 2. Review every word that is due today (nextReviewDay <= day).
  //    In reality a user reviews all due words each day.
  let reviewsToday = 0;
  for (const word of allWords) {
    if (word.nextReviewDay <= day && word.cycleStep < REVIEW_CYCLE.length) {
      reviewsToday++;

      // Advance the cycle
      const intervalDays = REVIEW_CYCLE[word.cycleStep];
      word.cycleStep++;
      word.nextReviewDay = day + intervalDays;
    }
  }

  reviewsPerDay[day] = reviewsToday;
}

// ── Output ──────────────────────────────────────────────────────────────────

console.log("=== FSRS Cycle Simulation ===");
console.log(`New words/day: ${NEW_WORDS_PER_DAY}`);
console.log(`Cycle length : ${REVIEW_CYCLE.length} reviews per word`);
console.log(`Simulation   : ${TOTAL_DAYS} days\n`);

console.log("Day | Reviews | New | Cumulative Words");
console.log("----|---------|-----|------------------");

let maxReviews = 0;
let maxDay = 0;
let totalReviews = 0;

for (let day = 0; day < TOTAL_DAYS; day++) {
  const cumulativeWords = (day + 1) * NEW_WORDS_PER_DAY;
  const r = reviewsPerDay[day];
  totalReviews += r;

  if (r > maxReviews) {
    maxReviews = r;
    maxDay = day;
  }

  console.log(
    `${String(day + 1).padStart(3)} | ${String(r).padStart(7)} | ${String(NEW_WORDS_PER_DAY).padStart(3)} | ${String(cumulativeWords).padStart(16)}`
  );
}

// Count memorised words (cycleStep >= REVIEW_CYCLE.length)
const memorised = allWords.filter(w => w.cycleStep >= REVIEW_CYCLE.length).length;
const stillActive = allWords.filter(w => w.cycleStep < REVIEW_CYCLE.length).length;

// Count how many are still due after day 180
let dueAfter180 = 0;
for (const word of allWords) {
  if (word.cycleStep < REVIEW_CYCLE.length && word.nextReviewDay <= TOTAL_DAYS - 1) {
    dueAfter180++;
  }
}

console.log("\n=== Summary ===");
console.log(`Total words learned  : ${allWords.length}`);
console.log(`Total reviews done   : ${totalReviews}`);
console.log(`Avg reviews/day      : ${(totalReviews / TOTAL_DAYS).toFixed(1)}`);
console.log(`Max reviews in a day : ${maxReviews} (day ${maxDay + 1})`);
console.log(`Words fully memorised: ${memorised}`);
console.log(`Words still in cycle : ${stillActive}`);
console.log(`Due but unreviewed   : ${dueAfter180}`);
