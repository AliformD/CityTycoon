# City Tycoon: project map for Claude

Read this first. It tells you where things live so you do not need to read the whole repo. Open only the files the task touches. If this file is wrong or out of date, fix it in the same change.

Owner: Angel (beginner C# developer, dyslexic). Explain terms plainly, expect typos, use they/them for people. One PR per change, Angel reviews and merges. Never merge PRs yourself.

## What it is
A Monopoly-like board game in C# (.NET 10). Design: `docs/GAME_DESIGN.md` (numbered sections; Section 29 is the phase plan). Rulings: `docs/DECISIONS.md` (numbered rows, add the next number). How code fits: `docs/ARCHITECTURE.md`. Balance results: `docs/BALANCE.md`.

Status: Phases 0 to 10 done. Phase 11 (online multiplayer) is on hold by Angel's choice; Phase 12 (more content and house rules) is ongoing and open ended. Local hot-seat play plus Easy, Normal and Hard bots works.

## Projects (arrows mean "uses")
`Web -> AI -> Engine`, `Sim -> AI -> Engine`. The Engine uses nothing of ours (a test enforces it).

| Project | Job | Start here |
| --- | --- | --- |
| `src/CityTycoon.Engine` | All rules. No screen, no bot code. | `GameEngine.cs`, `Steps/` |
| `src/CityTycoon.AI` | Bots. They only ever get a `PlayerView`. | `EasyBot.cs`, `NormalBot.cs`, `HardBot.cs` |
| `src/CityTycoon.Web` | Blazor WASM UI. | `Services/GameSession.cs`, `Pages/Play.razor` |
| `src/CityTycoon.Sim` | Plays many bot games, writes balance reports. | `SimulationRunner.cs`, `Program.cs` |
| `content/*.json` | Every number, card, ordinance, character. Embedded as resources. | `rules.json` |
| `tests/` | xUnit. `Engine.Tests` has one folder per feature. | `Helpers/TestGame.cs` |

## How the engine works
- `GameEngine.GetPendingDecisions()` says who must choose; `Submit(playerId, action)` applies a choice. UI, bots and (later) a server only answer decisions.
- The game is a stack of `Step`s (`Steps/Step.cs`). A step implements Run, LegalActions, IsLegal and Receive and returns Done, Continue or WaitFor. Child steps run first.
- State is plain data (`State/GameState.cs`), so it serializes to JSON for saves. Everything is deterministic from the seed (`Randomness/Pcg32.cs`); never use `System.Random` or the clock in the engine.
- Rules that change other rules (perks, tiles, ordinances) are `IRuleModifier`s (`Modifiers/`). `RuleModifiers.Active` is cached by a signature, so anything that changes which modifiers apply must be part of that signature.
- Fair play: a `PlayerView` (`Views/PlayerView.cs`) is all a bot or screen may see. Hidden tiles, other hands and sealed bids stay out of it.

## Where to change things
| Change | Touch |
| --- | --- |
| A number (cash, rent, costs) | `content/rules.json`, `Content/RulesConfig.cs` |
| An Opportunity or Event card | `content/opportunity.json` or `events.json`; new effect: `Cards/CardEffects.cs` (explicit list) |
| An ordinance | `content/ordinances.json`; new effect: `Ordinances/OrdinanceEffects.cs` (explicit list) |
| A Government tile | `content/govtiles.json`, `Tiles/TileLandings.cs` |
| A character or perk | `content/characters.json` / `groups.json`, `Modifiers/*Modifiers.cs` |
| A new action, event or step | Add the type, then register it with `[JsonDerivedType]` on `GameAction`, `GameEvent` or `Step`, describe events in `Events/EventDescriber.cs` |
| A bot behavior | The `virtual` methods in `EasyBot` (Reserve, PropertyWorth, SealedAmount, PickBuild, Vote, NudgeThreshold); trades, haggling, color group values and card values in `EasyBot.Trades.cs`; secret personalities (`BotPersonality`, decision #144) in `EasyBot`'s constructor |
| A screen | `src/CityTycoon.Web/Components/` and `Pages/`; styles in `wwwroot/css/app.css` (colors are variables at the top, Day and Night) |
| A new event that moves money or changes a property | Also add it to `Events/GameHistory.cs` (money ledger and space history); a test checks every dollar is listed |
| What a bot writes in its private notebook, or the game log download | `BotNote.cs` and `EasyBot.Note` in `EasyBot.Trades.cs`, `GameLogText.cs`; trade report card in `Events/TradeReport.cs` |
| How an event is shown (pause, icon, sound, money, token walk) | `src/CityTycoon.Web/Services/Playback.cs`; pictures in `Components/Icon.razor`, sounds in `wwwroot/js/app.js` (`ct.sound`) |

## Commands
- Build and test: `dotnet test` (all projects). Last known: 487 engine tests and 99 bot tests, all passing.
- Run the game: `dotnet run --project src/CityTycoon.Web`.
- Balance Sim: `dotnet run -c Release --project src/CityTycoon.Sim -- --games 1000 --players 4 --bots HardVsEasy --mode timed --characters random --seed 1 --out <NEW folder>`. `--out` must be a folder that does not exist yet. Try other numbers without editing `rules.json` with `--set goSalary=300` (any rules.json name, repeatable) and `--rounds 50`. The report includes pacing (first building, close calls, bankruptcies, money at the end), a trade report card and landing odds; it also writes `trades.csv`, `bot-notes.csv` and `landing.csv`.
- CI (`.github/workflows/ci.yml`) builds and tests on every push and PR.

## Gotchas that cost time before
- A class that derives from one implementing an interface must list the interface again to override default-implemented members, or the base default silently wins.
- Explicit lists (card effects, tile landings, ordinance effects, modifier types) are hand-written on purpose (Blazor trimming). A new type that is not in its list does nothing.
- Razor: string component parameters need `@` (`Id="@var"`); copy a `for` loop variable to a local before using it inside child content; no nested quotes in lambda attributes (use a handler method); do not name a loop variable `section`; build mixed text and C# strings in a method.
- Cloud container: install the SDK with `apt` (`dotnet-sdk-10.0`). To check the UI, run Web with `--urls http://localhost:5199` in the background and kill it by PID only. Never `pkill -f` or `ps | grep` with text from your own command line.
- `TestGame` plays with the old money ($1,500 start, $200 GO, $100 catch-up) from `Helpers/TestContent.cs`, so rebalancing `rules.json` doesn't break rule tests. Bot tests use the real rules.
- The Sim runs Release builds: rebuild with `dotnet build -c Release` after changing `content/*.json`, or `--no-build` runs the old numbers.
- Bot tuning: measure in the Sim before and after. Lobbying and blocking bids looked smart but lost games (see `docs/BALANCE.md`).

## Keeping this file honest
When you finish a change: update the row in `docs/DECISIONS.md` if you made a rule call, the matching section of `docs/ARCHITECTURE.md` if the structure changed, and this file if a path, command, count or status changed. Keep this file under about 100 lines; link to the other docs instead of copying them.

## Not built yet (small, none is a major phase)
- Debug replay export (design Section 26.4): save the seed plus the list of actions. The engine does not record actions yet.
- Online multiplayer (Phase 11) was skipped on purpose. It stays possible: the engine is server-ready (state and actions are serializable, views are per player, play is deterministic).
- Accessibility check list (Section 27) was only partly verified by hand: keyboard use and focus outlines in every panel.
