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
- **AuctionStep** collects sealed bids from everyone at once, then reveals them together.
- **PaymentStep** moves money, and declares bankruptcy if you can't pay.

Because every step keeps its progress in properties, the **whole stack can be saved** in the middle of anything, even halfway through an auction, and loaded back later.

## Randomness you can replay

All randomness (dice, shuffles, board layout) comes from one seeded generator, `Pcg32`, stored in the game state. The same **seed** (a starting number) and the same choices always produce exactly the same game. That makes bugs easy to reproduce, and later lets every computer in an online game agree.

Tests swap the real dice for `ScriptedDice`, so a test can say "the next roll is 3 + 4". The engine only knows it has an `IDiceSource` (an interface), so it can't tell the difference. Handing a class the thing it needs, instead of letting it build its own, is called **dependency injection**.

## Controllers and the game host

Each seat at the table has an `IPlayerController`: a bot today, a person clicking in the browser from Phase 2, a person on another device later. `GameHost` asks the engine what it's waiting for and passes each question to the right controller. For sealed bids it collects every answer before handing any of them in, so nobody's bid can influence anyone else's.

## The browser app (Phase 2)

The Web project draws the game and passes clicks to the engine. It never changes the game state itself.

| Piece | Job |
| --- | --- |
| `Pages/Home.razor` | The menu and new-game setup screen. |
| `Pages/Play.razor` | The game screen: board, player panels, decision panel, log. |
| `Components/*.razor` | Small reusable pieces of screen (a board space, a property card, the auction panel...). A **component** is a C# class with its own HTML, and Blazor calls these `.razor` files. |
| `Services/GameSession.cs` | Starts a game, gives each seat a controller, runs the `GameHost` in the background, and tells the pages to redraw when something changes. |
| `Services/HumanUiController.cs` | The controller for a person. |

### How a click reaches the engine

A bot answers a question instantly. A person can't: they have to read the screen and click. `HumanUiController` handles this with a `TaskCompletionSource`, which is like an IOU for an answer:

1. The game host asks the human controller a question.
2. The controller hands the question to the page (so the buttons appear) and gives the host an IOU: a `Task` that isn't finished yet. The host `await`s it, which means "pause here until it's ready" without freezing the browser.
3. The person clicks a button. The page calls `Answer(...)`, which fills in the IOU.
4. The host wakes up with the answer and submits it to the engine.

Because the engine only ever sees "a controller answered", bots, people on this screen, and (later) people online all work the same way.

### Keeping the shared screen fair

On one shared screen, the board and log are drawn from a **spectator view**: the same `PlayerView` a player gets, but for nobody in particular, so it holds no secrets. The auction panel uses the bidding player's own view. In Hidden mode with two or more humans, the pass-the-device screen covers everything until the right person taps "show me".
