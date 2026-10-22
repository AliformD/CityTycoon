# City Tycoon: Game Design & Build Specification

**Version:** 1.0 (handoff spec)
**Date:** 2026-10-19
**Owner:** Angel Leon
**Working title:** City Tycoon (placeholder; may change)

This file is the single source of truth for building City Tycoon, a virtual board game inspired by Monopoly. It has three parts:

1. **The rules**: everything about how the game plays (Sections 2–17).
2. **The technical design**: how the software should be structured (Sections 18–28).
3. **The build plan**: the phases to build it in, with "done" criteria (Section 29).

Every number in the rules is a starting value. All of them must be stored in a config file (Section 18), so they can be tuned without changing code.

---

## 0. Instructions for the AI building this

Read this section first.

### 0.1 About the project owner

- Angel is **learning C#**. He knows C# syntax at a beginner level and has no formal computer-science training.
- He **enjoys architecture**: how files and classes fit together, separation of concerns, SOLID principles, design patterns. He wants to understand **why** the code is structured the way it is, not just get working code.
- Explain things in **plain language**. Define a technical term the first time you use it, and don't assume he knows a concept just because the code uses it.
- His messages may contain typos. Read for intent, and ask if something is truly unclear.

### 0.2 How to work

- **Build in the phases from Section 29, in order.** Don't jump ahead. Each phase should end with something Angel can run and see.
- **Keep pull requests small.** Each PR description should include: *What I built*, *Why it's structured this way*, *How to run it*, *How to test it*.
- **Tests come with features.** Every rule gets at least one unit test. Use the scripted-dice helper (Section 26.2) so tests are predictable.
- **Never hard-code a tunable number.** If a number appears in this spec, it belongs in `rules.json` (Section 18).
- **Never hard-code board positions.** The board layout changes every game (Section 4.3). Look spaces up by ID.
- **Graphics are low priority.** Clean, readable, and functional beats pretty. Simple shapes and colors are fine.
- **When the spec is ambiguous or two rules conflict,** pick the simplest reasonable reading, write it down in `docs/DECISIONS.md` (a running list of decisions), and flag it to Angel. Don't silently invent big new mechanics.
- **Comment the code** where the "why" isn't obvious, written for a beginner reader.

---

## 1. Vision

### 1.1 The pitch

City Tycoon keeps what makes Monopoly fun: moving around the board, landing on other people's properties, building up, and cashing in when someone lands on your hotel. It fixes what makes Monopoly drag: feeling like you're just along for the ride, forced actions, stalling strategies, and runaway leaders.

### 1.2 Design pillars

| Pillar | What it means in the rules |
| --- | --- |
| **Luck creates surprise; players make choices** | Dice still matter, but Momentum tokens, held cards, auctions, loans and votes give you ways to respond. |
| **Interaction everywhere** | Auctions involve everyone. Cards target other players. City Council votes involve everyone. Some actions happen on other players' turns. |
| **No stalling, no lockouts** | Jail isn't a safe hiding spot. Houses are unlimited. Timed games end on a clock with a score. |
| **Every game is different** | Government tiles are hidden, random, and in different spots each game. Characters, zoning, and ordinances shift the strategy. |
| **Catch-up without punishment** | The player in last place sets the City Council agenda. Lobbying lets trailing players block the leader. (There is no GO hand-out for last place; see decision #143.) |

### 1.3 Platform goals

- **v1:** play on one computer against **AI (computer) players** and/or **other people on the same device** ("hot-seat", or pass-and-play).
- **Later:** **online multiplayer**, so Angel can play with friends and family in other places.
- **2 to 6 players** per game, in any mix of humans and AI.

---

## 2. Changes from the earlier draft

For reference, these are Angel's decisions that replaced parts of the first rulebook draft:

1. **Momentum Nudge is ±2.** One token moves you 1 or 2 spaces more or fewer (was ±1).
2. **Maximum 6 players.**
3. **8 playable characters**, each with one small unique ability (Section 5).
4. **No Casino betting limit.** You can bet any amount of cash you have.
5. **All properties are bought by auction** instead of at a set price. Each property keeps an *assessed value* used for rent, mortgages and scoring (Section 8).
6. **Government tiles are hidden** (face-down) until a player lands on one. Each side of the board has exactly one, in a random non-corner position that changes every game (Section 12).
7. **Hotels and skyscrapers are limited** by **zoning** and **permits**, which the players can change through **City Council votes** with paid lobbying and a random outcome (Sections 10–11).
8. **The card system must support adding many more cards later** (Section 21).
9. The old "Reserve a property" rule is replaced by **Down Payments** on auction wins (Section 8.5).

---

## 3. Components (digital equivalents)

| Component | Details |
| --- | --- |
| Board | 48 spaces: 4 corners + 4 sides × 11 spaces (10 fixed spaces + 1 Government tile per side). |
| Dice | 2 standard six-sided dice, plus a 3rd die for Precision Rolls. |
| Money | Starting cash $1,200 per player. The bank has unlimited money. |
| Properties | 28 total: 22 color properties in 8 color groups, 4 railroads, 2 utilities. |
| Buildings | Houses (unlimited), hotels and skyscrapers (limited by permits). |
| Government tiles | 7 types. 4 are used each game, one per side, face-down. |
| Characters | 8, each with a small unique ability. |
| Decks | Opportunity (kept in hand), Event (happens right away), Ordinance (City Council proposals). |
| Mall shop | A row of 3 face-up Opportunity cards for sale. |
| Tokens & markers | Momentum tokens, Loan cards, Down-payment (pending) markers, Insured markers. |

---

## 4. The board

### 4.1 Corners

Spaces are numbered clockwise from GO in the code's index order (GO = index 0).

| Corner | Board index | Effect |
| --- | --- | --- |
| GO | 0 | Passing or landing collects salary (Section 15.1). |
| Jail / Just Visiting | 12 | See Section 14. |
| City Park | 24 | Choose one: gain 1 Momentum token, or draw 1 Opportunity card. No money pot. |
| Go To Jail | 36 | Move to Jail. |

### 4.2 Districts and fixed spaces

Each side of the board is a **district**. Zoning (Section 10) is set per district. Every color group sits entirely inside one district.

Each district has these 10 fixed spaces in this order (from the corner before it, moving clockwise):

| # | District 1: **Old Town** (GO → Jail) | District 2: **Midtown** (Jail → City Park) | District 3: **Harbor** (City Park → Go To Jail) | District 4: **Uptown** (Go To Jail → GO) |
| --- | --- | --- | --- | --- |
| 1 | Mill Street (Brown) | Gallery Row (Pink) | Pier Road (Red) | Summit Avenue (Green) |
| 2 | Event | City Power & Light (Utility) | Opportunity | Grand Plaza (Green) |
| 3 | Cobble Lane (Brown) | Theater Way (Pink) | Anchor Street (Red) | Event |
| 4 | Income Tax ($200) | Studio Street (Pink) | Lighthouse Lane (Red) | Crown Street (Green) |
| 5 | Old Town Station (Railroad) | Midtown Station (Railroad) | Harbor Station (Railroad) | Uptown Station (Railroad) |
| 6 | Elm Avenue (Light Blue) | Market Street (Orange) | Harbor Boulevard (Yellow) | Opportunity |
| 7 | Opportunity | Event | Wharf Street (Yellow) | Skyline Drive (Dark Blue) |
| 8 | Maple Avenue (Light Blue) | Baker Street (Orange) | City Water Works (Utility) | Luxury Tax ($100) |
| 9 | Birch Avenue (Light Blue) | Fountain Square (Orange) | Marina Drive (Yellow) | Platinum Row (Dark Blue) |
| 10 | **Hospital** (landmark) | **Casino** (landmark) | **Mall** (landmark) | **Stadium** (landmark) |

All street names are placeholders.

### 4.3 Board generation (every new game)

1. Shuffle the 7 Government tile types and pick 4. The other 3 are not used this game, and nobody learns which ones they were.
2. Randomly assign one of the 4 picked tiles to each district.
3. For each district, insert its tile at a random position among that district's 10 fixed spaces. There are **11 possible positions**: before space 1, between any two spaces, or after space 10. The tile is **never** on a corner.
4. Place every Government tile **face-down** (hidden).
5. Build the final 48-space ring: GO, District 1 (11 spaces), Jail, District 2 (11), City Park, District 3 (11), Go To Jail, District 4 (11).

Because the tile position shifts the spaces after it, **the board index of every space can differ between games.** Code must always find spaces by ID, never by a fixed index. Only the corners have fixed indexes (0, 12, 24, 36).

### 4.4 Screen layout

The board is drawn as the outer ring of a 13×13 grid (13 × 4 − 4 = 48 cells), with play going clockwise around the screen like classic Monopoly:

- Index 0 (GO) is at the bottom-right corner, grid (12, 12).
- Indexes 1–11 run left along the bottom row (x = 11 → 1, y = 12).
- Index 12 (Jail) is at the bottom-left, (0, 12).
- Indexes 13–23 run up the left column (x = 0, y = 11 → 1).
- Index 24 (City Park) is at the top-left, (0, 0).
- Indexes 25–35 run right along the top row (x = 1 → 11, y = 0).
- Index 36 (Go To Jail) is at the top-right, (12, 0).
- Indexes 37–47 run down the right column (x = 12, y = 1 → 11).

The 11×11 middle area holds the dice, the current decision panel, and the game log.

---

## 5. Characters

Each player picks a **different** character. Each one has a single small ability.

| Character | Ability | Design note |
| --- | --- | --- |
| **The Mogul** | In silent (sealed-bid) auctions, your bid counts as $20 higher than it is when bids are compared. You still pay your real bid. You win ties and any silent auction where you're within $20 of the top bid. You also win dice ties. | Small edge in the main buying system. |
| **The Hustler** | Once per Casino visit, you may reroll your dice (High/Low) or your duel roll (Dice Duel). Once per lap, you may reroll your dice when you roll to move (not a Precision Roll). Your Stadium pick triples rent instead of doubling it. | For risk-takers. |
| **The Commuter** | Start the game with 3 Momentum tokens instead of 2. Your token limit is 4 instead of 2, and you can Nudge up to 4 spaces instead of 2. | More control over movement. |
| **The Lawyer** | You never pay Jail bail (leaving early is free). When you're sent to Jail, you still collect half the GO salary. You can't be targeted by Audit. | Jail still halves your rent. |
| **The Contractor** | Your building costs are 25% lower. | Stacks with other discounts (Section 9.4). |
| **The Politician** | Your City Council vote is worth 250 influence instead of 100, and each lobbying dollar adds 2 influence instead of 1. | Stronger voice in zoning. |
| **The City Planner** | At game start, secretly look at 1 hidden Government tile of your choice. Each time you pass GO, secretly look at 1 more that is still hidden. Once per game, after a City Council roll, you may reverse the result. | Information advantage. |
| **The Medic** | If you're sent to the Hospital, you pay no bill and roll normally on your next turn. Insured markers are free for you. | Shrugs off bad luck. |

**Selection:** turn order is decided randomly first. Then players pick characters in **reverse** turn order (the last player picks first), so the first player's advantage is balanced by picking last. AI players pick randomly from what's left. There's also a "Random characters" setup option.

Every character ability goes through the rule-modifier system (Section 20) so abilities are easy to add or change.

---

## 6. Setup

1. Choose 2–6 players. Each one is Human or AI (Easy / Normal / Hard), with a name.
2. Choose a game mode: **Classic** or **Timed** (default 40 rounds). See Section 16.
3. Choose a privacy mode: **Hidden** or **Open** (Section 25).
4. Optionally enter a game seed (for replays and testing). Otherwise a random seed is used.
5. Decide turn order randomly.
6. Pick characters (Section 5).
7. Generate the board (Section 4.3).
8. **Starting zoning:** every district starts at **Residential**, except one randomly chosen district that starts at **Commercial** (Section 10).
9. **Permits:** set the hotel and skyscraper permit supply for the player count (Section 10.3).
10. Shuffle the Opportunity, Event and Ordinance decks. Deal 3 Opportunity cards face up to the Mall shop.
11. Each player gets $1,200 and 2 Momentum tokens (the Commuter gets 3).
12. The City Planner takes their first secret peek.
13. The first player starts round 1.

---

## 7. Turns, rounds, and movement

### 7.1 Rounds

A **round** ends when every remaining player has taken a turn. Round-based effects (ordinance durations, City Council sessions, Timed-mode endings) are checked at the end of each round.

### 7.2 Turn sequence

1. **Start of turn**
   - Apply start-of-turn effects in this order: Green perk (Dividends), then the Orange perk charge (if you're in Jail or recovering from the Hospital and an opponent owns all of Orange, pay them $25).
   - If you're in Jail, follow Section 14 instead of the normal pre-roll phase.
2. **Pre-roll phase.** Any of these, in any order:
   - Build, sell buildings, mortgage or unmortgage.
   - Propose trades.
   - Take or repay loans.
   - Play Opportunity cards that are played on your own turn.
   Then choose how to move:
   - **Normal roll** (2 dice), or
   - **Precision Roll** (Section 7.4), or
   - **Express Line** (Section 13.2, needs 2 or more railroads), or
   - **Shortcut card** (Section 15.4).
3. **Roll.**
4. **Nudge decision** (Section 7.3).
5. **Detour window:** any *other* player holding a Detour card may play it now (Section 15.4).
6. **Move** one space at a time. Passing GO triggers GO effects as you pass.
7. **Resolve the space** you land on.
8. **Post-move phase:** the same options as the pre-roll phase, except moving.
9. **Doubles:** if you rolled doubles (and aren't in Jail), take another turn from step 2. Three doubles in a row sends you to Jail instead of moving.
10. **End of turn:** if you hold more Opportunity cards than your hand limit, discard down to the limit. If you were the last player in turn order, run the **end-of-round checks**:
    1. Count down ordinance durations and end any that expire.
    2. Hold a City Council session if one is due (Section 11.1).
    3. Check whether the Timed-mode limit is reached (Section 16).

### 7.3 Momentum tokens

Momentum tokens let you steer your movement.

- **Limit:** you can hold up to 2 (the Commuter 4). The Light Blue perk adds 1 more.
- **Gaining tokens:**
  - Start with 2 (the Commuter 3).
  - Passing GO gives no tokens (only the Light Blue perk does).
  - Gain 1 from City Park (if chosen) and the Fire Station, 2 from the Fast Track card, 2 from the Momentum Surge event (everyone else 1), and 1 from the Toll Collector railroad bonus.
- **Detour beats a Nudge:** a Detour card played on you cancels your Nudge (the tokens stay spent) and moves you 1 space forward or back from what you rolled.
- **Nudge:** after you roll, spend **1 token for each space** you move more or fewer than you rolled: 1 token for ±1, 2 tokens for ±2.
  - Only one Nudge per roll, so the most you can shift is ±2 (the Commuter ±4, spending 4 tokens).
  - Your total movement must still be at least 1 space forward.
  - Nudging never changes whether you rolled doubles.
- **Precision Roll:** before you roll, spend **2 tokens** (1 with all 4 railroads) to roll 3 dice and pick which 2 to use. Doubles count only if the 2 dice you pick match.
- **You can't spend tokens** while in Jail.
- **One-die turns:** if you must roll only 1 die (Hospital recovery or Road Work), you can't Precision Roll, but you can still Nudge.

### 7.4 Moving backwards

Backward movement (a backward Nudge or Rush Hour) never collects GO when it crosses GO.

---

## 8. Getting property: auctions

There are **no fixed purchase prices.** Every property has an **assessed value** (Section 8.7), which sets its rent, mortgage value, and minimum bid, and counts toward net worth.

### 8.1 When an auction happens

- A player lands on an **unowned** property (by dice, card, or any other movement).
- A **forced auction** is started by the Red perk (Hostile Takeover) or the Hostile Bid card.
- A property is **forfeited** (unpaid down payment) or goes back to the bank (bankruptcy to the bank).

### 8.2 Auction format: sealed bids

Sealed bids are used because they work the same way for humans on one screen, AI players, and online players.

1. Every player who isn't bankrupt may bid, **including players in Jail.**
2. Each bidder secretly picks either:
   - **Pass**, or
   - **A bid** of at least the minimum bid, in steps of $10, plus a payment choice: **Pay in full** or **Down payment** (Section 8.5).
3. All bids are revealed at the same moment.
4. The highest bid wins. The Mogul's bid counts $20 higher when bids are compared.
5. **Ties:** the Mogul wins first, then the player who landed there, then the next player in turn order after the lander.
6. The winner pays **their own bid** to the bank (or to the owner, in forced auctions).
7. **If nobody bids,** the property stays unowned. The next player to land there starts a new auction.

### 8.3 Minimum bids

| Auction type | Minimum bid |
| --- | --- |
| Normal (unowned property) | 50% of assessed value, rounded up to $10 |
| Forced (Hostile Takeover / Hostile Bid) | 100% of assessed value |
| Re-auction after a forfeit | 50% of assessed value |

### 8.4 How much you can bid

- **Pay in full:** your bid can't be more than your current cash.
- **Down payment:** 20% of your bid (rounded up to $10) can't be more than your cash, and you must have a free pending slot (Section 8.5).
- You may take a loan while an auction is open, so you can bid more (Section 15.7).

### 8.5 Down payments (pending properties)

You can win an auction even when you're short on cash.

- Pay **20%** of the winning bid now. The property becomes **pending** for you.
- The **balance** is due the next time you pass or land on GO. It's paid automatically from your cash right after you collect salary.
- If you can't pay it then, you may raise money (loans, mortgages, selling buildings). If you still can't, the property is **forfeited**: you lose the deposit, and the property is re-auctioned right away. You can't bid in that re-auction.
- **While a property is pending:**
  - It collects **no rent**.
  - It can't be built on, mortgaged, or traded.
  - It **does not count** toward completing a color group, a railroad bonus, or a utility bonus.
- You can hold at most **2 pending properties** at a time.

### 8.6 Forced auctions

The Hostile Takeover perk and the Hostile Bid card can force an auction on an opponent's property, with these limits:

- The property must have no buildings and must not be part of a full color group the owner holds.
- The property can't be next to a revealed Police Station.
- The owner may bid too. If the owner wins, they pay the bank. Otherwise the winner pays the owner.
- If nobody bids (including the owner), the owner keeps the property.

### 8.7 Assessed values and rents

The values below come from classic Monopoly's price and rent structure. "Full-set rent" doubles the base rent while you own the whole group and none of it is mortgaged or pending. Levels 1–4 are houses, 5 is a hotel, and 6 is a skyscraper.

| Property | Group | Assessed value | Base rent | 1 house | 2 houses | 3 houses | 4 houses | Hotel | Skyscraper | Cost per house level |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Mill Street | Brown | $60 | $2 | $10 | $30 | $90 | $160 | $250 | $375 | $50 |
| Cobble Lane | Brown | $60 | $4 | $20 | $60 | $180 | $320 | $450 | $675 | $50 |
| Elm Avenue | Light Blue | $100 | $6 | $30 | $90 | $270 | $400 | $550 | $825 | $50 |
| Maple Avenue | Light Blue | $100 | $6 | $30 | $90 | $270 | $400 | $550 | $825 | $50 |
| Birch Avenue | Light Blue | $120 | $8 | $40 | $100 | $300 | $450 | $600 | $900 | $50 |
| Gallery Row | Pink | $140 | $10 | $50 | $150 | $450 | $625 | $750 | $1,125 | $100 |
| Theater Way | Pink | $140 | $10 | $50 | $150 | $450 | $625 | $750 | $1,125 | $100 |
| Studio Street | Pink | $160 | $12 | $60 | $180 | $500 | $700 | $900 | $1,350 | $100 |
| Market Street | Orange | $180 | $14 | $70 | $200 | $550 | $750 | $950 | $1,425 | $100 |
| Baker Street | Orange | $180 | $14 | $70 | $200 | $550 | $750 | $950 | $1,425 | $100 |
| Fountain Square | Orange | $200 | $16 | $80 | $220 | $600 | $800 | $1,000 | $1,500 | $100 |
| Pier Road | Red | $220 | $18 | $90 | $250 | $700 | $875 | $1,050 | $1,575 | $150 |
| Anchor Street | Red | $220 | $18 | $90 | $250 | $700 | $875 | $1,050 | $1,575 | $150 |
| Lighthouse Lane | Red | $240 | $20 | $100 | $300 | $750 | $925 | $1,100 | $1,650 | $150 |
| Harbor Boulevard | Yellow | $260 | $22 | $110 | $330 | $800 | $975 | $1,150 | $1,725 | $150 |
| Wharf Street | Yellow | $260 | $22 | $110 | $330 | $800 | $975 | $1,150 | $1,725 | $150 |
| Marina Drive | Yellow | $280 | $24 | $120 | $360 | $850 | $1,025 | $1,200 | $1,800 | $150 |
| Summit Avenue | Green | $300 | $26 | $130 | $390 | $900 | $1,100 | $1,275 | $1,925 | $200 |
| Grand Plaza | Green | $300 | $26 | $130 | $390 | $900 | $1,100 | $1,275 | $1,925 | $200 |
| Crown Street | Green | $320 | $28 | $150 | $450 | $1,000 | $1,200 | $1,400 | $2,100 | $200 |
| Skyline Drive | Dark Blue | $350 | $35 | $175 | $500 | $1,100 | $1,300 | $1,500 | $2,250 | $200 |
| Platinum Row | Dark Blue | $400 | $50 | $200 | $600 | $1,400 | $1,700 | $2,000 | $3,000 | $200 |

Notes:

- **Skyscraper rent** is 1.5 × hotel rent, rounded up to the nearest $25.
- **Railroads:** assessed value $200 each. **Utilities:** assessed value $150 each.
- **Mortgage value** is 50% of assessed value. Unmortgaging costs the mortgage value plus 10%.

---

## 9. Building

### 9.1 Building levels

| Level | Building | Cost of this level |
| --- | --- | --- |
| 1–4 | 1 to 4 houses | 1 × the group's house cost, each |
| 5 | Hotel | 1 × house cost, and uses 1 hotel permit |
| 6 | Skyscraper | 3 × house cost, uses 1 skyscraper permit, and returns the hotel permit |

### 9.2 Requirements to build

You can build on a property only when **all** of these are true:

1. You own the **entire color group**, and none of it is mortgaged or pending.
2. It's **your turn** (pre-roll or post-move phase) and you're **not in Jail**.
3. You follow the **even-build rule**: no property in the group can be more than 1 level ahead of another in the group.
4. The new level is allowed by the district's **zoning** (Section 10).
5. A **permit** is available if the new level is a hotel or skyscraper (Section 10.3).
6. You haven't hit the **build limit**: at most **3 levels per turn** across all your properties (the Construction Permit card adds 2 more).
7. No **Building Moratorium** ordinance is active.
8. **Skyscrapers only:** every property in the group already has a hotel.

### 9.3 Selling buildings

- Sell one level at a time, back to the bank, for **half** of what that level cost.
- Selling must also follow the even-build rule.
- Selling a hotel or skyscraper returns its permit to the supply. Selling a skyscraper back down to a hotel takes a hotel permit back. If no hotel permit is free, the building drops straight to 4 houses (you get half of both levels' costs).
- Selling is allowed any time you need to raise money, even when it isn't your turn.

### 9.4 Building discounts

- Discounts come from the Contractor (10%), a School next door (25%), a Power Plant next door (50%), and the Construction Permit card (25%).
- **Add the discounts together, up to a maximum of 75% off.** Round the final cost down to the nearest $5.
- **Dark Blue perk:** skyscraper levels cost 1 × house cost (instead of 3 ×), before any discounts.

---

## 10. Zoning and permits

### 10.1 Zoning levels

Each district has its own zoning level, which caps how high buildings can go there.

| Zoning level | Highest building allowed |
| --- | --- |
| **Restricted** | 2 houses |
| **Residential** | 4 houses |
| **Commercial** | Hotels |
| **High-Rise** | Skyscrapers |

- **Start of game:** every district is Residential, except one random district that starts Commercial.
- Zoning only changes through ordinances (Section 11).

### 10.2 Grandfathering

If a district is **downzoned** below the buildings already there, those buildings **stay** and keep earning rent. You can't add new levels above the new cap. If you sell a building down, you can't rebuild it above the cap.

### 10.3 Permits

Hotels and skyscrapers each need a permit from a citywide supply. Houses are unlimited.

| Players | Hotel permits | Skyscraper permits |
| --- | --- | --- |
| 2–3 | 8 | 2 |
| 4 | 10 | 3 |
| 5–6 | 12 | 4 |

- When the supply runs out, nobody can build that type until a permit is returned (by selling, bankruptcy, Fire!, and so on) or an ordinance adds more.
- The current permit counts are always shown on screen.

---

## 11. City Council and ordinances

The City Council is how players change zoning and city rules. Money can influence the vote, but the outcome is always partly random. Rich players can push the city their way, and trailing players can pool money to block them.

### 11.1 When sessions happen

| Session type | When | Who sets the agenda |
| --- | --- | --- |
| **Regular session** | At the end of every 4th round (rounds 4, 8, 12, …) | The player with the **lowest net worth**. Ties go to whoever is next in turn order after the current player. |
| **Emergency session** | When someone lands on a revealed City Hall tile, or draws the Emergency Session Event card | The player who triggered it |

### 11.2 Setting the agenda

1. Draw 2 Ordinance cards.
2. The agenda setter picks 1 to put to a vote. The other goes to the bottom of the deck.
3. If the chosen card needs a target district, the agenda setter picks it.
4. If a card can't do anything right now (for example, Rezone Up on a district that's already High-Rise), it can't be picked. If neither card can be picked, draw again.

### 11.3 Voting and lobbying

Every player who isn't bankrupt takes part, including players in Jail.

1. Each player **secretly** chooses:
   - A vote: **Yes**, **No**, or **Abstain**.
   - A lobbying amount: any amount of their cash, from $0 up. Players who abstain can't lobby.
2. All votes and lobbying amounts are revealed together.
3. **Influence** is counted for each side:
   - Each Yes or No vote adds **100 influence** to its side (the Politician adds 250).
   - Each lobbying dollar adds **1 influence** to the side that player voted for (2 for the Politician).
   - **Public opinion** always adds 100 to Yes and 100 to No, so nothing is ever certain.
   - The Lobbyist card adds 200 free influence to the player's side.
4. **Chance of passing** = Yes influence ÷ (Yes influence + No influence). The screen shows this as a percentage before the roll.
5. A random roll decides the outcome using that chance.
6. **All lobbying money goes to the bank, whether the ordinance passes or fails.**
7. **The City Planner** may then reverse the result, once per game: a pass becomes a fail, or a fail becomes a pass.
8. A passed ordinance takes effect immediately. If it has a duration, it lasts that many rounds and ends during the end-of-round checks.

**Example:** Ana votes Yes and lobbies $300. Ben votes No and lobbies $100. Cal abstains.
Yes = 100 (vote) + 300 (lobbying) + 100 (public) = 500. No = 100 (vote) + 100 (lobbying) + 100 (public) = 300.
Chance of passing = 500 ÷ 800 = 62.5%.

### 11.4 Ordinance deck (17 cards)

| Ordinance | Copies | Effect | Duration |
| --- | --- | --- | --- |
| Rezone Up | 3 | The chosen district goes up one zoning level. | Permanent |
| Downzone | 2 | The chosen district goes down one zoning level (grandfathering applies). | Permanent |
| Hotel Permit Expansion | 1 | Add 4 hotel permits to the supply. | Permanent |
| Skyscraper Permit Expansion | 1 | Add 2 skyscraper permits to the supply. | Permanent |
| Building Moratorium | 1 | Nobody can build anywhere. | 2 rounds |
| Rent Control | 1 | Rents in the chosen district are −25%. | 3 rounds |
| Tourism Boost | 1 | Rents in the chosen district are +25%. | 3 rounds |
| Transit Investment | 1 | Railroad rents are +50%. | 3 rounds |
| Property Tax Hike | 1 | Every player pays $10 per building level they own, right away. | Once |
| Infrastructure Bond | 1 | Every player pays $50. The lowest-zoned district goes up one level (the agenda setter breaks ties). | Once |
| Stimulus Check | 1 | Every player collects $100. | Once |
| Historic Preservation | 1 | The chosen district's zoning can never change again. | Permanent |
| Casino Regulation | 1 | Casino bets and Dice Duel stakes are capped at $200. | 4 rounds |
| Public Works | 1 | Every hidden Government tile is revealed. | Once |

---

## 12. Government tiles

Government tiles are city-owned spaces that affect the properties right next to them. They're hidden at the start, so part of the strategy is guessing which properties will turn out good or bad.

### 12.1 Hidden tiles

- Every tile starts **face-down.** Everyone can see *where* each tile is, but not what it is.
- A tile is **revealed** when a player **lands on it** by any movement, including cards. Passing over it does not reveal it. Rush Hour's backward move does not reveal it.
- **While a tile is hidden, it has no effects at all:** neither its neighbor effect nor its landing effect.
- When a tile is revealed, its landing effect applies to the player who landed on it, and its neighbor effect turns on right away for the rest of the game.
- Some effects let a player **peek** privately (the City Planner and the Inside Source card). Peeking doesn't reveal the tile to anyone else.

### 12.2 Neighbors

- A tile's **neighbors** are the space just before it and the space just after it.
- A neighbor effect only does something if that neighbor is a property (color property, railroad or utility). If the neighbor is a corner, tax space, card space or landmark, the effect does nothing there.
- With one tile per side, and corners between sides, no property can ever sit between two tiles.

### 12.3 The 7 tiles

| Tile | Neighbor effect (after reveal) | Landing effect |
| --- | --- | --- |
| **Police Station** | Neighbors can't be targeted by negative Opportunity cards (ones played against someone else) or Hostile Takeover. Helpful cards still work. | Pay a $25 parking ticket to the bank. |
| **Fire Station** | Neighbors are safe from the Fire! Event. | Gain 1 Momentum token. |
| **School** | Building on neighbors costs 25% less. | Draw 1 Opportunity card. |
| **City Hall** | Neighbor rent is +25%. | Pay $10 per building level you own, then start an Emergency Council session with you setting the agenda. |
| **Power Plant** | Neighbor rent is −25%, and building there costs 50% less. | Pay $50 to the player who owns both utilities, or to the bank if nobody does. |
| **Transit Hub** | When you land on a neighbor owned by someone else, you may pay $25 to the bank and hop onto the Transit Hub **instead of** paying rent. You don't get the Transit Hub's landing effect from hopping. | You may pay $50 to the bank to move to any railroad. Resolve that railroad normally: pay rent if owned, or start an auction if unowned. |
| **Landfill** | Neighbor rent is −50%. | Pay a $25 cleanup fee to the bank. |

---

## 13. Rent and set bonuses

### 13.1 How rent is calculated

Rent on a property is worked out in this order:

1. **Base rent:**
   - **Color property:** use the property's rent table at its building level. At level 0, double the base rent if the owner holds the whole group and none of it is mortgaged or pending.
   - **Railroad:** $25 / $50 / $100 / $200 depending on how many railroads the owner has (pending ones don't count).
   - **Utility:** 4 × the dice total with one utility, 10 × with both. If you got there without rolling (by a card, for example), roll 2 dice to work out the rent.
2. **Percentage modifiers:** add together every percentage that applies (Government tile neighbors, Rent Control, Tourism Boost, Transit Investment). The total is kept between −75% and +100%.
3. **Multipliers:** apply each one that's active: the Stadium (×2) and Grand Opening (×2). Both together make ×4.
4. **Owner in Jail:** ×0.5.
5. **Rent Strike** played by the payer: ×0.5.
6. **Round down** to the whole dollar. If the base rent was above $0, the minimum is $1.

**Other rules:**

- Mortgaged and pending properties collect no rent.
- **Dark Blue perk:** Dark Blue properties ignore **every negative modifier** in steps 2 and 5 (bad tiles, Rent Control, Rent Strike). They are still halved when the owner is in Jail (step 4).

### 13.2 Set bonuses (color perks)

A perk stays on as long as you own the whole group with none of it mortgaged or pending.

| Group | Perk | Effect |
| --- | --- | --- |
| Brown | **Community Roots** | Collect an extra $50 every time you pass GO. |
| Light Blue | **Commuter Belt** | Gain 1 Momentum token when you pass GO. Your token limit goes up by 1. |
| Pink | **Arts District** | When you draw an Opportunity card, draw 2 and keep 1. Your hand limit becomes 4. |
| Orange | **Neighborhood Watch** | Any opponent in Jail or recovering from the Hospital pays you $25 at the start of each of their turns there. |
| Red | **Hostile Takeover** | Once per lap (resets when you pass GO), force an auction on one eligible opponent property (Section 8.6). |
| Yellow | **Bank Partner** | Your loans cost $210 to repay instead of $250, and you can hold 3 instead of 2. |
| Green | **Dividends** | At the start of each of your turns, collect $20 for every hotel or skyscraper you own anywhere. |
| Dark Blue | **Prime Real Estate** | Immune to negative rent modifiers (Section 13.1). Skyscraper levels cost 1 × house cost. |

### 13.3 Railroad bonuses (they stack)

| Railroads owned | Bonus |
| --- | --- |
| 2 | **Express Line:** instead of rolling, pay $50 to the bank and move to any railroad you own. Then roll 1 die and move that many spaces. You may Nudge. Express Line never gives an extra turn. |
| 3 | **Toll Collector:** gain 1 Momentum token every time someone pays you railroad rent. |
| 4 | **Rail Baron:** your Precision Rolls cost 1 token instead of 2. |

### 13.4 Utility bonuses (need both utilities)

- **Utility Bill:** every time an opponent passes GO, they pay you $20 out of their salary.
- **Power Grid:** every time an opponent builds a level, they pay you $10.

---

## 14. Jail

Jail is short, and it's no longer a safe place to hide late in the game.

### 14.1 Going to Jail

You go to Jail when you land on Go To Jail, roll doubles 3 times in a row, or draw the Go to Jail Event card. Move straight there without passing GO, and your turn ends. The Lawyer still collects half the GO salary on the way in.

### 14.2 While you're in Jail

- **Frozen assets:** your properties collect only **half rent** (Section 13.1, step 4).
- You can't build, and you can't spend Momentum tokens.
- You **can** trade, take or repay loans, mortgage, bid in auctions, vote, lobby, and play Reaction cards.
- If an opponent owns all of Orange, you pay them $25 at the start of each of your Jail turns.

### 14.3 Getting out

You can stay at most 2 turns. At the start of a Jail turn, choose one:

1. **Pay $50 bail** (the Lawyer pays $0), then roll and move normally. Doubles give an extra turn as usual.
2. **Use a Get Out of Jail Free card**, then roll and move normally.
3. **Try for doubles:** if you roll doubles, move by that roll, but you **don't** get an extra turn.
4. If you fail on your **2nd** Jail turn, you must pay $50 (the Lawyer $0) and move by that roll.

**Just Visiting:** landing on the Jail space by a normal move does nothing.

---

## 15. Other spaces, cards, and money

### 15.1 GO

Passing or landing on GO triggers these, in this order:

1. Collect **$300** salary.
2. **Utility Bill:** if an opponent owns both utilities, pay them $20.
3. **Brown perk:** +$50.
4. **Catch-up bonus:** off by default (`catchUpBonus` is 0). As a house rule, the player with the lowest net worth (ties all count) gets that much.
5. **Light Blue perk:** gain 1 Momentum token. (GO itself gives no tokens.)
6. **Down payments due:** pay off any pending balances (Section 8.5).
7. **City Planner:** peek at 1 still-hidden Government tile.
8. **Red perk:** your Hostile Takeover becomes available again.

### 15.2 Tax spaces

- **Income Tax:** pay $200 to the bank.
- **Luxury Tax:** pay $100 to the bank.

### 15.3 Landmarks

Nobody owns landmarks. Each one gives the player who lands there a choice.

**Hospital (end of Old Town)**

- **If an Event sends you here:** move straight here without passing GO, and pay a **$100 medical bill**. On your next turn you roll **only 1 die** while you recover.
  - If you have an **Insured** marker, the bill is $0, you roll normally next turn, and the marker is used up.
  - The **Medic** pays nothing and rolls normally.
- **If you just land here:** you're visiting. You may buy an **Insured** marker for $50 (free for the Medic). You can hold only 1.
- An Insured marker protects you from **one** Hospital bill **or** one Fire! Event, whichever happens first.

**Casino (end of Midtown)**

When you land here, you may make **one** bet. There's **no maximum**: you can bet any amount up to your cash (unless the Casino Regulation ordinance is active).

- **High / Low:** call High (8–12) or Low (2–6), then roll 2 dice.
  - If you're right, you win your bet.
  - If you roll a 7 or call it wrong, you lose your bet.
  - If you're right **and** rolled doubles, you win 2 × your bet.
  - The Hustler may reroll once.
  - Over many bets, this game loses about 8% of the amount bet, so it's a real risk.
- **Dice Duel:** challenge another player. Both put in the same amount, up to whichever player has less cash. Each rolls 2 dice, and the higher total takes everything. Reroll ties. The challenged player may say no. The Hustler may reroll their own roll once.

**Mall (end of Harbor)**

The Mall shop is 3 face-up Opportunity cards. When you land here, you may do one of these:

- **Buy** one card. Prices are $50, $100 and $150 from left to right. Slide the remaining cards left and refill the right slot from the deck.
- **Sell** one card from your hand to the bank for $50. The sold card goes to the discard pile.

**Stadium (end of Uptown)**

Choose any one color group. Its rent is ×2 (×3 if the Hustler picks it) until the start of your next turn. You can pick your own group, or someone else's as part of a deal.

**City Park (corner)**

Choose one: gain 1 Momentum token, or draw 1 Opportunity card.

### 15.4 Opportunity cards (kept in hand)

- You draw one when you land on an Opportunity space, from City Park, from the School, and from some other effects.
- **Hand limit:** 3 cards (4 with the Pink perk). At the end of your turn, discard down to your limit.
- Play cards on your **own turn**, except **Reaction** cards, which you play when their trigger happens.
- Players can see how many cards each player holds, but not which ones.

| Card | Copies | When you play it | Effect |
| --- | --- | --- | --- |
| Rent Strike | 2 | Reaction: when you owe rent | Pay half of that rent. |
| Insurance | 2 | Reaction: when a card is played against you | Cancel that card. Insurance can't be cancelled. |
| Detour | 2 | Reaction: after another player rolls and nudges, before they move | Cancel their Nudge (the token stays spent) and move their piece 1 space forward or back from what they rolled. |
| Shortcut | 2 | Your turn, instead of rolling | Move to any space up to 6 spaces ahead. GO counts if you pass it. It's never doubles. |
| Hostile Bid | 1 | Your turn | Force an auction on one eligible opponent property (Section 8.6). |
| Audit | 1 | Your turn | Pick a player. They pay 10% of their cash to the bank, rounded down. The Lawyer is immune. |
| Construction Permit | 1 | Your turn | This turn, build up to 2 levels past your build limit, at 25% off. |
| Get Out of Jail Free | 1 | Jail, or the start of a Hospital recovery turn | Leave Jail for free, or skip the 1-die recovery turn. |
| Dice Duel | 1 | Your turn | Challenge any player to a Dice Duel from anywhere on the board. |
| Tip-Off | 1 | Your turn | Look at the top 3 Event cards and put them back in any order. |
| Fast Track | 1 | Your turn | Gain 2 Momentum tokens. |
| Grand Opening | 1 | Your turn | Pick one of your properties. Its rent is ×2 until your next turn. |
| Inside Source | 1 | Your turn | Secretly peek at 1 hidden Government tile. |
| Lobbyist | 1 | During a Council vote, before the reveal | Add 200 free influence to your side. |

**18 cards total.** The deck must support adding many more later (Section 21).

### 15.5 Event cards (happen right away)

You draw one when you land on an Event space.

| Card | Copies | Effect |
| --- | --- | --- |
| Sprained Ankle | 2 | Go to the Hospital (Section 15.3). |
| Fire! | 1 | Your most-built property (you choose if tied) loses 1 level, unless it's next to a revealed Fire Station or you use your Insured marker. If you have no buildings, nothing happens. |
| Recession | 1 | Every player pays 10% of their cash to the bank, except whoever has the least cash (ties are all exempt). |
| Housing Boom | 1 | Every player collects $50 for each hotel or skyscraper they own. |
| City Festival | 1 | Everyone collects $50. You collect $100 instead. |
| Block Party | 1 | Collect $25 from every other player. |
| Lottery | 1 | Everyone who can puts in $20 and rolls 1 die. The highest roll takes the pot. Reroll ties. |
| Rush Hour | 1 | Every player moves back 2 spaces. Nobody resolves the space they land on, and nobody collects GO. |
| Road Work | 1 | Next turn you roll only 1 die, unless you spend 1 Momentum token. |
| Momentum Surge | 1 | Everyone gains 1 Momentum token. You gain 2. |
| Property Reassessment | 1 | Pay $25 per house and $100 per hotel or skyscraper you own. |
| Tax Refund | 1 | Collect $100. |
| Inheritance | 1 | Collect $150. |
| Advance to GO | 1 | Move to GO and collect GO effects. |
| Go to Jail | 1 | Go straight to Jail. |
| Emergency Session | 1 | Start an Emergency Council session with you setting the agenda. |

**17 cards total.**

### 15.6 Decks running out

A used Opportunity or Event card goes to the bottom of its deck, so the decks are shuffled only once, at the start, and keep their order all game. Players who remember what was drawn can guess what comes next (decision #146). Cards held in players' hands or in the Mall shop stay where they are. (City Council ordinances still use a discard pile that is shuffled when the deck runs low.)

### 15.7 Loans

- **When:** any time on your turn, or whenever you need to pay, bid, or lobby.
- Each loan gives you **$200** and costs **$250** to repay (Yellow perk: $210).
- You can hold **2** at a time (Yellow perk: 3).
- Repay any time on your turn.
- In scoring, each unpaid loan counts as **−$250** (−$210 with the Yellow perk).
- In bankruptcy, the bank's loans are paid back first.

### 15.8 Mortgages

- Mortgage an unbuilt property for 50% of its assessed value. A property can't be mortgaged while its group has any buildings.
- Unmortgaging costs the mortgage value plus 10%.
- Mortgaged properties collect no rent, and they break the full-set bonus and the perks.

### 15.9 Trading

- Any player can propose a trade at any time the game is waiting on a normal turn action (not in the middle of resolving something).
- **Can be traded:** cash, unbuilt properties (the whole group must have no buildings), Opportunity cards, Momentum tokens, and Get Out of Jail Free cards.
- **Can't be traded:** pending properties, loans, Insured markers, and buildings.
- A mortgaged property transfers still mortgaged. The receiver pays the bank 10% of the mortgage value right away.
- The player asked can accept, decline, or send back a **counter-offer** with different terms. A trade can have up to 3 counter-offers, then it must be accepted or declined.
- Opportunity cards: you can offer your own. Other hands are secret, so you can only ask for cards the other player offered first (in a counter-offer).

### 15.10 Bankruptcy

You're bankrupt if you owe more than you can raise by selling every building, mortgaging everything, and taking every loan you can.

- **If you owe another player:** first repay your loans to the bank from your cash. Then that player receives your remaining cash, properties (still mortgaged where they were), Opportunity cards, and Momentum tokens. Your buildings are sold to the bank first.
- **If you owe the bank:** your buildings are removed, and each property is auctioned (minimum bid 50% of assessed value).
- Bankrupt players are out of the game. Their permits return to the supply.

---

## 16. Game modes, catch-up, and scoring

### 16.1 Modes

| Mode | How it ends |
| --- | --- |
| **Classic** | Play until only one player isn't bankrupt. |
| **Timed** (recommended, default) | Play a set number of rounds (default 40, adjustable when you start a game; 30 to 45 suits a 90-minute game). After the last round's end-of-round checks, the game is scored. If only one player is left before that, they win. |

### 16.2 Catch-up mechanics

- No cash hand-out: the GO catch-up bonus is 0 by default (decision #143). You get the bonuses you earn.
- The player with the lowest net worth **sets the agenda** at regular Council sessions.
- Lobbying lets trailing players team up to block the leader's ordinances.

### 16.3 Net worth

Net worth is **public** and shown live for every player. It's calculated as:

- Cash
- \+ the assessed value of each property you own (half if mortgaged; for pending properties, assessed value minus the unpaid balance)
- \+ the full cost of every building level you currently own
- \+ $50 for each Opportunity card in your hand
- − $250 for each unpaid loan (−$210 with the Yellow perk)

**Winner:** the highest net worth wins. If tied, the player with more cash wins. If still tied, they share the win.

The end screen shows each player's net-worth breakdown.

---

## 17. Rule timing and edge cases

These rulings settle common conflicts. Add new ones to `docs/DECISIONS.md` as they come up.

1. **Effect order:** when several effects trigger at once, resolve the active player's effects first, then go around in turn order.
2. **Reactions:** a Reaction window is offered **only** to players who hold a matching card. Online, the window has a time limit. Players can turn on an "auto-pass reactions" setting.
3. **Detour timing:** the roll happens, then the mover's Nudge, then the Detour window, then movement. A Detour cancels the Nudge (the token stays spent) and moves the piece 1 space from the dice roll. Insurance can cancel a Detour.
4. **Hidden tiles:** neighbor effects only count once the tile is revealed. Landing on a tile reveals it and applies its landing effect in the same step.
5. **Transit Hub:** hopping onto the Hub from a neighbor doesn't apply the Hub's landing effect, so you can't chain moves.
6. **Shortcut, Express Line and card moves** all count as landing: they reveal tiles, start auctions, and charge rent.
7. **Doubles:** only the original 2-dice roll (or the 2 dice chosen in a Precision Roll) counts. Nudge and Detour don't affect doubles.
8. **Cash can't go negative.** Any payment you can't make starts the raise-money / bankruptcy flow.
9. **Lobbying and bids are paid in cash.** You may take loans before you submit.
10. **When net worth is checked** for the catch-up bonus and the agenda setter, it's calculated at that exact moment.
11. **Ordinance durations** count down during the end-of-round checks. "3 rounds" means it ends at the end of the 3rd round after it passes.
12. **Mogul +$20:** only used when comparing bids. Payment is always the real bid.

---

## 18. Tunable rules config (`rules.json`)

Every value below must live in one config file that loads at startup. The rules engine reads all numbers from it. The simulation tool (Section 26.3) uses it to test balance changes.

| Key | Default | Section |
| --- | --- | --- |
| `startingCash` | 1200 | 6 |
| `startingMomentum` | 2 | 7.3 |
| `momentumCap` | 2 | 7.3 |
| `nudgeMaxSpaces` | 2 | 7.3 |
| `nudgeCost` | 1 | 7.3 |
| `precisionRollCost` | 2 | 7.3 |
| `goSalary` | 300 | 15.1 |
| `goMomentumGain` | 0 | 15.1 |
| `catchUpBonus` | 0 | 16.2 |
| `incomeTax` / `luxuryTax` | 200 / 100 | 15.2 |
| `auctionMinBidPercent` | 50 | 8.3 |
| `forcedAuctionMinBidPercent` | 100 | 8.3 |
| `bidIncrement` | 10 | 8.2 |
| `downPaymentPercent` | 20 | 8.5 |
| `maxPendingProperties` | 2 | 8.5 |
| `mortgagePercent` / `unmortgageFeePercent` | 50 / 10 | 8.7 |
| `buildLimitPerTurn` | 3 | 9.2 |
| `skyscraperCostMultiplier` | 3 | 9.1 |
| `skyscraperRentMultiplier` | 1.5 | 8.7 |
| `maxBuildDiscountPercent` | 75 | 9.4 |
| `permitsByPlayerCount` | see Section 10.3 | 10.3 |
| `councilEveryNRounds` | 4 | 11.1 |
| `voteInfluence` / `politicianVoteInfluence` | 100 / 200 | 11.3 |
| `publicOpinionInfluence` | 100 per side | 11.3 |
| `loanAmount` / `loanRepay` / `maxLoans` | 200 / 250 / 2 | 15.7 |
| `jailBail` / `maxJailTurns` | 50 / 2 | 14.3 |
| `jailRentMultiplier` | 0.5 | 14.2 |
| `hospitalBill` / `insuredMarkerCost` | 100 / 50 | 15.3 |
| `mallPrices` / `mallSellPrice` | [50, 100, 150] / 50 | 15.3 |
| `handLimit` | 3 | 15.4 |
| `timedModeDefaultRounds` | 40 | 16.1 |
| Character, perk, tile, card and ordinance numbers | as listed | 5, 12, 13, 15 |

Perk, character, tile, card, and ordinance numbers live in their own content files (Section 21), next to the definitions they belong to.

---

## 19. Technical design: stack and structure

### 19.1 Recommended stack

| Piece | Choice | Why |
| --- | --- | --- |
| Language | **C#** | Angel already knows it. |
| Runtime | Current **LTS release of .NET** (.NET 10 at time of writing; confirm before starting) | Long-term support, free, works on Windows, Mac and Linux. |
| Rules engine | Plain **C# class library** with no UI code | The same engine can run in the browser, a test, a simulation, or a future server. |
| UI | **Blazor WebAssembly** | Runs in any web browser while still being written in C#. Simple HTML/CSS/SVG is enough for the graphics level wanted. A local-only version can be hosted for free as static files. |
| Tests | **xUnit** | Standard for .NET. |
| Online (later) | **ASP.NET Core + SignalR** | SignalR sends real-time messages between server and browsers, and it's the standard .NET way to do this. The same engine runs on the server. |
| CI | **GitHub Actions** | Builds and runs tests on every push. |

**Alternative:** Godot 4 with C# makes sense if richer animation becomes a priority. Check whether Godot's C# web export is supported before choosing it, because browser play matters for playing online with family. Unity would also work, but it's heavier than this project needs.

**Before starting:** confirm the stack with Angel. Explain in plain words what Blazor and SignalR are and why they were chosen.

### 19.2 Solution layout

```
/CityTycoon.sln
/content/                     JSON game content (board, cards, rules, ...)
/src/
  CityTycoon.Engine/          Rules engine. No UI. No networking.
  CityTycoon.AI/              AI players. Depends only on Engine.
  CityTycoon.Sim/             Console app: runs bot-vs-bot games and writes balance reports.
  CityTycoon.Web/             Blazor WebAssembly UI (local hot-seat + bots).
  CityTycoon.Server/          (Phase 11) ASP.NET Core + SignalR online host.
/tests/
  CityTycoon.Engine.Tests/
  CityTycoon.AI.Tests/
/docs/
  GAME_DESIGN.md              This file.
  DECISIONS.md                Running log of rule rulings and design decisions.
  ARCHITECTURE.md             Plain-language explanation of how the code fits together.
```

**Dependency rule** (arrows mean "depends on"):

```
Web ──► AI ──► Engine
Web ──────────► Engine
Sim ──► AI ──► Engine
Server ──► AI ──► Engine
```

The Engine never depends on anything above it. This is **separation of concerns**: the rules don't know or care whether a human, a bot, or a network message is making the decisions.

---

## 20. Engine architecture

### 20.1 Core idea: the engine is a referee

The engine holds the **game state** (everything about the game at this moment). It answers two questions:

1. **"What decision is the game waiting for, and from whom?"** This is the *pending decision*.
2. **"Player X made choice Y. What happens now?"** The engine checks that the choice is legal, applies it, and returns the events that happened.

Nothing outside the engine changes the game state directly. The UI, the bots, and the future server all just answer the pending decisions.

```csharp
// Simplified shape of the main engine API.
public sealed class GameEngine
{
    public GameState State { get; }

    // Who needs to decide what right now (can be several players at once,
    // e.g. sealed bids or council votes).
    public IReadOnlyList<PendingDecision> GetPendingDecisions();

    // Every legal option for this player right now, for UI buttons and bots.
    public IReadOnlyList<GameAction> GetLegalActions(PlayerId player);

    // Validate + apply. Returns what happened, for the log and animations.
    public ActionResult Submit(PlayerId player, GameAction action);

    // What one player is allowed to see (hides other hands, sealed bids,
    // hidden government tiles they haven't peeked at).
    public PlayerView GetView(PlayerId viewer);
}
```

### 20.2 Game state

`GameState` is a plain data object that can be saved to JSON:

- Settings: seed, game mode, privacy mode, player count.
- Board: the generated list of spaces, each tile's revealed/hidden status, and which players have peeked at which tiles.
- Players: cash, position, character, Momentum tokens, hand, loans, Insured marker, Jail and Hospital status, doubles count, pending properties, and per-lap flags (such as Hostile Takeover used).
- Ownership: owner, building level, mortgaged/pending status for each property.
- City: zoning per district, permit supply, active ordinances with rounds left, Historic Preservation locks.
- Decks: draw piles, discard piles, the Mall row.
- Turn tracking: round number, active player, phase, temporary effects (Stadium, Grand Opening).
- RNG state (Section 20.5).
- The event log.

### 20.3 The resolution stack

Effects nest inside each other. For example: you land on a space, which starts an auction, during which someone takes a loan. Or a card is played, which opens a reaction window. To handle this, the engine keeps a **stack** of steps waiting to resolve:

- A step can push new steps on top of the stack (for example, "LandOnSpace" pushes "RunAuction").
- When a step needs a player's choice, it creates a pending decision and pauses.
- When the choice arrives, the engine continues from that step.

Explain this pattern to Angel with a diagram in `docs/ARCHITECTURE.md`. It's the trickiest part of the engine.

### 20.4 Actions, decisions, and events

| Concept | What it is | Examples |
| --- | --- | --- |
| **PendingDecision** | What the engine is waiting for | `ChooseMoveMethod`, `ChooseNudge`, `SubmitBid`, `CastVote`, `ChooseCasinoBet`, `ChooseStadiumGroup`, `ReactionWindow`, `RaiseMoney`, `ChooseDistrict`, `PickAgendaOrdinance`, `RespondToTrade`, `DiscardToHandLimit` |
| **GameAction** | A player's answer | `RollDice`, `PrecisionRoll`, `Nudge(+2)`, `Bid(240, DownPayment)`, `Pass`, `Vote(Yes, lobby: 300)`, `Build(propertyId)`, `PlayCard(cardId, target)`, `ProposeTrade(...)` |
| **GameEvent** | Something that happened, for the log, animations, and replays | `DiceRolled`, `PlayerMoved`, `PassedGo`, `TileRevealed`, `AuctionWon`, `RentPaid`, `OrdinancePassed`, `PlayerBankrupt` |

Use C# `record` types for actions and events, so they're simple, read-only, and easy to turn into JSON.

### 20.5 Randomness and determinism

- Every bit of randomness (dice, shuffles, board generation, Council outcomes) comes from **one seeded random number generator** stored in the game state.
- Write a small custom generator (such as PCG32 or xoshiro128**) instead of using `System.Random`, so results are guaranteed identical on every machine and .NET version.
- **The same seed plus the same actions must always produce the same game.** This makes replays, bug reports, online syncing, and tests reliable.
- Tests use an injectable dice source (Section 26.2) to script exact rolls.

### 20.6 The rule-modifier system

Characters, perks, Government tiles, ordinances, and temporary effects all *change* rules. Don't write `if (character == Mogul)` all over the code. Each of them supplies **modifiers** that the engine asks about at fixed **hook points**:

```csharp
public interface IRuleModifier
{
    // Each hook has a default "no change" implementation; a modifier
    // overrides only the hooks it cares about.
    int ModifyRentPercent(RentContext ctx) => 0;
    int ModifyBuildDiscountPercent(BuildContext ctx) => 0;
    int ModifyBail(PlayerId p, int bail) => bail;
    int ModifyVoteInfluence(PlayerId p, int influence) => influence;
    int ModifyBidForComparison(PlayerId p, int bid) => bid;
    void OnPassGo(GoContext ctx) { }
    void OnTurnStart(TurnContext ctx) { }
    bool CanBeTargeted(TargetContext ctx) => true;
    // ...more hooks added as needed.
}
```

The engine gathers every active modifier (from the character, owned perks, revealed tiles, active ordinances, and temporary effects) and combines them. This follows the **Open/Closed Principle**: you add a new character or ordinance by writing a new modifier, without changing existing code.

---

## 21. Content data (JSON)

All game content lives in `/content` as JSON files. The engine checks them when it loads them. A test must confirm that every file loads, every ID is unique, and every reference points to something real.

| File | Contents |
| --- | --- |
| `rules.json` | All tunables (Section 18). |
| `board.json` | Corners and the 10 fixed spaces of each district, in order. |
| `properties.json` | Property data (Section 8.7). |
| `groups.json` | Color groups and their perks. |
| `characters.json` | The 8 characters. |
| `govtiles.json` | The 7 Government tiles. |
| `opportunity.json`, `events.json`, `ordinances.json` | The three decks. |

### 21.1 Example: property

```json
{
  "id": "platinum-row",
  "name": "Platinum Row",
  "type": "color",
  "group": "dark-blue",
  "assessedValue": 400,
  "houseCost": 200,
  "rents": [50, 200, 600, 1400, 1700, 2000, 3000]
}
```

`rents[0]` is the base rent, `rents[1..4]` are houses, `rents[5]` is the hotel, and `rents[6]` is the skyscraper.

### 21.2 Example: cards (built for expansion)

Every card names an **effect type** plus parameters. Adding a new card that reuses an existing effect type is **just a JSON edit**. A card with brand-new behavior needs one new effect class in C#.

```json
[
  {
    "id": "tax-refund",
    "deck": "event",
    "name": "Tax Refund",
    "text": "Collect $100.",
    "copies": 1,
    "effect": { "type": "CollectFromBank", "amount": 100 }
  },
  {
    "id": "rent-strike",
    "deck": "opportunity",
    "name": "Rent Strike",
    "text": "Pay half of one rent.",
    "copies": 2,
    "timing": "reaction",
    "trigger": "OnRentOwed",
    "effect": { "type": "RentMultiplier", "multiplier": 0.5 }
  },
  {
    "id": "audit",
    "deck": "opportunity",
    "name": "Audit",
    "text": "Pick a player. They pay 10% of their cash to the bank.",
    "copies": 1,
    "timing": "ownTurn",
    "target": "opponent",
    "effect": { "type": "PayPercentOfCashToBank", "percent": 10 }
  }
]
```

```csharp
// Each effect type is one small class, registered by its "type" name.
public interface ICardEffect
{
    string Type { get; }                       // e.g. "CollectFromBank"
    void Apply(EffectContext ctx, JsonElement parameters);
}
```

Use the same pattern for Government tile effects and ordinances. Also add a `contentVersion` field to each file, and support optional extra card files (for example `opportunity.expansion1.json`) that load alongside the base set, so expansions can be added later.

### 21.3 Example: character

```json
{
  "id": "politician",
  "name": "The Politician",
  "text": "Your City Council vote is worth 200 influence instead of 100.",
  "modifiers": [ { "type": "VoteInfluence", "value": 200 } ]
}
```

---

## 22. Player controllers

Every seat at the table is run by a **controller**. The engine doesn't know which kind it is.

```csharp
public interface IPlayerController
{
    // Given only what this player may see, choose an action.
    Task<GameAction> DecideAsync(PlayerView view, PendingDecision decision,
                                 IReadOnlyList<GameAction> legalActions);
}
```

| Controller | Used for |
| --- | --- |
| `HumanUiController` | A person clicking in the browser. |
| `BotController` | An AI player (Section 23). |
| `RemoteController` | (Phase 11) A person on another device, through the server. |

A **game host** loop asks the engine for pending decisions and routes each one to the right controller. When several players must decide at once (sealed bids, votes), the host collects every answer before submitting them.

---

## 23. AI players

### 23.1 Fair play

Bots only ever receive their own `PlayerView`. They **never** see hidden tiles they haven't peeked at, other players' cards, or sealed bids. Write a test that proves this.

### 23.2 Difficulty levels

| Level | Behavior |
| --- | --- |
| **Easy** | Mostly random legal choices with basic sense: never bids more than its cash minus a safety reserve, builds when it can, votes randomly with no lobbying. |
| **Normal** | Uses simple estimates (Section 23.3). Bids up to a property's estimated value. Builds on its best groups. Lobbies when an ordinance clearly helps or hurts it. Uses cards at sensible times. |
| **Hard** | Normal, plus: tracks opponents' likely set completions and bids to block them, keeps a cash reserve based on the most dangerous rents ahead, lobbies more strategically, and uses Nudge to dodge big rents or reach good spaces. |

### 23.3 Estimates the bots use

- **Property value:** expected rent per lap (based on how often spaces get landed on) × rounds left, plus a big bonus if winning the property completes a set, plus a blocking bonus if it stops an opponent's set, adjusted for revealed tiles and zoning.
- **Cash reserve:** enough to survive the most expensive rent within the next 12 spaces.
- **Vote value:** how much this ordinance would change the bot's net worth compared with each opponent's. Lobby up to a share of that difference.
- **Nudge choice:** compare the expected value of the possible landing spots.

Each bot gets a small random "personality" (for example, risk tolerance), so bots don't all play the same. On top of that each bot secretly draws a style, Aggressive, Balanced or Reserved, on its own, so a table can have two Aggressive bots and no Reserved one (decision #144). Bot decisions must be quick (under about 50 ms each), so simulations stay fast.

---

## 24. User interface (v1, Blazor)

Graphics should be simple and clear: colored rectangles, readable text, and icons. Function comes first.

### 24.1 Screens

1. **Main menu:** New Game, Continue (load save), Rules, Settings.
2. **New game setup:** player count (2–6); for each seat, Human or Bot (Easy/Normal/Hard) and a name; character picks; game mode and round count; privacy mode; optional seed.
3. **Game screen:**
   - The board ring (Section 4.4). Each property shows its color band, short name, owner marker, building level, and mortgaged/pending state. Hidden tiles show a "?" card back. Revealed tiles show their icon.
   - District labels on each side, with a **zoning badge** (Restricted, Residential, Commercial, High-Rise).
   - **City panel:** permits left, active ordinances with rounds left, and the round number (with the limit in Timed mode).
   - **Player panels:** name, character, cash, net worth, Momentum tokens, card count, loans, Insured marker, Jail/Hospital status.
   - **Center:** dice, the current decision panel with big clear buttons, and a scrolling game log.
4. **Decision panels:** Move method, Nudge (−2, −1, 0, +1, +2 with each landing spot previewed on the board), Auction (bid entry with +$10/+$50/+$100 buttons, a Pass button, and a Pay in full / Down payment toggle), Casino, Mall, Stadium, Jail, Raise money, and Reaction prompts.
5. **Property detail:** tap any property to see its full rent table, current rent with each modifier explained, owner, and level.
6. **City Council:** the agenda card, vote and lobbying entry, then a dramatic reveal of every vote and lobbying amount, a meter showing the chance of passing, the roll, and the result.
7. **Trade builder:** two columns (what you give / what you get) for cash, properties, cards, and tokens; propose, accept, decline, or counter.
8. **Pass-the-device screen** (privacy mode): "Pass to Angel. Tap when ready." It covers everything until tapped.
9. **End of game:** final scoreboard with each player's net-worth breakdown and simple stats (rent collected, auctions won, money spent lobbying).
10. **Rules reference:** the full rules inside the app.

### 24.2 Feel

- Short, skippable animations for dice and token movement, with an animation speed setting.
- A "Fast bots" option that skips bot animations.
- Every number shown should be explainable: hovering or tapping a rent shows how it was calculated.

---

## 25. Hidden information and local play

Local hot-seat has a privacy problem: everyone is looking at the same screen. Two modes handle this:

| Mode | How hidden information works |
| --- | --- |
| **Hidden** (default) | Before any private input (sealed bid, vote, looking at your hand, a peek result), the pass-the-device screen appears. After the input, the screen blanks again. When several humans must bid or vote, each one is prompted in turn behind the pass screen. Bots answer instantly. |
| **Open** (casual) | Hands are shown openly and bids are typed in one after another without hiding. Good for one person playing against bots, or a relaxed family game. |

Add a per-player "Auto-pass auctions" toggle, so hot-seat games with many humans don't slow down.

---

## 26. Testing and simulation

### 26.1 Unit tests

- At least one test for **every rule** in Sections 4–17, named after the rule (for example `Nudge_CannotReduceMovementBelowOne`).
- Content tests: all JSON loads, IDs are unique, references are valid, every card's effect type exists.
- Fair-play test: a bot's `PlayerView` never contains hidden information.

### 26.2 Scripted dice

Provide an `IDiceSource` interface with a `ScriptedDice` test version, so a test can say "the next rolls are 3+4, then 6+6." Board generation and shuffles must also be controllable in tests (a fixed seed or a scripted layout).

### 26.3 Simulation tool (`CityTycoon.Sim`)

A console app that runs many bot-vs-bot games and writes a report (CSV plus a readable summary):

- Average and spread of game length.
- Win rate by character, by color group owned, and by seat position.
- Average auction price as a share of assessed value, over the course of the game.
- Council pass rate, average lobbying spent, and how often trailing players block leaders.
- Casino usage and its effect on results, time spent in Jail, how often bankruptcies happen, how often permits run out.
- Any errors or stuck games (these must be zero).

**Balance targets:**

- No character's win rate is more than 5 percentage points above an even share (1 ÷ number of players).
- Timed games average 25–35 rounds of meaningful play.
- No single color perk wins far more often than the others.

Running `dotnet run --project src/CityTycoon.Sim -- --games 1000 --players 4` should finish in reasonable time.

### 26.4 Replays

Since the game is deterministic (Section 20.5), a saved **seed plus the list of actions** can replay any game exactly. Add a debug option to export one, so bugs can be reproduced.

---

## 27. Accessibility

These are requirements, not extras:

- **Read-aloud:** a speaker button on every card, ordinance, tile description, and the rules reference, using the browser's built-in speech (Web Speech API, called from Blazor).
- **Readable text:** a font setting that includes highly legible options (such as Atkinson Hyperlegible or OpenDyslexic), adjustable text size, and comfortable line spacing.
- **Icons with text:** every important state (mortgaged, pending, hidden tile, zoning level) has an icon as well as a label.
- **Color-blind safe:** color groups also show a pattern or short code, not just color.
- **Keyboard support** and visible focus outlines.
- Respect the system's "reduce motion" setting.

---

## 28. Online multiplayer (later, but plan for it now)

Online play isn't built until Phase 11, but these choices from Phase 1 keep it possible:

- **Server-authoritative:** the engine runs on the server, and clients only send actions. This stops cheating.
- **Only views are sent:** each client receives only its own `PlayerView`, never the full state.
- **Everything is serializable:** game state, actions and events can all be turned into JSON.
- **Determinism** (Section 20.5).
- **Rooms:** a host creates a room and shares a short join code. Seats can be humans or bots.
- **Reconnects:** if a player drops, they can rejoin the same seat. After a timeout, a bot plays for them until they return.
- **Timers:** online reaction windows, bids and votes have time limits, with defaults applied when time runs out (Pass or Abstain).
- **Hosting:** decide in Phase 11. Any service that runs an ASP.NET Core app with WebSockets will work.

---

## 29. Build plan

Each phase should be playable or runnable when it's done. Don't start a phase until the previous one's acceptance criteria pass.

### Phase 0: Repository setup

- Create the solution and projects (Section 19.2), with the dependency rules enforced.
- Set up GitHub Actions to build and run tests on every push.
- Add `docs/GAME_DESIGN.md` (this file), a `DECISIONS.md`, an `ARCHITECTURE.md` stub, and a README explaining how to run everything.
- Load `rules.json` into a typed C# class.

**Done when:** CI is green; one sample test passes; `rules.json` loads in a test.

### Phase 1: Core game loop (engine only)

- Board generation with hidden tiles placed (no tile effects yet).
- Players, seeded RNG, dice, movement, doubles, three doubles → Jail (basic Jail: pay $50 or roll for doubles, 2 turns max).
- GO salary.
- Sealed-bid auctions (pay in full only), tie-break rules.
- Rent: base rent, full-set double, railroads, utilities. Tax spaces.
- Basic bankruptcy. Classic and Timed endings. Net-worth scoring.
- The resolution stack and pending-decision API (Section 20).
- A random bot plus the Sim console runner.

**Done when:** 1,000 random-bot games finish with zero errors; the same seed gives an identical event log; every implemented rule has tests.

### Phase 2: First playable version in the browser

- Blazor app: setup screen, board rendering, player panels, game log, dice.
- Hot-seat humans plus Easy bots.
- Auction panel. Privacy modes with the pass-the-device screen.
- End-of-game scoreboard.

**Done when:** Angel can play a full game in the browser against 3 Easy bots, and a 2-human hot-seat game works with hidden bids.

### Phase 3: Building and money

- Building levels, the even-build rule, the build limit, selling.
- Zoning levels per district (fixed at starting values for now) and the permit supply.
- Mortgages, loans, down payments and pending properties, the raise-money flow, full bankruptcy rules.
- Trading (engine plus trade-builder UI).

**Done when:** tests cover every building, zoning and permit rule; Angel can build to hotels in the starting Commercial district; trades work between humans and bots.

### Phase 4: Momentum, Jail, and landmarks

- Momentum tokens: Nudge (±2) with a landing preview, Precision Roll, the token cap.
- Full Jail rules (frozen assets, half rent).
- Hospital, Casino (High/Low and Dice Duel, no limit), Stadium, City Park. The Mall can wait for Phase 5.
- Insured markers.

**Done when:** all of these have tests and UI panels.

### Phase 5: Cards

- The data-driven card effect system (Section 21.2).
- The Opportunity deck with hand, hand limit, and own-turn plays.
- Reaction windows (Rent Strike, Insurance, Detour) with auto-pass.
- The Event deck. The Mall shop. Reshuffling.
- Proof of expansion: a test that loads an extra card file.

**Done when:** every card in Sections 15.4–15.5 works and is tested, and a new card that reuses an existing effect type can be added with JSON only.

### Phase 6: Government tiles

- Hidden and revealed status, reveal on landing, peeking (Inside Source).
- All 7 tiles' neighbor and landing effects, using the rule-modifier system (Section 20.6).
- The rent breakdown display showing tile modifiers.
- Private peek knowledge in each player's view.

**Done when:** all tile rules are tested; tiles appear in different positions across seeds; hidden tiles have no effect until revealed.

### Phase 7: Perks and characters

- All 8 color perks, the railroad bonuses (including Express Line), and the utility bonuses.
- All 8 characters and the character-selection screen.

**Done when:** every perk and character has tests; the Sim report shows win rates by character.

### Phase 8: City Council

- The Ordinance deck, regular and Emergency sessions, the agenda setter (lowest net worth).
- Sealed votes with lobbying, the influence math, the odds meter, random resolution.
- Durations, grandfathering, Historic Preservation, every ordinance effect.
- The Council UI with the dramatic reveal.

**Done when:** the influence math is tested with the Section 11.3 example; all 17 ordinances are tested; a full game runs with several Council sessions.

### Phase 9: Catch-up, save/load, polish, accessibility

- The catch-up GO bonus and the end-screen net-worth breakdown.
- Save and load (JSON), and continuing a saved game.
- Settings: animation speed, fast bots, auto-pass.
- Accessibility features (Section 27) and the in-app rules reference with read-aloud.

**Done when:** a saved game reloads exactly; read-aloud works on cards and rules.

### Phase 10: Smarter AI and balancing

- Normal and Hard bots (Section 23).
- Full Sim reports (Section 26.3).
- Use the reports to tune `rules.json`, and record what changed and why in a `docs/BALANCE.md` file.

**Done when:** Hard bots beat Easy bots most of the time over 1,000 games; balance targets are met or the gaps are written up for Angel to decide.

### Phase 11: Online multiplayer

- `CityTycoon.Server` with SignalR: rooms, join codes, seats, server-authoritative games.
- `RemoteController`, per-player views, reconnects, bot takeover, time limits.
- Hosting setup and a short guide for Angel.

**Done when:** two browsers on different networks can play a full game together, and a dropped player can rejoin.

### Phase 12: Content and variants (ongoing)

- More Opportunity, Event and Ordinance cards (as JSON files).
- Optional house-rule toggles (for example, an open ascending auction for shared-screen play).
- Theme and city naming pass.

---

## 30. Open questions for Angel

Each has a default the builder should use until Angel decides otherwise.

| # | Question | Default |
| --- | --- | --- |
| 1 | Final game name, and city and street names? | "City Tycoon" and the placeholder names above. |
| 2 | Is the tech stack (Blazor + .NET, SignalR later) OK? | Yes, unless Angel says otherwise. |
| 3 | Sealed-bid auctions, or open bidding where prices go up live? | Sealed bids, with open bidding as a later option. |
| 4 | How long should Timed mode be? | 30 rounds. |
| 5 | Should Momentum tokens be tradeable? | Yes. |
| 6 | Permit supply and zoning starting values feel right? | As in Section 10; tune after simulations. |
| 7 | Should Council sessions be every 4 rounds? | Yes; tune after playtests. |
| 8 | Should the Casino stay unlimited even late in the game? | Yes (only the Casino Regulation ordinance caps it). |
| 9 | Character names and art style? | The names in Section 5, shown as simple icons. |

---

## 31. Glossary

| Term | Meaning |
| --- | --- |
| **Assessed value** | A property's official value. It sets rent, the mortgage value, the minimum bid, and its share of net worth. |
| **Agenda setter** | The player who picks which ordinance is voted on. |
| **District** | One side of the board. Zoning is set per district. |
| **Down payment / pending** | Winning an auction by paying 20% now and the rest at your next GO. |
| **Grandfathering** | Buildings that existed before a downzoning are allowed to stay. |
| **Hot-seat** | Several people playing on one device, taking turns. |
| **Influence** | Voting power in the City Council: votes + lobbying + public opinion. |
| **Lap** | One full trip around the board. |
| **Momentum token** | A token you spend to Nudge or Precision Roll. |
| **Net worth** | The score: cash plus property and building value, minus loans. |
| **Nudge** | Spending 1 token per space to move 1–2 spaces more or fewer (the Commuter up to 4). |
| **Ordinance** | A City Council proposal that changes the rules or zoning. |
| **Permit** | A limited license needed to build a hotel or skyscraper. |
| **Precision Roll** | Rolling 3 dice and choosing 2. |
| **Round** | Every remaining player takes one turn. |
| **Zoning** | The highest building level a district allows. |

### Software terms (for Angel)

| Term | Meaning |
| --- | --- |
| **Blazor WebAssembly** | A way to build web pages in C# that run directly in the browser. |
| **Class library** | A C# project with no screen of its own, used by other projects. The rules engine is one. |
| **CI (Continuous Integration)** | A service (GitHub Actions) that automatically builds and tests the code every time it changes. |
| **Deterministic** | Same inputs always give the same result. |
| **JSON** | A simple text format for data, used here for game content and save files. |
| **Seed** | A starting number for the random generator. The same seed gives the same "random" results. |
| **Server-authoritative** | The server runs the real game, and players' devices only send choices. This prevents cheating. |
| **SignalR** | A .NET library for real-time messages between a server and browsers. |
| **Unit test** | A small automatic check that one piece of code works correctly. |
