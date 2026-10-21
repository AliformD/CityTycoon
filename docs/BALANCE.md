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

## How to repeat this
`dotnet run -c Release --project src/CityTycoon.Sim -- --games 1000 --players 4 --bots HardVsEasy --mode timed --characters random --seed 20000 --out <new folder>`
`--bots` takes `Random`, `Easy`, `Normal`, `Hard`, `Mixed`, `NormalVsEasy`, `HardVsEasy` or `HardVsNormal`. The report prints the match-up result for the two-kind modes.
