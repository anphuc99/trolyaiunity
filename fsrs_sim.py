#!/usr/bin/env python3
"""
FSRS v5  Spaced Repetition Simulator
=====================================
Input : ratings array  — 0=Again  1=Hard  2=Good  3=Easy
Output: intervals array — days until next review after each session

Usage:
    python fsrs_sim.py
    python fsrs_sim.py "0 0 1 2 2 2"
    python fsrs_sim.py "[0,0,1,2,2,2]"
"""
import math
import sys

# ─── FSRS v5 Default Parameters ───────────────────────────────────────────────
W = [
    0.4072,   # w0   initial S for Again
    1.1829,   # w1   initial S for Hard
    3.1262,   # w2   initial S for Good
    7.2102,   # w3   initial S for Easy
    5.5013,   # w4   D0 midpoint  (= D0 when grade=Good)
    1.0651,   # w5   difficulty sensitivity
    0.9019,   # w6   difficulty update per grade step
    0.1544,   # w7   mean-reversion weight toward D0(Good)
    1.5256,   # w8   recall stability — exp base
    0.1147,   # w9   recall stability — S-decay exponent
    1.0039,   # w10  recall stability — R factor
    1.9619,   # w11  forget stability — base
    0.1108,   # w12  forget stability — D exponent
    0.2920,   # w13  forget stability — S exponent
    2.2700,   # w14  forget stability — R factor
    0.0789,   # w15  Hard penalty   (< 1 → fewer days)
    2.9898,   # w16  Easy bonus     (> 1 → more  days)
    0.5100,   # w17  (short-term learning, not used in daily sim)
    0.3634,   # w18  (short-term learning, not used in daily sim)
]

DECAY            = -0.5
# FACTOR chosen so that next_interval(s) == s days to reach TARGET_RETENTION:
#   FACTOR = target^(1/DECAY) - 1  →  19/81 ≈ 0.2346
FACTOR           = 0.9 ** (1.0 / DECAY) - 1
TARGET_RETENTION = 0.9           # aim for 90 % recall at review time

GRADE_NAMES = ["Again", "Hard", "Good", "Easy"]


# ─── Core FSRS v5 Formulas ────────────────────────────────────────────────────

def retrievability(t: float, s: float) -> float:
    """
    R(t, S) — probability of recall after t days with stability S.
    Reaches TARGET_RETENTION exactly when t == next_interval(S).
    """
    return (1.0 + FACTOR * t / s) ** (1.0 / DECAY)


def next_interval(s: float) -> int:
    """
    Schedule interval (days) so that R == TARGET_RETENTION.
    Simplifies to round(S) because FACTOR is defined that way.
    """
    ivl = s / FACTOR * (TARGET_RETENTION ** (1.0 / DECAY) - 1.0)
    return max(1, round(ivl))


def _clamp(x: float, lo: float = 1.0, hi: float = 10.0) -> float:
    return max(lo, min(hi, x))


def init_difficulty(g: int) -> float:
    """
    Initial difficulty for grade g (1–4).
    D0(Again=1) ≈ 7.6  D0(Hard=2) ≈ 6.5
    D0(Good=3)  = 5.5  D0(Easy=4) ≈ 4.4
    Range clamped to [1, 10].
    """
    return _clamp(W[4] - (g - 3) * W[5])


def update_difficulty(d: float, g: int) -> float:
    """
    Updated difficulty after grade g.
    Pressing Easy makes cards easier; pressing Again makes them harder.
    Mean-reversion (w7) gently pulls difficulty back toward D0(Good).
    """
    d_new    = d - W[6] * (g - 3)
    d_revert = W[7] * W[4] + (1.0 - W[7]) * d_new
    return _clamp(d_revert)


def recall_stability(d: float, s: float, r: float, g: int) -> float:
    """
    New stability after a successful recall (g ∈ {2=Hard, 3=Good, 4=Easy}).
    - Higher current difficulty → slower stability growth.
    - Lower retrievability (forgotten more) → larger stability boost.
    - Hard: multiplied by w15 < 1  (smaller boost).
    - Easy: multiplied by w16 > 1  (larger  boost).
    """
    hp = W[15] if g == 2 else 1.0   # Hard penalty
    eb = W[16] if g == 4 else 1.0   # Easy bonus
    return s * (
        math.exp(W[8])
        * (11.0 - d)
        * (s ** -W[9])
        * (math.exp(W[10] * (1.0 - r)) - 1.0)
        * hp * eb
        + 1.0
    )


def forget_stability(d: float, s: float, r: float) -> float:
    """
    New stability after a lapse (Again, g=1).
    Card resets but retains a fraction of previous stability.
    Higher difficulty → slower recovery.
    """
    return (
        W[11]
        * (d ** -W[12])
        * ((s + 1.0) ** W[13] - 1.0)
        * math.exp(W[14] * (1.0 - r))
    )


# ─── Simulator ────────────────────────────────────────────────────────────────

def simulate(ratings: list) -> tuple:
    """
    Simulate a FSRS v5 review sequence.

    Parameters
    ----------
    ratings : list[int]
        Sequence of review grades: 0=Again, 1=Hard, 2=Good, 3=Easy.

    Returns
    -------
    intervals : list[int]
        Days until the next review after each session.
    rows : list[str]
        Human-readable log lines (one per session).
    """
    s = d = None
    elapsed = 0
    intervals, rows = [], []

    for i, rating in enumerate(ratings):
        g    = rating + 1             # FSRS internal grade 1–4
        name = GRADE_NAMES[rating]

        if i == 0:                    # ── new card: first encounter
            s = W[g - 1]             #   initial stability from w0..w3
            d = init_difficulty(g)
            r = None
        else:                         # ── repeat review
            r = retrievability(elapsed, s)
            if g == 1:
                s = forget_stability(d, s, r)
            else:
                s = recall_stability(d, s, r, g)
            d = update_difficulty(d, g)

        ivl = next_interval(s)
        intervals.append(ivl)

        r_str = f"{r:.0%}" if r is not None else "  —"
        rows.append(
            f"  #{i+1:02d}  {name:<5}  "
            f"R={r_str:<5}  S={s:7.2f}d  D={d:.2f}  →  {ivl:3d} day(s)"
        )
        elapsed = ivl

    return intervals, rows


# ─── CLI ──────────────────────────────────────────────────────────────────────

def parse_ratings(text: str) -> list:
    text = text.strip().strip("[]").replace(",", " ")
    parts = text.split()
    if not parts:
        raise ValueError("Mảng rỗng.")
    ratings = [int(p) for p in parts]
    if any(r < 0 or r > 3 for r in ratings):
        raise ValueError("Mỗi rating phải là 0, 1, 2 hoặc 3.")
    return ratings


def print_result(ratings: list, intervals: list, rows: list) -> None:
    print()
    print(f"  Input  : {ratings}")
    print(f"  Output : {intervals}   ← số ngày đến lần ôn tiếp theo")
    print()
    print(f"  {'#':<4} {'Grade':<6} {'R trước':<8} {'Stability':>10} {'Difficulty':>10}  {'Tiếp theo'}")
    print("  " + "─" * 62)
    for row in rows:
        print(row)
    print()


BANNER = """
╔════════════════════════════════════════════════════════╗
║            FSRS v5  Spaced Repetition Simulator        ║
║                                                        ║
║  Grades:  0 = Again   1 = Hard   2 = Good   3 = Easy  ║
║                                                        ║
║  Stability ≈ số ngày đến lần ôn tập tiếp theo (90 %)  ║
╚════════════════════════════════════════════════════════╝"""


def main() -> None:
    print(BANNER)

    # ── Accept ratings from CLI argument
    if len(sys.argv) > 1:
        raw = " ".join(sys.argv[1:])
        try:
            ratings = parse_ratings(raw)
            intervals, rows = simulate(ratings)
            print_result(ratings, intervals, rows)
        except (ValueError, TypeError) as e:
            print(f"  Lỗi: {e}")
        return

    # ── Interactive mode
    print("\n  Nhập ratings để mô phỏng, hoặc Enter để thoát.")
    print("  Ví dụ: 0 0 1 2 2 2   hoặc   [0,0,1,2,2,2]\n")

    while True:
        try:
            raw = input("  ratings> ").strip()
        except (EOFError, KeyboardInterrupt):
            print()
            break

        if not raw:
            break

        try:
            ratings = parse_ratings(raw)
        except (ValueError, TypeError) as e:
            print(f"  ✗ Lỗi: {e}\n")
            continue

        intervals, rows = simulate(ratings)
        print_result(ratings, intervals, rows)


if __name__ == "__main__":
    main()
