#!/usr/bin/env node
/**
 * FSRS v5 Spaced Repetition Simulator (ts-fsrs)
 * ===============================================
 * Input  : ratings array — 0=Again  1=Hard  2=Good  3=Easy
 * Output : intervals array — thời gian đến lần ôn tiếp theo
 *          Learning phase → phút (e.g. "1m")
 *          Review  phase  → ngày  (e.g. "4d")
 *
 * Usage:
 *   node fsrs_sim.js
 *   node fsrs_sim.js "0 0 1 2 2 2"
 *   node fsrs_sim.js "[0,0,1,2,2,2]"
 *   node fsrs_sim.js --r=0.85 "0 0 1 2 2 2"   ← tỉ lệ nhớ 85%
 *   node fsrs_sim.js --r=0.95 "2 2 2 2 2"     ← tỉ lệ nhớ 95%
 *
 * --r      : tỉ lệ nhớ mục tiêu (request_retention), 0.70–0.99, mặc định 0.90
 *            Cao hơn → interval ngắn hơn  |  Thấp hơn → interval dài hơn
 *
 * Workload simulation (số từ cần ôn mỗi ngày):
 *   node fsrs_sim.js --days=180 --words=20 --rating=2
 *   node fsrs_sim.js --days=180 --words=20 --rating=2 --r=0.85
 *
 * --days   : số ngày mô phỏng (mặc định 180)
 * --words  : số từ mới học mỗi ngày (mặc định 20)
 * --rating : mức đánh giá nhất quán  0–3 (mặc định 2 = Good)
 */

import { FSRS, Rating, createEmptyCard, State } from "ts-fsrs";
import readline from "readline";

// ─── Constants ────────────────────────────────────────────────────────────────

/** Map user-facing grade (0–3) to ts-fsrs Rating enum (1–4). */
const GRADE_TO_RATING = [
  Rating.Again, // 0
  Rating.Hard,  // 1
  Rating.Good,  // 2
  Rating.Easy,  // 3
];

const GRADE_NAMES = ["Again", "Hard", "Good", "Easy"];

// ─── Helpers ─────────────────────────────────────────────────────────────────

/**
 * Format an interval: learning/relearning → minutes, review → days.
 * @param {object} card  - ts-fsrs Card result
 * @param {Date}   now   - time the review was done
 * @returns {{ label: string, sortable: number }}
 */
function formatInterval(card, now) {
  const ms   = card.due - now;
  const mins = Math.round(ms / 60_000);
  const days = card.scheduled_days;

  const isReview = card.state === State.Review;

  if (isReview && days >= 1) {
    return { label: `${days}d`, sortable: days };
  }
  // Learning / Relearning: show minutes
  return { label: `${mins}m`, sortable: mins / 1440 };
}

// ─── Simulator ───────────────────────────────────────────────────────────────

/**
 * Simulate a FSRS review sequence using ts-fsrs.
 * @param {number[]} ratings - Array of 0=Again,1=Hard,2=Good,3=Easy.
 * @returns {{ intervals: string[], rows: string[] }}
 */
/**
 * @param {number[]} ratings
 * @param {number}   retention  Target retention 0.70–0.99 (default 0.90)
 */
function simulate(ratings, retention = 0.90) {
  const f   = new FSRS({ request_retention: retention });
  let card  = createEmptyCard();
  let now   = new Date("2026-01-01T00:00:00Z"); // deterministic start date

  const intervals = [];
  const rows      = [];

  for (let i = 0; i < ratings.length; i++) {
    const input  = ratings[i];
    const rating = GRADE_TO_RATING[input];
    const name   = GRADE_NAMES[input];

    const result    = f.next(card, now, rating);
    const nextCard  = result.card;
    const { label } = formatInterval(nextCard, now);

    const stateStr  = State[nextCard.state].padEnd(11);
    const sStr      = nextCard.stability.toFixed(2).padStart(8);
    const dStr      = nextCard.difficulty.toFixed(2).padStart(8);
    const repsStr   = String(nextCard.reps).padStart(3);

    rows.push(
      `  #${String(i + 1).padStart(2, "0")}  ` +
      `${name.padEnd(6)}  ` +
      `state=${stateStr}  ` +
      `S=${sStr}  ` +
      `D=${dStr}  ` +
      `reps=${repsStr}  ` +
      `→  ${label}`
    );

    intervals.push(label);

    // Advance time to the scheduled due date for the next review
    now  = nextCard.due;
    card = nextCard;
  }

  return { intervals, rows };
}

// ─── Workload Simulator ───────────────────────────────────────────────────────

/**
 * Simulate one card's full review schedule starting from day 0,
 * always rated with `rating`, stopping once the next due day > maxDays.
 * @param {number} retention   - request_retention 0.70–0.99
 * @param {number} rating      - user grade 0–3
 * @param {number} maxDays     - simulation window in days
 * @returns {Array<{relDay:number, reps:number}>}
 *   relDay = days from card creation, reps = repetitions done that day
 */
function getSingleCardSchedule(retention, rating, maxDays) {
  const f       = new FSRS({ request_retention: retention });
  const tsRating = GRADE_TO_RATING[rating];
  let card = createEmptyCard();
  let now  = new Date(0);         // epoch = day 0
  const schedule  = [];
  let sameDayReps = 0;
  let currentAbsDay = 0;

  while (true) {
    const result  = f.next(card, now, tsRating);
    const next    = result.card;
    sameDayReps++;

    const dueDayAbs = Math.floor(next.due.getTime() / 86_400_000);

    if (dueDayAbs <= currentAbsDay) {
      // Same-day learning step — advance clock but stay on this day.
      now  = next.due;
      card = next;
    } else {
      // Flush the current day's reps.
      schedule.push({ relDay: currentAbsDay, reps: sameDayReps });
      sameDayReps = 0;

      if (dueDayAbs > maxDays) break;

      currentAbsDay = dueDayAbs;
      now  = next.due;
      card = next;
    }
  }

  return schedule;
}

/**
 * Build a per-day workload table for learning `newPerDay` new cards every day
 * over `totalDays`, all rated consistently with `rating`.
 * @returns {Array<{new:number, reviews:number, totalCards:number, totalReps:number}>}
 *   Index 1 = day 1, index totalDays = last day (index 0 unused).
 */
function buildWorkload(newPerDay, rating, totalDays, retention) {
  const relSched = getSingleCardSchedule(retention, rating, totalDays);

  const work = Array.from({ length: totalDays + 1 }, () => ({
    new: 0, reviews: 0, totalCards: 0, totalReps: 0,
  }));

  // Each day `startDay` introduces `newPerDay` cards.
  // Their future reviews land on startDay + relDay.
  for (let startDay = 1; startDay <= totalDays; startDay++) {
    for (const { relDay, reps } of relSched) {
      const absDay = startDay + relDay;
      if (absDay < 1 || absDay > totalDays) continue;

      if (relDay === 0) {
        // Introduction day (includes same-day learning steps).
        work[absDay].new      += newPerDay;
        work[absDay].totalReps += newPerDay * reps;
      } else {
        // Future scheduled review.
        work[absDay].reviews   += newPerDay;
        work[absDay].totalReps += newPerDay * reps;
      }
    }
  }

  for (let d = 1; d <= totalDays; d++) {
    work[d].totalCards = work[d].new + work[d].reviews;
  }

  return work;
}

/**
 * Print the workload table with bar chart and summary.
 */
function printWorkloadResult(work, { newPerDay, rating, totalDays, retention }) {
  const days     = work.slice(1);   // strip unused index 0
  const maxCards = Math.max(...days.map((d) => d.totalCards));
  const BAR_MAX  = 40;
  const scale    = Math.max(1, Math.ceil(maxCards / BAR_MAX));

  console.log();
  console.log(`  ── Workload Simulation ${'─'.repeat(43)}`);
  console.log(`  Words/day : ${newPerDay}  |  Days: ${totalDays}  |  Rating: ${GRADE_NAMES[rating]} (${rating})  |  Retention: ${(retention * 100).toFixed(0)}%`);
  console.log(`  1█ = ${scale} card${scale > 1 ? 's' : ''}`);
  console.log();
  console.log(`  ${'Day'.padStart(4)} │ ${'New'.padStart(4)} │ ${'Reviews'.padStart(7)} │ ${'Total'.padStart(5)} │ Chart`);
  console.log('  ' + '─'.repeat(4) + '─┼─' + '─'.repeat(4) + '─┼─' + '─'.repeat(7) + '─┼─' + '─'.repeat(5) + '─┼─' + '─'.repeat(BAR_MAX + 2));

  for (let d = 1; d <= totalDays; d++) {
    const { new: n, reviews: r, totalCards: t } = work[d];
    const bar = '█'.repeat(Math.max(0, Math.round(t / scale)));
    console.log(
      `  ${String(d).padStart(4)} │ ${String(n).padStart(4)} │ ${String(r).padStart(7)} │ ${String(t).padStart(5)} │ ${bar}`
    );
  }

  // ── Summary
  const totalNew     = days.reduce((s, d) => s + d.new,        0);
  const totalReviews = days.reduce((s, d) => s + d.reviews,    0);
  const totalCards   = days.reduce((s, d) => s + d.totalCards, 0);
  const totalReps    = days.reduce((s, d) => s + d.totalReps,  0);
  const avgCards     = (totalCards / totalDays).toFixed(1);
  const peakDay      = days.reduce((best, d, i) => d.totalCards > best.v ? { v: d.totalCards, d: i + 1 } : best, { v: 0, d: 1 });

  console.log();
  console.log('  ── Summary ' + '─'.repeat(50));
  console.log(`  Từ mới giới thiệu      : ${totalNew.toLocaleString()}`);
  console.log(`  Lượt ôn tập (scheduled): ${totalReviews.toLocaleString()}`);
  console.log(`  Tổng thẻ gặp mỗi ngày  : ${totalCards.toLocaleString()} (avg ${avgCards}/ngày)`);
  console.log(`  Tổng reps (incl. learn): ${totalReps.toLocaleString()}`);
  console.log(`  Ngày bận nhất          : ngày ${peakDay.d} → ${peakDay.v} thẻ`);
  console.log();
}

// ─── CLI helpers ─────────────────────────────────────────────────────────────

/**
 * Extract --r=<value> flag from argv tokens.
 * Returns { retention: number, rest: string[] }.
 */
function parseArgs(argv) {
  let retention = 0.90;
  let words     = 20;
  let days      = null;   // null = not specified (per-card mode)
  let rating    = 2;
  const rest    = [];

  for (const arg of argv) {
    let m;
    if ((m = arg.match(/^--r=(.+)$/))) {
      const v = parseFloat(m[1]);
      if (isNaN(v) || v < 0.70 || v > 0.99)
        throw new Error("--r phải là số từ 0.70 đến 0.99 (ví dụ: --r=0.85)");
      retention = v;
    } else if ((m = arg.match(/^--words=(\d+)$/))) {
      words = parseInt(m[1], 10);
      if (words < 1) throw new Error("--words phải >= 1");
    } else if ((m = arg.match(/^--days=(\d+)$/))) {
      days = parseInt(m[1], 10);
      if (days < 1) throw new Error("--days phải >= 1");
    } else if ((m = arg.match(/^--rating=([0-3])$/))) {
      rating = parseInt(m[1], 10);
    } else {
      rest.push(arg);
    }
  }

  return { retention, words, days, rating, rest };
}

function parseRatings(text) {
  const cleaned = text.trim().replace(/^\[/, "").replace(/\]$/, "").replace(/,/g, " ");
  const parts   = cleaned.split(/\s+/).filter(Boolean);
  if (!parts.length) throw new Error("Mảng rỗng.");
  const ratings = parts.map(Number);
  if (ratings.some((r) => !Number.isInteger(r) || r < 0 || r > 3)) {
    throw new Error("Mỗi rating phải là 0, 1, 2 hoặc 3.");
  }
  return ratings;
}

function printResult(ratings, intervals, rows, retention) {
  console.log();
  console.log(`  Tỉ lệ nhớ: ${(retention * 100).toFixed(0)}%  (--r=${retention})`);
  console.log(`  Input    : [${ratings.join(", ")}]`);
  console.log(`  Output   : [${intervals.join(", ")}]`);
  console.log(`             (Learning phase = phút "m"  |  Review phase = ngày "d")`);
  console.log();
  console.log(`  ${"#".padEnd(4)} ${"Grade".padEnd(7)} ${"State".padEnd(16)} ${"Stability".padStart(10)} ${"Difficulty".padStart(10)} ${"Reps".padStart(5)}  Tiếp theo`);
  console.log("  " + "─".repeat(74));
  rows.forEach((r) => console.log(r));
  console.log();
}

const BANNER = `
╔════════════════════════════════════════════════════════════════╗
║           FSRS v5 Spaced Repetition Simulator (ts-fsrs)       ║
║                                                                ║
║  Per-card mode:                                                ║
║    node fsrs_sim.js [--r=0.9] "0 0 1 2 2 2"                   ║
║                                                                ║
║  Workload mode (từ cần ôn mỗi ngày):                           ║
║    node fsrs_sim.js --days=180 --words=20 --rating=2 [--r=0.9] ║
║                                                                ║
║  Grades: 0=Again  1=Hard  2=Good  3=Easy                       ║
║  --r     tỉ lệ nhớ 0.70–0.99  (mặc định 0.90)                 ║
╚════════════════════════════════════════════════════════════════╝`;

// ─── Main ────────────────────────────────────────────────────────────────────

function main() {
  console.log(BANNER);

  // Parse all flags and remaining args
  let retention, words, days, rating, rest;
  try {
    ({ retention, words, days, rating, rest } = parseArgs(process.argv.slice(2)));
  } catch (e) {
    console.error(`  Lỗi: ${e.message}`);
    process.exit(1);
  }

  // ── Workload mode: triggered when --days is specified
  if (days !== null) {
    const work = buildWorkload(words, rating, days, retention);
    printWorkloadResult(work, { newPerDay: words, rating, totalDays: days, retention });
    return;
  }

  // ── Per-card mode (CLI)
  if (rest.length > 0) {
    const raw = rest.join(" ");
    try {
      const ratings              = parseRatings(raw);
      const { intervals, rows } = simulate(ratings, retention);
      printResult(ratings, intervals, rows, retention);
    } catch (e) {
      console.error(`  Lỗi: ${e.message}`);
      process.exit(1);
    }
    return;
  }

  // ── Interactive mode (per-card)
  console.log("\n  Per-card mode: nhập ratings, hoặc Enter / Ctrl+C để thoát.");
  console.log("  Workload mode: dùng --days=180 --words=20 --rating=2 (xem banner trên).");
  console.log(`  Tỉ lệ nhớ: ${(retention * 100).toFixed(0)}%  |  Ví dụ: 0 0 1 2 2 2\n`);

  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
    prompt: "  ratings> ",
  });

  rl.prompt();

  rl.on("line", (line) => {
    const raw = line.trim();
    if (!raw) {
      rl.close();
      return;
    }
    try {
      const ratings              = parseRatings(raw);
      const { intervals, rows } = simulate(ratings, retention);
      printResult(ratings, intervals, rows, retention);
    } catch (e) {
      console.error(`  ✗ Lỗi: ${e.message}\n`);
    }
    rl.prompt();
  });

  rl.on("close", () => process.exit(0));
}

main();
