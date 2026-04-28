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
 * --r  : tỉ lệ nhớ mục tiêu (request_retention), 0.70–0.99, mặc định 0.90
 *        Cao hơn  → interval ngắn hơn (ôn nhiều hơn để chắc chắn nhớ)
 *        Thấp hơn → interval dài hơn  (chấp nhận quên nhiều hơn)
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

// ─── CLI helpers ─────────────────────────────────────────────────────────────

/**
 * Extract --r=<value> flag from argv tokens.
 * Returns { retention: number, rest: string[] }.
 */
function parseArgs(argv) {
  let retention = 0.90;
  const rest = [];
  for (const arg of argv) {
    const m = arg.match(/^--r=(.+)$/);
    if (m) {
      const v = parseFloat(m[1]);
      if (isNaN(v) || v < 0.70 || v > 0.99) {
        throw new Error("--r phải là số từ 0.70 đến 0.99 (ví dụ: --r=0.85)");
      }
      retention = v;
    } else {
      rest.push(arg);
    }
  }
  return { retention, rest };
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
╔══════════════════════════════════════════════════════════╗
║         FSRS v5 Spaced Repetition Simulator (ts-fsrs)   ║
║                                                          ║
║  Grades:  0 = Again   1 = Hard   2 = Good   3 = Easy    ║
║                                                          ║
║  --r=<0.70–0.99>  tỉ lệ nhớ mục tiêu (mặc định 0.90)   ║
║  Learning/Relearning phase  → interval tính bằng phút   ║
║  Review phase               → interval tính bằng ngày   ║
╚══════════════════════════════════════════════════════════╝`;

// ─── Main ────────────────────────────────────────────────────────────────────

function main() {
  console.log(BANNER);

  // Parse --r flag and remaining args
  let retention, rest;
  try {
    ({ retention, rest } = parseArgs(process.argv.slice(2)));
  } catch (e) {
    console.error(`  Lỗi: ${e.message}`);
    process.exit(1);
  }

  // CLI argument mode
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

  // Interactive mode
  console.log("\n  Nhập ratings để mô phỏng, hoặc Enter / Ctrl+C để thoát.");
  console.log(`  Tỉ lệ nhớ hiện tại: ${(retention * 100).toFixed(0)}% (thay đổi bằng --r=0.85 v.v.)`);
  console.log("  Ví dụ:  0 0 1 2 2 2   hoặc   [0,0,1,2,2,2]\n");

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
