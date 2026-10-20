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
