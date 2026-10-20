# Decisions

A running list of rulings and design decisions, so nobody has to rediscover them. Newest at the bottom of each section. "Angel" means Angel decided it; "Builder" means the builder picked the simplest reading and flagged it for Angel to confirm or change.

## Kickoff (2026-10-20)

| # | Decision | Who | Why |
| --- | --- | --- | --- |
| 1 | Tech stack: C# on .NET 10, rules engine as a plain class library, Blazor WebAssembly for the browser UI, xUnit for tests, GitHub Actions for CI, ASP.NET Core + SignalR for online play later. | Angel (trusted the recommendation) | Keeps the engine separate from any screen or network code, so online play with friends and family can be added later without rewriting the rules. |
| 2 | Angel uses Visual Studio, so the README's "how to run" steps are written for it. | Angel | |
| 3 | For the first round of building, the builder may merge its own phase PRs without waiting. Later PRs wait for Angel's review. | Angel | |
| 4 | Use the default answers to all 9 open questions in GAME_DESIGN.md Section 30. | Angel | |
| 5 | **The Commuter with the Light Blue perk** can hold **8** Momentum tokens: the Commuter's +1 and Light Blue's +2 add together on top of the base limit of 5. | Angel | Sections 5 and 13.2 each set a limit without saying what happens together. |
| 6 | **No extra turn from doubles on any Jail turn,** including after paying bail or using Get Out of Jail Free. | Angel | Section 14.3 option 1 said "doubles give an extra turn as usual", which clashed with Section 7.2 step 9. Section 14.3 is read as "no extra turn". |
| 7 | Default font: **Atkinson Hyperlegible** (OpenDyslexic stays available as an option). | Angel | |
| 8 | The solution file uses the classic `.sln` format (not the newer `.slnx`). | Builder | Opens in every recent version of Visual Studio. |
| 9 | Content JSON files are embedded into the Engine at build time. | Builder | The browser has no normal file system, so this keeps one loading method for the browser, tests and the Sim. |
| 10 | Numbers the spec mentions outside the Section 18 table (railroad rents, utility multipliers, player limits, doubles-to-Jail count) also live in `rules.json`. | Builder | "Never hard-code a tunable number." |

## Phase 1: core game loop (2026-10-20)

| # | Decision | Who | Why |
| --- | --- | --- | --- |
| 11 | A player whose cash is below an auction's minimum bid passes automatically and isn't asked. | Builder | Asking a question with only one possible answer slows hot-seat games down. Revisit in Phase 3, when loans let players bid more than their cash. |
| 12 | Auctions of a bankrupt player's properties break ties starting from the player whose turn it is (there is no "lander"). | Builder | Section 8.2 only defines tie order for a lander. |
| 13 | Paying bail takes you back to the normal "choose how to move" step, so "Pay bail" and "Roll" are two separate clicks. | Builder | Matches Section 14.3 ("pay, then roll and move normally") and leaves room for the other move methods later. |
| 14 | Until Phase 3, any payment you can't cover with cash makes you bankrupt straight away (no mortgages, loans or selling yet). Bail can only be chosen with enough cash on hand. | Builder | Phase 1 asks for "basic bankruptcy". |
| 15 | Railroad and utility rent counts mortgaged railroads and utilities the owner holds. | Builder | Section 13.1 only says pending ones don't count. |
| 16 | The Sim stops a Classic game after 500 rounds and reports it as "stalled" instead of an error. With no buildings until Phase 3, most Classic games stall, which is expected for now. | Builder | Classic mode has no round limit, so a safety stop is needed. |

## Phase 2: first playable version (2026-10-20)

| # | Decision | Who | Why |
| --- | --- | --- | --- |
| 17 | The pass-the-device screen appears only before a private choice (a sealed bid), and only when two or more humans are playing in Hidden mode. | Builder | With one human and some bots, there's nobody to hide from. Public choices like "Roll dice" don't need hiding. |
| 18 | Bots pause for 0.35 seconds before each move so people can follow along. "Fast bots" turns the pause off. | Builder | Section 24.2 asks for a fast-bots option. Proper animations come later. |
| 19 | Easy bots won't bid into a safety reserve ($100 to $250, random per bot) or much past assessed value (90% to 130%, random per bot). In Jail they pay bail only when they have plenty of cash. | Builder | Section 23.2 "basic sense", plus a small personality so bots don't all play alike. |
| 20 | The board and log are drawn from a "spectator" view (no secrets), and the log text is built from a view too, so the screen can never show a hidden tile or an unrevealed bid. | Builder | Fair play (Section 23.1) applies to what's on a shared screen as well. |

## Phase 3: building and money (2026-10-20)

| # | Decision | Who | Why |
| --- | --- | --- | --- |
| 21 | Only the player whose turn it is can offer a trade, and only at the "choose how to move" or "anything else before you end your turn?" moments (or in Jail). | Builder | Section 15.9 says "any player at any time the game is waiting on a normal turn action". With one shared screen, letting every player interrupt at once gets confusing. Easy to widen later. |
| 22 | No counter-offers: the other player accepts or declines. To change a deal, decline and offer again. | Builder | Keeps the first version simple. |
| 23 | A player can offer at most 3 trades per turn (`maxTradeProposalsPerTurn` in `rules.json`). | Builder | Stops a player (or a bot) flooding someone with offers. |
| 24 | A trade is checked again at the moment it's accepted. If something changed (for example the cash is gone), it's cancelled with a reason in the log. | Builder | The world can change between offer and answer. |
| 25 | Selling a building gives back half of its full cost before any discounts. | Builder | Section 9.3 says "half of what that level cost". Discounts arrive with characters and tiles, and this stops "build cheap, sell high" tricks. |
| 26 | Unmortgaging and the 10% trade fee round **up** to a whole dollar. Mortgage values themselves are always whole numbers. | Builder | The spec doesn't say; rounding up means the bank never loses a dollar. |
| 27 | When someone goes bankrupt, their pending properties go back to the bank and are auctioned, even if they owed another player. | Builder | A pending property was never fully paid for, so it isn't theirs to hand over. |
| 28 | Replaces #11: a player passes an auction automatically only if they couldn't reach the minimum bid **even after taking every loan they can and using a down payment**. | Builder | Loans and down payments now let people bid above their cash. |
| 29 | Replaces #14: owing more cash than you have now opens a "raise money" choice (mortgage, sell buildings, take loans). You go bankrupt only when even all of that wouldn't be enough. | Builder | Section 15.10. |
| 30 | During an auction, the only "manage money" action allowed is taking a loan. | Builder | Section 8.4. |
| 31 | If someone can't pay a pending balance at GO but could raise it, they choose: raise the money, or give the property up. | Builder | Section 8.5 says "you may raise money", so it's a choice, not forced. |
| 32 | The pass-the-device screen also appears when one person offers another person a trade. | Builder | The other person needs the device to answer. |
| 33 | The Sim plays half Easy bots and half Random bots by default (`--bots random`, `easy` or `mixed`). | Builder | Random bots test odd situations; Easy bots test sensible play. |

## Phase 4: Momentum, Jail and landmarks (2026-10-20)

| # | Decision | Who | Why |
| --- | --- | --- | --- |
| 34 | You can't Nudge a Jail roll (trying for doubles). | Builder | Nudging a Jail roll would let you change where you walk out to, which the spec doesn't mention. Momentum can't be spent in Jail anyway (Section 10.4). |
| 35 | City Park only gives a Momentum token for now. The "draw an Opportunity card" choice arrives with the cards in Phase 5. | Builder | There are no cards yet. |
| 36 | If two players boost the same color group at the Stadium, its rent is still only doubled once. | Builder | "Doubled" stacking to ×4 would be very swingy, and the spec doesn't say it stacks. |
| 37 | Each player has at most one Stadium boost. Landing on the Stadium again moves it to the new group. Boosts end at the start of the chooser's next turn. | Builder | Section 13.3 says "until your next turn". |
| 38 | Casino bets are whole dollars, at least $1, up to all your cash (no betting limit). A Dice Duel stake is capped by the smaller of the two players' cash. | Builder | Angel asked for no betting limit. The duel cap means nobody can lose money they don't have. |
| 39 | Easy bots always walk past the Casino and only accept small duels (up to 1/20 of their cash). | Builder | Gambling has no edge for them, so "basic sense" (Section 23.2) says skip it. |
| 40 | The "send to Hospital" rule (pay the bill, then one die next turn, or use up an Insured marker) is built and tested, but nothing sends you there until Event cards in Phase 5. Landing on the Hospital is just a visit where you may buy a marker. | Builder | The things that send you to Hospital are cards. |
| 41 | When someone goes bankrupt to another player, that player also gets their Momentum tokens (up to the cap). Going bankrupt to the bank, the tokens are lost. | Builder | It matches "the creditor gets everything". |
| 42 | Utility rent uses the dice total, not the nudged total. | Builder | Utility rent is "dice × a number", and the dice didn't change. |
| 43 | Jail half rent rounds down, but is always at least $1. Stadium doubling is applied before Jail halving. | Builder | Keeps rent whole numbers; the order doesn't change the result except for rounding. |
