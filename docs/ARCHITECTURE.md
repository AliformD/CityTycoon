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
