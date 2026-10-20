# City Tycoon

A virtual board game inspired by Monopoly, written in C#. The full rules and the build plan are in [docs/GAME_DESIGN.md](docs/GAME_DESIGN.md).

## What you need

- **Visual Studio 2026** (the free Community edition is fine) with the **"ASP.NET and web development"** workload ticked in the Visual Studio Installer. That workload includes the .NET 10 SDK and everything Blazor needs.
- Or, without Visual Studio: the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and any editor.

## Running it in Visual Studio

1. Open `CityTycoon.sln` (File > Open > Project/Solution).
2. In **Solution Explorer**, right-click the project you want to run and pick **Set as Startup Project**:
   - `CityTycoon.Web` opens the game in your browser.
   - `CityTycoon.Sim` runs bot-vs-bot games in a console window.
3. Press **F5** (run with the debugger) or **Ctrl+F5** (run without it).

## Playing

Run `CityTycoon.Web` and the game opens in your browser. On the **New game** screen, pick 2 to 6 players and make each one a **Human** or an **Easy bot**, then press **Start game**.

- Click the big buttons in the middle of the board to roll, bid and end your turn.
- Click any space on the board to see its rent table.
- With two or more humans in **Hidden** mode, a "Pass to ..." screen covers the board before each secret bid, so the next person can take the device without anyone peeking.
- Tick **Fast bots** to stop the short pause between bot moves.

Right now the game has movement, auctions, rent, taxes, Jail and scoring. Buildings, cards, Government tiles, characters and the City Council arrive in later phases (see Section 29 of the design doc).

## Running the tests in Visual Studio

Open **Test > Test Explorer** and click **Run All Tests** (the double green arrow). Every test should show a green tick.

## Running from a terminal (optional)

```bash
dotnet build                                  # build everything
dotnet test                                   # run every test
dotnet run --project src/CityTycoon.Web       # start the game, then open the address it prints
dotnet run --project src/CityTycoon.Sim       # run bot games
```

## How the folders fit together

| Folder | What's in it |
| --- | --- |
| `content/` | JSON files with every game number and all the game content (board, properties, cards). Change a number here, not in the code. |
| `src/CityTycoon.Engine/` | The rules engine: the "referee" of the game. No screens, no internet code. |
| `src/CityTycoon.AI/` | Computer (bot) players. |
| `src/CityTycoon.Sim/` | A console app that plays lots of bot games to check balance. |
| `src/CityTycoon.Web/` | The game you play in the browser (Blazor). |
| `tests/` | Automatic tests that check the rules work. |
| `docs/` | The design doc, a log of decisions, and an explanation of the architecture. |

[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) explains *why* it's split up this way.
