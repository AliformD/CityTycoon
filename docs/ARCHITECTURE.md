# Architecture

This file explains how the code fits together and *why*. It grows with each phase.

## The big idea: separation of concerns

"Separation of concerns" means each part of the program has one job and doesn't meddle in the others' jobs. City Tycoon is split into projects (a **project** is a folder of C# code that builds into one `.dll` or `.exe`):

```
Web ──► AI ──► Engine
Web ──────────► Engine
Sim ──► AI ──► Engine
```

An arrow means "uses" (or "depends on"). The Engine is at the bottom and uses nothing else of ours.

- **Engine** is the referee. It knows every rule and keeps track of the game, but it has no idea whether a person, a bot, or (later) someone over the internet is making the choices.
- **AI** contains the bots. A bot asks the Engine "what can I do?" and picks one of the answers, exactly like a person clicking a button would.
- **Web** draws the board and buttons in the browser and passes clicks to the Engine.
- **Sim** plays thousands of bot games quickly to test balance.

### Why this matters for online play

Because the Engine never talks to the screen directly, the same Engine can later run on a **server** (a computer on the internet that everyone connects to). Each player's browser just sends choices to the server and gets back what they're allowed to see. Nothing in the rules has to be rewritten for that.

A test (`DependencyRuleTests`) fails if anyone makes the Engine depend on another project, so the rule can't be broken by accident.

## Content lives in JSON, not in code

Every number (starting cash, rents, taxes, and so on) lives in the `content/` folder. The Engine reads `rules.json` into a C# class called `RulesConfig`. To change a number, you edit the JSON file. You don't touch the code.

## The engine is a referee (Phase 1)

`GameEngine` holds the game and answers two questions:

1. **"What are you waiting for?"** `GetPendingDecisions()` lists who must choose something, for example "Player A: choose how to move", or "Players A, B and C: submit a sealed bid".
2. **"Player A chose this. What happens now?"** `Submit(playerId, action)` checks the choice is legal, applies it, and returns the events that happened (dice rolled, moved, paid rent...).

The UI, the bots and (later) the online server never change the game directly. They only answer decisions. That's why the same engine works for all of them.

```
            asks "what are you waiting for?"
 UI / bot  ─────────────────────────────────►  GameEngine
           ◄─────────────────────────────────  "A: choose how to move"
            Submit(A, RollDice)
           ─────────────────────────────────►  checks it's legal, applies it
           ◄─────────────────────────────────  events: DiceRolled, PlayerMoved...
```

### Words used in the code

| Word | Meaning |
| --- | --- |
| **GameState** | All the data about the game right now: players, board, who owns what. Just data, no rules. It can be saved to JSON. |
| **Step** | One piece of work, like "a player's turn" or "an auction for Market Street". The rules live in steps. |
| **PendingDecision** | A question the engine is waiting on: who must answer and what kind of question it is. |
| **GameAction** | An answer: `RollDice`, `PlaceBid(150)`, `EndTurn`... |
| **GameEvent** | Something that happened: `DiceRolled`, `AuctionWon`, `RentPaid`... Used for the log, animations, stats and replays. |
| **PlayerView** | What one player is allowed to see. Bots and screens only ever get a view, so nobody can peek at hidden tiles. |

## The resolution stack (the trickiest part)

Things in the game nest inside each other. You roll and move; the space you land on starts an auction; during the auction everyone bids; then your turn carries on. The engine handles this with a **stack**, like a stack of plates:

- The engine always works on the **top** plate.
- A step can put **new plates on top** ("push"). Landing on a property pushes an auction.
- When the top step is finished, it's **removed**, and the engine goes back to the plate underneath, which carries on from where it was.
- When a step needs a player's choice, it says **"wait"**. The engine stops and reports a pending decision. When the answer arrives, the same step carries on.

Here's the stack while Ana's roll lands her on an unowned Market Street:

```
   1. Game starts           2. Ana's turn starts      3. Ana rolls and moves     4. Everyone bids         5. Auction done
                                                                                                           (removed)
                                                      ┌──────────────────────┐   ┌──────────────────┐
                                                      │ Resolve space        │   │ Auction: Market  │ ◄ waiting for bids
                            ┌──────────────────┐      │ (pushes an auction,  │   ├──────────────────┤   ┌──────────────────┐
                            │ Turn: Ana        │      │  then removes itself)│   │ Turn: Ana        │   │ Turn: Ana        │ ◄ "End turn?"
   ┌──────────────────┐     ├──────────────────┤      ├──────────────────────┤   ├──────────────────┤   ├──────────────────┤
   │ Game             │     │ Game             │      │ Turn: Ana            │   │ Game             │   │ Game             │
   └──────────────────┘     └──────────────────┘      ├──────────────────────┤   └──────────────────┘   └──────────────────┘
                                                      │ Game                 │
                                                      └──────────────────────┘
```

- **GameStep** sits at the bottom for the whole game. Each time it's on top, a turn has just ended, so it starts the next player's turn, runs the end-of-round checks, and ends the game when it's time.
- **TurnStep** is one player's turn. It remembers its `Stage` (start, choose move, roll, post-move...) so it can pause for a choice and carry on later. This pattern is called a **state machine**.
- **ResolveSpaceStep** looks at the space you landed on and pushes whatever comes next.
- **AuctionStep** is either a sealed-bid auction (everyone bids at once, then the bids are revealed together) or, for the 2nd and 3rd property of a color, an open auction where players take turns to raise or drop out (`AuctionStep.ShouldBeOpen`).
- **PaymentStep** moves money, and declares bankruptcy if you can't pay.

Because every step keeps its progress in properties, the **whole stack can be saved** in the middle of anything, even halfway through an auction, and loaded back later.

## Randomness you can replay

All randomness (dice, shuffles, board layout) comes from one seeded generator, `Pcg32`, stored in the game state. The same **seed** (a starting number) and the same choices always produce exactly the same game. That makes bugs easy to reproduce, and later lets every computer in an online game agree.

Tests swap the real dice for `ScriptedDice`, so a test can say "the next roll is 3 + 4". The engine only knows it has an `IDiceSource` (an interface), so it can't tell the difference. Handing a class the thing it needs, instead of letting it build its own, is called **dependency injection**.

## Controllers and the game host

Each seat at the table has an `IPlayerController`: a bot today, a person clicking in the browser from Phase 2, a person on another device later. `GameHost` asks the engine what it's waiting for and passes each question to the right controller. For sealed bids it collects every answer before handing any of them in, so nobody's bid can influence anyone else's.

## Managing money between decisions (Phase 3)

Building, selling, mortgages, loans and trades aren't answers to a question like "roll or pay bail?". They're extra things you may do **while** a question is waiting, and then the same question is asked again. For example, before rolling you might build two houses and take a loan, then roll.

Several different moments allow some of these, so instead of every step repeating the code, each `PendingDecision` carries a `ManagementOptions` value that says which extras are allowed right now:

| Moment | Allowed |
| --- | --- |
| Your turn (before rolling, or after moving) | Everything (`OwnTurn`) |
| In Jail | Everything except building (`InJail`) |
| Owing more than you have | Sell, mortgage, take loans (`RaiseMoney`) |
| Bidding in an auction | Take a loan |

`ManagementOptions` is a **flags enum**: each option is one bit, so several can be combined with `|`, like `Mortgage | TakeLoan`, and checked with `HasFlag`.

All the "manage money" actions inherit from one base class, `ManagementAction`. When one arrives, `GameEngine.Submit` hands it to `Rules/ManagementRules.cs` instead of the step on top of the stack. That class checks it against the decision's options and the rules (`BuildingRules`, `MoneyRules`, `TradeRules`), applies it, and then the engine simply asks the same question again with fresh numbers. A trade offer is the one exception: it pushes a `TradeResponseStep` on top of the stack, so the other player answers first and then the proposer's question comes back. That player may also answer with a `CounterTrade` (checked by `TradeRules.WhyBadCounter`, and by `GameEngine.WhyCantCounter` for the screen): the step swaps in the counter, counts it, and asks the other side, up to `maxTradeCounters` times. A `TradeOffer` can also carry Opportunity cards (`GiveCards`, `GetCards`); `Reversed()` gives the same trade from the other side. Bot trade logic lives in `src/CityTycoon.AI/EasyBot.Trades.cs`: one `Gain` sum for accepting, offering and countering; `GroupEarnings` (rent plus `PerkWorth` until the game ends) says what a color group is worth, and `GroupsEarnings` shares one house budget between groups a trade finishes together (decision #149); `Limit` finds the most the bot would pay; `Haggle` picks a counter halfway toward the other side, up to that limit (decision #148).

Every rule that can say "no" has a `WhyCant...` method that returns a plain-English reason (or `null` for "yes"). The screen uses the same methods to grey out buttons and explain why, so the screen and the engine can never disagree.

## Momentum and landmarks (Phase 4)

`Rules/MomentumRules.cs` is the one place that gives or spends Momentum tokens. `Gain` always respects the cap (2 by default), and `CanSpend` says no in Jail, so no other code has to remember those rules.

A turn now has a few more stages in `TurnStep`: choose how to move (normal roll or Precision Roll), keep two of three dice if it was a Precision Roll, then choose a Nudge (−2 to +2, or 0 for no Nudge), then move. Each stage is just another question the step asks and waits on, the same pattern as everything else.

Each landmark that asks a question is its own small step in `Steps/LandmarkSteps.cs` (`HospitalVisitStep`, `CasinoStep`, `StadiumStep`). `ResolveSpaceStep` pushes the right one onto the stack when you land. A Dice Duel is the one place where the Casino asks a **different** player a question (the opponent), and that works because a `PendingDecision` always names who must answer.

Rent changes from the Stadium (×2) and Jail (×½) live in `RentCalculator`, so every place that charges rent gets them for free.

## Cards (Phase 5)

Every Opportunity and Event card is **data** in `content/opportunity.json` and `content/events.json`. A card names an **effect type** (like `PayPercentOfCash`) and gives it settings (like `"percent": 10`). The code for each effect type lives in `src/CityTycoon.Engine/Cards/CardEffects.cs`, as one small class per effect.

So adding a card is usually just JSON. Audit and a made-up "Tax Inspector" (20% instead of 10%) both use `PayPercentOfCash` with different numbers. Only a brand-new *kind* of effect needs a new C# class. The test `AnExpansionFile_AddsACard_WithNoNewCode` proves this by loading an extra card file.

| Piece | Job |
| --- | --- |
| `Cards/ICardEffect.cs` | What every effect class can do: check its settings when the content loads (`Validate`), list the ways it can be played (`Plays`), and do its thing (`Apply`). |
| `Cards/CardEffects.cs` | The effect classes, plus one list inside `CardEffects` that maps each effect name to its class. It's an ordinary list on purpose: Blazor trims away code it thinks is unused, and looking classes up "by magic" (reflection) can break after trimming. |
| `Cards/CardRules.cs` | Decks, shuffling, drawing, discarding, and which cards can be played right now. |
| `Cards/ReactionWindow.cs` | A Reaction moment (Rent Strike, Insurance, Detour). Only players holding a matching card are asked, one at a time. |
| `Steps/CardPlayStep.cs` | Plays one card: first it opens an Insurance window if the card hurts someone, then it applies the effect. |
| `Steps/CardSteps.cs` | The questions cards ask: City Park, the Mall, Fire!, Tip-Off, the Dice Duel and the hand limit. |

Some effects need to change the thing they react to. Rent Strike halves a rent payment that's already being asked for, and Detour changes a move that's about to happen (it also undoes the mover's Nudge, so its numbers are counted from the dice roll). The effect gets a `Parent`: the step it was played under (the `PaymentStep` or the `TurnStep`), and changes that.

Opportunity cards are secret. The log only says "Angel drew an Opportunity card", and `PlayerView.MyHand` shows the cards to their owner only. Everyone else just sees how many cards each player holds.

## Rule modifiers and Government tiles (Phase 6)

Lots of things in City Tycoon *change* a rule: a Landfill lowers its neighbors' rent, a School makes building cheaper, and later characters, color perks and ordinances will do the same. Instead of writing `if (tile == "landfill")` inside the rent code, each of them is a **rule modifier** (`Modifiers/IRuleModifier.cs`, Section 20.6).

A modifier answers a few fixed questions, called **hook points**: "how much does this property's rent change?", "how much cheaper is building here?", "can a card target this?", "is this safe from Fire!?". Each answer has a "no change" default, so a modifier only answers what it cares about. `RuleModifiers.Active` gathers every modifier in play, and the rent, building, card and Fire! code just asks them all. A new kind of modifier is a new class; the rent code never has to change. This is the **Open/Closed Principle**: open to new rules, closed to edits.

| Piece | Job |
| --- | --- |
| `Modifiers/IRuleModifier.cs` | The hook points, and `RuleModifiers`, which gathers the active modifiers and adds up their answers. |
| `Tiles/TileModifier.cs` | A revealed tile's neighbor effect. One class serves all 7 tiles; the numbers come from the tile's `"neighbor"` settings in `govtiles.json`. Hidden tiles aren't in the list at all, so they can't have an effect. |
| `Tiles/TileLandings.cs` | Landing effects (pay the bank, gain a token, ride to a railroad...), registered like card effects: a tile names a type plus settings in JSON. |
| `Tiles/TileRules.cs` | Finds a tile's two neighbors, and reveals a tile when someone lands on it. |
| `Steps/TileSteps.cs` | The Transit Hub's two questions: hop instead of paying rent, or ride to a railroad. |

`RentCalculator.Explain` works out rent one step at a time and keeps a line for each step ("Base rent $10", "City Hall next door +25%", "Stadium boost ×2"). The game uses the total; the screen shows the lines, so players can see *why* rent is what it is.

## Perks, bonuses and characters (Phase 7)

Phase 6 built the rule-modifier idea for Government tiles. Phase 7 reuses it for three more sources of rule changes, with no new engine code per rule:

| Source | File | Who gets it |
| --- | --- | --- |
| Color perks | `content/groups.json` (each group's `perk`) | A player who owns the whole group outright. |
| Railroad and utility bonuses | `content/bonuses.json` | A player who owns enough railroads or utilities. |
| Characters | `content/characters.json` | The player who picked or was dealt it. |

Each entry has a list of `modifiers`, such as `{ "type": "GoCash", "value": 50 }`. The `type` is the name of a small class in `src/CityTycoon.Engine/Modifiers/` (`PerkModifiers.cs`, `BonusModifiers.cs`, `CharacterModifiers.cs`), and `value` is its number. `ModifierTypes.cs` is a hand-written list that connects the names to the classes, so the game still works after Blazor trims unused code. To add a new perk you edit the JSON; a new *kind* of rule change is one new class plus one line in that list.

A modifier class only overrides the hooks it cares about (the "no change" answers are the defaults in `IRuleModifier`). One C# detail worth knowing: because each class sits on top of the shared `OwnedModifier` base class, it has to say `, IRuleModifier` again in its own declaration. Without that, C# quietly keeps using the interface's "do nothing" default and the perk never switches on. The tests catch this, because every perk has a test that checks its effect.

`RuleModifiers` is still the only place the rest of the engine asks questions ("how many loans may this player hold?"). Because it is asked all the time, it keeps its answer until something changes: who owns what, which tiles are revealed, or who is bankrupt (`ModifierCache`).

New things the player is asked, each a step on the resolution stack: `CharacterPickStep` (reverse turn order), `PeekTileStep` (City Planner), `KeepOneCardStep` (Pink), plus the Hustler's reroll question inside the Casino and Dice Duel steps. Express Line and Hostile Takeover are extra choices at the start of a turn (`TurnStep.MoveChoices`).

The Sim (`--characters none|random`) prints a win rate for each character and flags any that wins more than 5 points above fair share.

## The City Council (Phase 8)

Sessions, votes and ordinances follow the same pattern as cards: the **content** is JSON and the **behavior** is a small class.

- `content/ordinances.json` lists the 14 ordinance kinds (17 cards with `copies`). An entry gives a `name`, `text`, optional `target: "district"`, a `duration` in rounds (or `lasting: true`), and an `effect` that names an effect type plus its settings.
- `src/CityTycoon.Engine/Ordinances/OrdinanceEffects.cs` is the explicit list of effect types (`Rezone`, `AddPermits`, `BuildingMoratorium`, `DistrictRent`, `RailroadRent`, `TaxPerLevel`, `InfrastructureBond`, `CollectFromBank`, `Preserve`, `CasinoCap`, `RevealAllTiles`). Each answers: *can this be picked right now?*, *which districts?*, *what happens when it passes?*, and, for timed ones, *which rule change does it make while it lasts?*
- A passed ordinance with a duration goes into `GameState.ActiveOrdinances`. `RuleModifiers.Active` turns each into a normal rule modifier (rent percentages reuse the `RentPercent` hook; two new hooks, `BuildingBlockedReason` and `MaxCasinoStake`, cover building and the Casino). The modifier cache's signature includes the active ordinances, so the cache refreshes when one starts or ends.
- `CouncilStep` (`Steps/CouncilSteps.cs`) runs a session as stages: start (find the agenda setter, draw two ordinances), pick ordinance, pick district, vote, resolve. Because a step keeps its progress in plain properties, a session can be saved and loaded halfway through, like every other step.
- The vote is a question put to every player at once (like sealed bids). Votes are only counted when all are in. The roll comes from `IDiceSource.RollBelow`, so tests can script it (`ScriptedDice.AddCouncilRoll`).
- `GameStep` starts a Regular session after the end-of-round timers tick; City Hall and the Emergency Session card push an Emergency one. `RoundEndHandled` stops the end-of-round work from running twice when the game comes back after a session.

In the browser, `CouncilPanel` asks the three questions (agenda, district, vote). When a vote has been counted, `GameSession` pauses the game and shows `CouncilReveal`, a full-screen box with every vote, the odds meter and the roll, until someone clicks Continue (games with no humans, and Fast bots, skip the pause). The meter's animation switches off when the player asks their device for reduced motion.

The Sim prints votes per game, the pass rate, lobbying spent, and how often each ordinance passed.

## The browser app (Phase 2)

The Web project draws the game and passes clicks to the engine. It never changes the game state itself.

| Piece | Job |
| --- | --- |
| `Pages/Home.razor` | The menu and new-game setup screen. |
| `Pages/Play.razor` | The game screen: board, player panels, decision panel, log. |
| `Components/*.razor` | Small reusable pieces of screen (a board space, a property card, the auction panel...). A **component** is a C# class with its own HTML, and Blazor calls these `.razor` files. |
| `Services/GameSession.cs` | Starts a game, gives each seat a controller, runs the `GameHost` in the background, and tells the pages to redraw when something changes. |
| `Services/HumanUiController.cs` | The controller for a person. |
| `Components/PropertyActions.razor` | Build, sell, mortgage and unmortgage buttons for one of your properties (Phase 3). |
| `Components/TradeBuilder.razor` | The "offer a trade" form (Phase 3). With `CounterTo` set it builds a counter-offer instead, starting from the offer being answered. |
| `Components/CasinoPanel.razor` | The Casino: bet High/Low or challenge someone to a Dice Duel (Phase 4). |
| `Components/HandPanel.razor`, `CardView.razor` | Your Opportunity cards with their Play buttons, and how one card looks (Phase 5). |

While you choose a Nudge or Precision dice, `Play.razor` works out every space you could land on and gives them to the board, which outlines them. Pointing at a button outlines its space more strongly, so you can see where you'd land before you click.

### How a click reaches the engine

A bot answers a question instantly. A person can't: they have to read the screen and click. `HumanUiController` handles this with a `TaskCompletionSource`, which is like an IOU for an answer:

1. The game host asks the human controller a question.
2. The controller hands the question to the page (so the buttons appear) and gives the host an IOU: a `Task` that isn't finished yet. The host `await`s it, which means "pause here until it's ready" without freezing the browser.
3. The person clicks a button. The page calls `Answer(...)`, which fills in the IOU.
4. The host wakes up with the answer and submits it to the engine.

Because the engine only ever sees "a controller answered", bots, people on this screen, and (later) people online all work the same way.

### Keeping the shared screen fair

On one shared screen, the board and log are drawn from a **spectator view**: the same `PlayerView` a player gets, but for nobody in particular, so it holds no secrets. The auction panel uses the bidding player's own view. In Hidden mode with two or more humans, the pass-the-device screen covers everything until the right person taps "show me". Since Phase 5 every question can show that person's secret cards, so the pass screen appears whenever the next question is for a different person.

## Polish: saves, settings and the rules page (Phase 9)

- `SaveGame.cs` turns the engine state into JSON (`SavedGame`) and back. `GameSession.Resume` starts a game from one, sharing `Launch` with `Start`.
- `SettingsService` keeps `UserSettings` and applies them through `wwwroot/js/app.js` (`window.ct`), which sets CSS variables like `--text-scale` and `--line-spacing`.
- `SpeechService` and `SpeakButton` read text aloud with the browser's speech feature.
- `RulesReference` builds the text of the `/rules` page from the content files, so card text stays in one place.
- `NetWorth.Breakdown` splits net worth into parts. `PlayerView.NetWorthBreakdowns` hands it to the end screen.
- Tests: `SaveLoadTests` saves and reloads after every step of a full game and checks the game continues the same way.

## Bots: Easy, Normal and Hard (Phase 10)

- `EasyBot` answers every kind of decision. `NormalBot` and `HardBot` inherit from it and override a few `virtual` methods: `Reserve`, `PropertyWorth`, `SealedAmount`, `PickBuild`, `Vote`, `NudgeThreshold`.
- The estimates (set bonus, blocking, vote impact) use only the bot's `PlayerView`.
- Blocking lives in `EasyBot` (`WithBlocking`, `RivalCompleting`, `MostTheyCouldPay`), so every level bids a little extra to stop one opponent finishing a color group (decision #135).
- The Sim has mixed-table modes so one kind of bot can be measured against another. Results are written up in `docs/BALANCE.md`.

## Replaying moves so they are easy to follow (Web)

- `Services/Playback.cs` takes the events a step produced and shows them one at a time. `GameSession.RunAsync` calls `Prepare` before each step and `PlayAsync` after it.
- `Playback.Apply` turns the real `PlayerView` into the view the screen draws while a replay is running. `GameSession.PublicView` goes through it, so every component shows the replayed state without knowing about it.
- Money, ownership and positions are worked out from events only for the common cases. If an event is not covered, the screen simply catches up when the replay ends.
- New pieces: `NoticeStack` (notices at the top of the board that fade, card pop-up, skip button), tumbling dice in `DiceView` (each die is a `Die`), space flashes in `SpaceCell`, and money pop-ups in `PlayerPanels`. Their CSS is at the end of `app.css`.
- `Playback.Notices` keeps the last three notices; `Show` adds one. `Look` gives each event an icon name (for `Icon`), a tone, a card and a sound.
- Sounds: `Services/SoundService.cs` calls `ct.sound` in `wwwroot/js/app.js`, which makes each sound with Web Audio. Playback asks for a sound per event; Settings has the on/off switch and volume.
- To replay a new kind of event: add its pause in `PauseFor`, its icon and sound in `Look`, and (if it moves money or changes a property) its entry in `MoneyChanges` or `ApplyBoard`.

## The look (Web)

- All colors are CSS variables at the top of `app.css`. Night redefines them under `html[data-theme="night"]`, and again for "Match my device" on a dark device. `ct.applySettings` sets `data-theme` from Settings > Look.
- `Components/Icon.razor` draws every picture as inline SVG by name (`dice`, `coin`, `jail`...). Add a new picture as a new `case` there.
- The board size is one variable, `--cell` (one square). It is worked out from the window size, so the whole board fits on screen.

## Property history and the money ledger (Web + Engine)

Both are worked out from the game log (`State.Log`), never stored separately. `Events/GameHistory.cs` walks the log once: `Ledger` turns every money event into one line per player (a payment between two players becomes two lines, out and in), starting with their starting cash, so a player's lines always add up to their cash. `ForSpace` collects what happened to one space and who got it last, and how. Both take a `PlayerView`, like `EventDescriber`, so they can only use what every player may see.

A new event that moves money must be added to `GameHistory.Ledger`, or the test `EveryDollar_IsInTheLedger` (AI tests, plays bot games) fails. On screen: `Components/SpaceHistoryPanel.razor` (inside `PropertyCard` and the space details panel) and `Components/LedgerDialog.razor` (opened by the letter circles in `PlayerPanels`).
