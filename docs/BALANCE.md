# Balance notes

What the simulations say, what was changed because of them, and what is left for Angel to decide. All runs are the Sim: 4 players, Timed mode, 30 rounds, 1,000 games each (seed 20000 up), random characters.

## Bot strength (Phase 10)

| Match-up (even seats listed first) | Even seats won | Even share |
| --- | --- | --- |
| Normal vs Easy | 62.4% | 50% |
| Hard vs Normal | 58.1% | 50% |
| Hard vs Easy | 69.5% | 50% |

Hard beats Easy most of the time, and Normal sits in between, as Section 23 wants.

### What made a bot stronger (found by trying settings in the Sim)
- **Paying more for property.** Hard bots value property 20% higher than Normal bots and win more auctions. At 1.0 they only tied Normal; at 1.2 they won about 58%; at 1.4 and above they did worse (overpaying).
- **A smaller cash reserve, sized to what is ahead.** Half of the biggest rent or tax in the next 12 spaces. Cash sitting in the bank does not win a Timed game, buildings do.
- **Lobbying lost money.** Easy bots spend about $610 per game lobbying. Hard bots spend about $300, and a version that lobbied freely lost to Normal. Hard lobbies only 10% of spare cash.
- **Blocking bids did not clearly help.** Section 23.2 asks Hard to bid to block an opponent's set. It is in, with a small bonus (10% of the group's value). Bigger blocking bonuses did slightly worse in the Sim (within noise, but never better).

## Flags for Angel (not changed)
- **The Mogul still wins more than an even share.** 30% with Easy bots, 35% with Hard bots (even share is 25%, target is no more than 5 points over). Earlier tuning in DECISIONS #91 to #93 already cut it from 49%. It is the only character over the target in the Hard run. Your note stands: with human players the Mogul's edge should be smaller, so I have not tuned it further.
- **Downzone passes about once per game** with Easy bots (DECISIONS #109).
- **Council votes** pass about 60% of the time, about 9 per game.
- **Hard bots go bankrupt more often** (0.10 per game against 0.01 for Easy). That is the smaller reserve at work and is a trade the bot makes on purpose.
- **Turn order is fair:** positions 1 to 4 each won between 24% and 27%.

## Trades (2026-10-21)
Normal and Hard bots now judge a trade that hands over a color group by the rent it should earn (decision #134), and bots answer with counter-offers. 1000 games each, 4 players, Timed, random characters, seed 1:

| Match-up | Before | After |
| --- | --- | --- |
| HardVsEasy (Hard seats' win rate) | 64.9% | 64.5% |
| NormalVsEasy (Normal seats' win rate) | 60.7% | 60.3% |
| Trades per game | 0.0 | 0.8 |
| Buildings per game | 13 | 17 |

Win rates moved less than the noise, so the new judgement doesn't make bots worse; it makes groups (and so buildings) happen more often.

Later the same day Easy bots got the same sums with a 1.25 caution (decision #139): HardVsEasy 65.8%, NormalVsEasy 59.1%, bot-to-bot trades about 0.1 per game (the old Easy rule had been selling sets too cheaply).

## Blocking a monopoly (2026-10-21)
Every bot now bids extra to stop one opponent finishing a color group, and Easy bots value finishing their own group (decision #135). Measured on top of the trades change above, 1000 games each, 4 players, Timed, random characters, seed 1:

| Run | Before | After |
| --- | --- | --- |
| NormalVsEasy (Normal seats' win rate) | 60.3% | 59.1% |
| HardVsEasy (Hard seats' win rate) | 64.5% | 66.1% |
| HardVsNormal (Hard seats' win rate) | 54.1% | 57.4% |
| Easy only: buildings per game | 6.5 | 9.1 |
| Bankruptcies per game (Easy only / HardVsEasy) | 0.01 / 0.03 | 0.02 / 0.05 |

Game length stayed at 30 rounds. What else was tried:
- **Blocking alone, with no own-group bonus for Easy:** all-Easy tables almost never finished a set (0.1 buildings per game), because three blockers always beat one Easy bot that didn't know the set was worth more to it.
- **Bigger blocking shares (15% Easy, 25% Normal and Hard):** Normal fell to 53% to 56% against Easy and 38% against Hard. Blocking helps the other players as much as the blocker, so paying a lot for it loses games. 10% keeps the order Easy < Normal < Hard.

## Fewer Momentum tokens (2026-10-21)
Start with 2, hold 2, none from GO, and Detour cancels a Nudge (decisions #140 and #141). 500 games each, 4 players, Timed 30 rounds, random characters, seed 1. "Before" is the same code run with `--set startingMomentum=1 --set momentumCap=5 --set goMomentumGain=1`.

| Run | Before | After |
| --- | --- | --- |
| Nudges per game (Normal only) | 25.8 | 15.8 |
| Average rent payment (Normal only) | $57 | $91 |
| Bankruptcies per game (Normal only / Easy only) | 0.09 / 0.02 | 0.31 / 0.04 |
| Buildings per game (Normal only) | 20.8 | 24.9 |
| HardVsEasy (Hard seats' win rate) | 64.8% | 80.4% |

With fewer tokens players can't dodge rent as often, so rent hurts more and the better builder pulls ahead. That widens the gap between Hard and Easy bots a lot. Easy bots were not retuned: worth watching in playtests.

## Money, game length and bot personalities (2026-10-21)
$1,200 start, $300 GO (the table below was run at $1,000; Angel asked for $1,200 after the runs, so those numbers are not re-measured), no catch-up bonus, 40 rounds (decision #143), and secret bot personalities (#144). 500 games each, 4 players, random characters, seed 1, all with the new Momentum rules.

Options tried before Angel picked (4 Normal bots):

| Setting | Buildings | Close calls | Bankruptcies | Cash at the end | Average rent |
| --- | --- | --- | --- | --- | --- |
| Old money, 30 rounds | 25 | 3.2 | 0.31 | $417 | $91 |
| Old money, 40 rounds | 33 | 5.0 | 0.82 | $526 | $109 |
| Old money, 50 rounds | 40 | 6.3 | 1.30 | $642 | $121 |
| GO $250 + catch-up $200, 50 rounds | 47 | 7.7 | 1.29 | $844 | $134 |
| GO $300, 50 rounds | 51 | 8.0 | 1.50 | $960 | $146 |
| $2,000 start + GO $250, 50 rounds | 56 | 7.0 | 1.83 | $985 | $171 |
| **$1,000 start, GO $300, no catch-up, 40 rounds (first pick, now $1,200)** | 32 | 6.0 | 0.54 | $601 | $91 |

A close call is cash under $150 that later climbs back to $400 without going bankrupt. More money means more buildings, so bigger rents and more bankruptcies, not fewer. The picked setting starts slower (first building around round 5 on average, not 3) and has fewer knockouts than the old money at the same length.

With the picked setting and personalities: HardVsEasy 75.0% (80.4% at 30 rounds with the old money), Easy only 0.15 bankruptcies and 15 buildings per game. Win rate by personality (Normal / Easy tables): Aggressive 31% / 30%, Balanced 25% / 26%, Reserved 19% / 20%.

## Bots value whole color groups and haggle (2026-10-21)
Decision #148. 1000 games each, 4 players, Timed, random characters:

| Run | Before | After |
| --- | --- | --- |
| HardVsEasy, seed 1 (Hard seats' win rate) | 76.5% | 75.9% |
| HardVsNormal, 40 rounds, seed 7 (Hard seats) | 68.6% | 68.6% |
| Mixed, seed 1: trades per game | 0.3 | 0.6 |
| Mixed, seed 1: buildings per game | 6.8 | 8.1 |
| HardVsNormal: win rate Aggressive / Balanced / Reserved | 27.8 / 25.4 / 21.7% | 27.6 / 25.5 / 21.8% |

Win rates moved less than the noise. Bot-to-bot trades stay rare (0.1 to 0.6 per game), because a bot selling the last piece of a group now asks for what the group is worth to the buyer too. The change shows most when a person trades with a bot.

## How to repeat this
`dotnet run -c Release --project src/CityTycoon.Sim -- --games 1000 --players 4 --bots HardVsEasy --mode timed --characters random --seed 20000 --out <new folder>`
`--set name=value` changes any rules.json number for one run (repeatable), and `--rounds` sets the Timed limit.
`--bots` takes `Random`, `Easy`, `Normal`, `Hard`, `Mixed`, `NormalVsEasy`, `HardVsEasy` or `HardVsNormal`. The report prints the match-up result for the two-kind modes.
