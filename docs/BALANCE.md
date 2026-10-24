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

## Shared house budget in multi-set trades (2026-10-22)
Decision #149. 1000 games each, 4 players, Timed, 40 rounds, random characters, seed 1:

| Run | Before | After |
| --- | --- | --- |
| HardVsEasy (Hard seats' win rate) | 79.3% | 79.3% |
| Mixed: trades per game | 0.9 | 0.9 |
| Mixed: buildings per game | 14.1 | 14.1 |

Identical, as expected: bots only offer cash for the last piece of one group, so the change only shows when a person offers a trade that finishes several groups.

## Trade report card and landing odds (2026-10-22)
Decision #150 added a trade report card and landing odds to every Sim summary. 300 games, HardVsEasy, 4 players, Timed (the Sim's default 30 rounds), random characters, seed 1:

- Offers: 1,458 (about 5 per game), finished: 29 (2%), 1.6 counter-offers per finished trade.
- Rent collected afterwards on what each side got (whole color groups): $24 for the side that wrote the final terms (usually the seller, who got cash), $193 for the other side (the buyer, who finished a set).
- Landing odds by color group (share of all turns that end on one of its spaces): Orange 8.5%, Pink 8.1%, Light Blue 7.9%, Yellow 7.9%, Red 7.3%, Green 6.9%, Brown 4.9%, Dark Blue 4.0%. Per space in `landing.csv`.

Bots almost never agree with each other, because both sides use the same sums: the buyer's limit is usually below the seller's. Making more trades happen is step E of the trading plan.

## Nudge per space and stronger characters (2026-10-22)
Decisions #151 and #152. 1000 games each, 4 players, Timed, 40 rounds, random characters, seed 1. "Before" is main just before this change.

| Character | 4 Normal bots before | after | Hard vs Easy before | after |
| --- | --- | --- | --- | --- |
| Mogul | 43.4 % | 48.9 % | 38.9 % | 36.6 % |
| Hustler | 20.8 % | 28.1 % | 19.4 % | 30.5 % |
| Commuter | 26.2 % | 20.2 % | 27.0 % | 20.0 % |
| Lawyer | 25.0 % | 25.3 % | 21.8 % | 24.2 % |
| Contractor | 23.3 % | 24.7 % | 27.6 % | 30.5 % |
| Politician | 23.4 % | 18.9 % | 24.6 % | 18.5 % |
| City Planner | 19.7 % | 18.0 % | 21.1 % | 19.7 % |
| Medic | 19.0 % | 16.8 % | 20.3 % | 20.7 % |

Even share is 25 %. Other numbers barely moved: Nudges per game 19.3 to 18.4 (Normal), bankruptcies 0.77 to 0.69, first building round 4.1 to 5.5 on average, Hard seats won 79.3 % to 74.2 % against Easy.

### Flags for Angel (not changed)
- **The Mogul got stronger** (it was already the only character over the target). The $20 edge is what was asked for.
- **The Hustler is now one of the strongest.** The lap reroll and the ×3 Stadium both help.
- **The Commuter and the Politician got weaker** even though neither lost anything on paper. The Commuter's tokens now buy fewer spaces (a 2-space Nudge used to cost 1 token, now 2). The Politician drop showed in both runs; it may be that a stronger lobby still costs real cash, and bots that lobby lose games (see "Blocking a monopoly" above). Not investigated further.

## Balance pass after the Hard bot report (2026-10-22)
Decision #153. 1,000 games, 4 Hard bots, Timed, 40 rounds, random characters, seed 1. "Before" is main with decisions #151 and #152.

| Character | before | after |
| --- | --- | --- |
| Mogul | 43.8 % | 33.9 % |
| Hustler | 31.3 % | 29.5 % |
| Commuter | 21.2 % | 22.2 % |
| Lawyer | 22.8 % | 24.4 % |
| Contractor | 23.7 % | 26.4 % |
| Politician | 19.9 % | 20.1 % |
| City Planner | 18.8 % | 20.7 % |
| Medic | 19.3 % | 23.2 % |

Even share is 25 %. The Mogul is still a little over the 30 % target, mostly from winning tied sealed bids (about 1 in 6 sealed auctions tie, because bots bid in $10 steps). Desperate bots now bet at the Casino: 0.3 bets per game, players lost $2,100 in total over 1,000 games. Game length is unchanged (39.8 rounds).

## Loan sizes and bots borrowing to build (2026-10-22)
Decision #154. 1,000 games, 4 Hard bots, Timed, 40 rounds, random characters, seed 1. "Before" is decision #153.

| | before | after |
| --- | --- | --- |
| Loans taken per game | 2.7 | 7.4 |
| Buildings built per game | 32.9 | 36.5 |
| Average rent payment | $102 | $116 |
| Bankruptcies per game | 0.74 | 1.02 |
| Games with a bankruptcy | 51 % | 66 % |
| Net worth at the end (players still in) | $3,206 | $3,453 |
| Yellow holders who won | 55.0 % | 60.8 % |

Does borrowing to build help? In 2,000 more games, half the seats were allowed to borrow to build and half were not: the borrowers won 28.3 % and the others 21.7 %, and both went bankrupt about as often (22 %). So the extra bankruptcies come from the higher rents hitting other players, not from the borrowers. Asking for 3 times the interest back instead of 2 changed almost nothing. With borrowing turned off (only the new sizes), bankruptcies were 0.67 per game.

Characters barely moved: Mogul 31.3 %, Hustler 31.9 %, Contractor 26.0 %, Lawyer 25.7 %, Medic 23.4 %, Commuter 22.6 %, Politician 20.1 %, City Planner 19.3 %. Game length 39.6 rounds.

## Building higher with Green and Dark Blue (2026-10-22)
Decision #155. 1,000 games, 4 Hard bots, Timed, 40 rounds, random characters, seed 1. "Before" is decision #154.

Counting the dividend and cheap skyscrapers alone changed almost nothing, because the bots' plans were not what held them back. At the end of a game, a Green set was usually stuck under 4-house zoning (Residential) or partly mortgaged; of the Green sets with no buildings at the end, about 3 in 4 had a mortgaged piece. So this change also makes bots push to rezone a district once their set there is built to the limit, and lift mortgages on a whole set first.

| | before | after |
| --- | --- | --- |
| Green dividends paid per game | $26 | $34 |
| Rezone Up passed per game | 0.59 | 0.68 |
| Buildings built per game | 36.5 | 37.6 |
| Green holders who won | 56.9 % | 59.1 % |
| Contractor win rate | 26.0 % | 27.2 % |

Bankruptcies 1.02 to 1.06 per game. Other characters moved by 1 point or less. Green is still held rarely (about 1 in 4 games) and its $200 houses are dear, so its dividend stays small; making it bigger would be a rules change (for example cheaper Green hotels), not a bot change.

## Bids that count the set bonus and the character, trades that count the time left (2026-10-22)
Decision #156. 1,000 games, 4 Hard bots, Timed, 40 rounds, random characters, seed 1. "Before" is decision #155.

| Character | before | character bump at full strength | at 30 % (chosen) |
| --- | --- | --- | --- |
| Mogul | 31.9 % | 25.8 % | 28.0 % |
| Hustler | 30.7 % | 27.5 % | 29.9 % |
| Commuter | 24.2 % | 38.4 % | 27.8 % |
| Lawyer | 24.2 % | 18.6 % | 24.0 % |
| Contractor | 27.2 % | 37.6 % | 29.7 % |
| Politician | 20.3 % | 16.8 % | 19.1 % |
| City Planner | 19.3 % | 16.5 % | 19.2 % |
| Medic | 22.6 % | 19.5 % | 22.8 % |

This is the backfire Angel warned about: at full strength the bump made the Commuter and the Contractor win far more (a Commuter that buys Light Blue does well, so it's a real strength, not a mistake). At 30 % no character is over the 30 % target. Counting the set's perk in bids alone (bump off) moved no character by more than 1 point.

Hard against Easy: Hard seats won 77.0 % before, 76.9 % after, so bidding didn't get worse.

Trades near the end: before this change, 21 trades (in 1,000 games) finished in the last 8 rounds, and the player who paid cash won only 3 of the 19 where someone paid. Now bots don't overpay rivals that late, and no trades finished in the last 8 rounds. Trades in all: 180 to 153 (in 1,000 games).

## Trade fixes: limits, splitting the difference, swaps, spare jail cards (2026-10-22)
Decision #157. 1,000 games, 4 Hard bots, Timed, 40 rounds, random characters, seed 1. "Before" is decision #156.

| | before | after |
| --- | --- | --- |
| Trades finished (1,000 games) | 153 | 392 |
| Share of offers that finished | 3 % | 7 % |
| Swaps (a property for a property) | 0 | 79 |
| Jail cards sold | 0 | 112 |
| Lawyer win rate | 24.0 % | 25.7 % |

In the swaps, each side won about as often as an even share (23 % and 24 %), so swaps help both sides about equally. Other characters moved by under 1.5 points; Politician 19.3 % and City Planner 18.6 % are still the weakest (their own changes come next). Bankruptcies 1.07 to 1.09 per game.

## City Planner tile swaps and no tile fees (2026-10-22)
Decision #158. 1,000 games, 4 Hard bots, Timed, 40 rounds, random characters, seed 1. "Before" is decision #157.

City Planner win rate 18.6 % to 21.3 %. In a game with a Planner it peeks about 3 times, swaps 0.34 times, and saves $70 in tile fees. Other characters moved by up to 3.5 points (Lawyer 25.7 % to 22.2 %, Hustler 29.3 % to 31.5 %); with these seeds a 2 to 3 point swing between runs is normal, because one change shifts every later dice roll. Bankruptcies 1.09 to 1.11 per game.

## The Politician plans its votes; vote deals (2026-10-22)
Decision #159. 1,000 games, 4 Hard bots, Timed, 40 rounds, random characters, seed 1. "Before" is decision #158.

| | Before | Votes in dollars, no deals | With deals |
| --- | --- | --- | --- |
| Politician wins | 18.7 % | 20.9 % | 22.6 % |
| Lobbying per game | $338 | $0 | $0 |
| Vote deals per game | 0 | 0 | 0.6 (about $40 each) |

Lobbying: one extra dollar of lobbying moves the chance of winning a vote by much less than a dollar of what the ordinance is worth, even for the Politician (each of its dollars is worth 2 influence). For example, a 25 % rent boost on a Dark Blue set with hotels is worth about $220 to its owner against the table; lobbying would have to beat that, and it doesn't. A test with the bots treating every ordinance as worth 4 times more still spent only $2 a game. So the bots stopped lobbying, and the Politician gained 2 points from that and from choosing the agenda by dollars, and 2 more from vote deals. Hustler 30.3 %, Contractor 28.8 %, Mogul 27.8 %, Commuter 25.8 %, Lawyer 22.8 %, Medic 22.2 %, City Planner 20.1 %.

## Table talk and temperament (2026-10-22)
Decision #160. 1,000 games, 4 Hard bots, Timed, 40 rounds, random characters, seed 1. "Before" is decision #159.

Remarks per game: 4.1 warnings, 0.5 sassy, 0.3 threats, 0.2 friendly, 0.1 scoldings (most trades the bots offer are for the last piece of a group, so warnings are the most common). Trades finished 410 to 403 per 1,000 games; bankruptcies 1.05 per game both times. Character win rates moved by under half a point (Politician 22.6 % to 22.8 %, Hustler 30.3 % to 30.7 %, City Planner 20.1 % to 19.9 %). Temperament changes margins only a little, so it reads as flavor more than strategy, which was the aim.

## Hustler Casino win bonus (2026-10-22)
Decision #161. Same Sim settings. Hustler 30.7 % to 31.1 % (within normal run-to-run noise); the bonus is worth about $10 on half of the Hustler's Casino landings. The Hustler stays just over the 30 % target, as it was before.

## Politician vote contracts (2026-10-23)
Decision #162. 2,000 games, 4 Hard bots, Timed, 30 rounds, random characters, seed 1. "Before" is decision #161.

| | Before | Contracts | Same rules, contracts off |
| --- | --- | --- | --- |
| Politician wins | 19.7 % | 21.1 % | 21.1 % |
| Bids for the Politician's vote per game | (vote deals: 0.4, $16) | 0.3 | 0 |
| Contracts taken per game | | 0.2 (about $25 each) | 0 |
| Lobbying per game | $0 | $0 | $0 |

The Politician gained about 1.5 points, but from the other changes (equal votes, no vote deals for the other players, City Hall Momentum), not from contracts: switching contracts off gives the same 21.1 %. Bots rarely bid because they value most ordinances at well under $100 to themselves against the table, and one vote only moves the chance of passing by about 10 to 20 points (public opinion and the other votes are always there). A $10 offer already buys the whole vote, so a bid only pays when an ordinance is worth $100 or more to the bidder. Bidding as if every ordinance were worth twice as much (a test, not kept) raised bids to 0.7 a game and the Politician to about 23 % in 1,000 games. Other characters moved by under a point. Human players may well value votes differently from the bots.

## Down payments only in silent auctions, and blocking the first color group (2026-10-23)
Decision #163. 1,000 games, 4 Hard bots, Timed, 40 rounds, random characters, seed 1. The Sim's new "First color group" line counts a group as held when one player has every deed in it (paid off or not).

| | before | down payments silent only | plus first-group blocking (chosen) |
| --- | --- | --- | --- |
| First group, median round | 2 | 2 | 8 |
| Games with a group by round 3 | 78 % | 69 % | 28 % |
| Games with a group by round 5 | 85 % | 75 % | 32 % |
| Groups held by round 10 (per game) | 2.3 | 1.7 | 1.0 |
| First group came from a trade | 1 % | 6 % | 57 % |
| First building, median round | 2 | 2 | 8 |
| Building levels standing at the end | 31.0 | 30.5 | 21.9 |
| Games with a bankruptcy | 68 % | 52 % | 28 % |
| Average rent payment | $117 | $108 | $73 |

Before, 22 % of first groups were won with a down payment in an open auction. The groups that still form early are mostly Brown (the second brown is the last piece, so there is no "second of three" fight) and Light Blue, and the winner pays 3 to 5 times the assessed value for the last piece, so they have little left to build with.

Stronger settings were tried and dropped: pushing back on the second of three at 50 % (up to 2.5 times) gave median round 9 but only 1.5 players built per game; making bots twice as unwilling to trade away the first group meant 63 % of games never had a group at all and trades nearly stopped.

Side effects: games are slower and calmer (less building, fewer bankruptcies), the cost of fewer early monopolies. Hard against Easy: Hard seats won 79.9 % before, 68.8 % after, because every bot now blocks hard, so overbidding for sets pays off less. Characters: Hustler 31.1 to 32.3 %, Mogul 27.6 to 29.7 %, Lawyer 23.0 to 27.3 %, Contractor 28.4 to 20.4 %, City Planner 19.7 to 21.1 %; the others moved by 2 points or less.

## Casino Dice Poker and the Showdown (2026-10-24)
Decision #164. 1,000 games, 4 Hard bots, Timed, 40 rounds, random characters, seed 1, same settings before and after.

| | Before (coin flip, High/Low, Dice Duel) | After (Dice Poker, Showdown) |
| --- | --- | --- |
| Turns ending on the Casino | 2.3 % | 3.6 % |
| Turns ending on City Park (the next space) | 3.8 % | 3.1 % |
| Casino use per game | 0.3 High/Low bets | 5.6 Dice Poker hands (0.4 with the player's own money) |
| Paid to players at the Casino per game | about $1 (High/Low; the coin flip evens out) | $242 |
| Duels / Showdowns per game | 0.0 | 2.2 (77 % folded) |
| Game length | 39.9 rounds | 39.9 rounds |
| Games with a bankruptcy | 27 % | 29 % |

Bots now stop on the Casino instead of nudging past it: it is worth about $39 to a Hard bot, City Park's token $25. Characters moved within normal run-to-run noise: Mogul 28.2 to 25.6 %, Hustler 31.9 to 31.5 %, Lawyer 24.4 to 26.5 %, Contractor 21.3 to 24.3 %, Politician 25.3 to 26.5 %, City Planner 22.2 to 19.7 %, Medic 22.6 to 21.9 %, Commuter 24.4 % both times. The Hustler stays just over the 30 % target, as before.

How the pay table was picked: worked out exactly (every keep for every roll). It returns 98.1 cents per $1 with perfect play, 95 cents for a player who also chases straights, 84 cents for one who always keeps their biggest group. With two rerolls it would pay $1.71 per $1, and one extra single-die reroll $1.52, so neither is safe as a Hustler perk. In a Showdown the player called out wins about 53 % of the time if they always play (they see the hand first); with folding, the caller only gains with three of a kind or better (about +$20 per Showdown against good play).

