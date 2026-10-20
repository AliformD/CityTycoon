using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Board;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Randomness;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Steps;
using CityTycoon.Engine.Views;

namespace CityTycoon.Engine;

/// <summary>What happened when a player submitted an action.</summary>
public sealed record ActionResult(bool Accepted, string? Error, IReadOnlyList<GameEvent> Events)
{
    public static ActionResult Rejected(string error) => new(false, error, []);
}

/// <summary>
/// The referee (Section 20.1). It holds the game state and answers two questions:
/// "what decision is the game waiting for, and from whom?" and
/// "player X chose Y: what happens now?".
///
/// Nothing outside the engine changes the game state. The UI, bots and the
/// future online server only ever answer pending decisions.
/// </summary>
public sealed class GameEngine
{
    private readonly EngineContext ctx;

    public GameState State { get; }
    public GameContent Content { get; }

    /// <summary>Continues a game from a state (for example a loaded save).</summary>
    public GameEngine(GameState state, GameContent content, IDiceSource? dice = null)
    {
        State = state;
        Content = content;
        ctx = new EngineContext(state, content, dice ?? new RngDiceSource(state.Rng));
    }

    /// <summary>Sets up a brand-new game (Section 6) and runs it until the first decision.</summary>
    public static GameEngine NewGame(GameSetup setup, GameContent? content = null, IDiceSource? dice = null)
    {
        content ??= GameContent.Default;
        RulesConfig rules = content.Rules;
        if (setup.Players.Count < rules.MinPlayers || setup.Players.Count > rules.MaxPlayers)
            throw new ArgumentException($"A game needs {rules.MinPlayers} to {rules.MaxPlayers} players.", nameof(setup));

        ulong seed = setup.Seed ?? (ulong)System.Random.Shared.NextInt64();
        var state = new GameState
        {
            Settings = new GameSettings
            {
                Seed = seed,
                Mode = setup.Mode,
                MaxRounds = setup.MaxRounds ?? rules.TimedModeDefaultRounds,
                Privacy = setup.Privacy,
            },
            Rng = new Pcg32(seed),
        };

        // Create the players. Their ID is their seat number.
        for (int seat = 0; seat < setup.Players.Count; seat++)
        {
            state.Players.Add(new PlayerState
            {
                Id = seat,
                Name = setup.Players[seat].Name,
                Cash = rules.StartingCash,
                Momentum = rules.StartingMomentum,
            });
        }

        // Decide turn order randomly (Section 6, step 5).
        if (setup.ShuffleTurnOrder)
            state.Rng.Shuffle(state.Players);

        // Generate the board (step 7) and register every property as owned by the bank.
        state.Board = BoardGenerator.Generate(content, state.Rng);
        foreach (BoardSpace space in state.Board.Where(s => s.Kind == SpaceKind.Property))
            state.Properties[space.Id] = new PropertyState { PropertyId = space.Id };

        // Starting zoning (step 8): every district starts Residential, except
        // one random district that starts Commercial.
        var districts = content.Board.Districts.Select(d => d.Id).ToList();
        foreach (string district in districts)
            state.Zoning[district] = rules.StartingZoning;
        state.Rng.Shuffle(districts);
        foreach (string district in districts.Take(rules.StartingCommercialDistricts))
            state.Zoning[district] = ZoningLevel.Commercial;

        // Permits (step 9): the supply depends on the player count.
        PermitSupply permits = rules.PermitsFor(setup.Players.Count);
        state.HotelPermits = permits.HotelPermits;
        state.SkyscraperPermits = permits.SkyscraperPermits;

        state.Log.Add(new GameStarted(seed, state.Players.Select(p => p.Id).ToList()));
        state.Stack.Add(new GameStep());

        var engine = new GameEngine(state, content, dice);
        engine.RunUntilInputNeeded();
        return engine;
    }

    /// <summary>Who needs to decide what right now. Several at once for sealed bids.</summary>
    public IReadOnlyList<PendingDecision> GetPendingDecisions() => State.Pending;

    /// <summary>Every legal option for this player right now, for UI buttons and bots.</summary>
    public IReadOnlyList<GameAction> GetLegalActions(int playerId)
    {
        PendingDecision? decision = DecisionFor(playerId);
        if (decision == null)
            return [];
        return [.. TopStep.LegalActions(ctx, decision), .. ManagementRules.LegalActions(ctx, playerId, decision.Management)];
    }

    /// <summary>True if this player may offer a trade right now (trades can't be listed in GetLegalActions).</summary>
    public bool CanProposeTrade(int playerId) =>
        DecisionFor(playerId) is { } decision && ManagementRules.CanProposeTrades(ctx, playerId, decision.Management);

    /// <summary>
    /// Why a management action (build, sell, mortgage...) isn't allowed right now,
    /// or null if it is. The screen uses this to explain greyed-out buttons.
    /// </summary>
    public string? WhyIllegal(int playerId, ManagementAction action) =>
        DecisionFor(playerId) is { } decision
            ? ManagementRules.WhyIllegal(ctx, playerId, decision.Management, action)
            : "The game isn't waiting for you.";

    /// <summary>Checks the choice is legal, applies it, and returns what happened.</summary>
    public ActionResult Submit(int playerId, GameAction action)
    {
        if (State.IsOver)
            return ActionResult.Rejected("The game is over.");

        PendingDecision? decision = DecisionFor(playerId);
        if (decision == null)
            return ActionResult.Rejected($"The game isn't waiting for a choice from player {playerId}.");

        int logBefore = State.Log.Count;
        if (action is ManagementAction management)
        {
            // Build, sell, mortgage, loans and trades are handled in one place.
            // Afterwards the same step runs again and asks its question again.
            if (ManagementRules.WhyIllegal(ctx, playerId, decision.Management, management) is string why)
                return ActionResult.Rejected(why);
            ManagementRules.Apply(ctx, playerId, management);
            State.Pending.Clear();
            RunUntilInputNeeded();
            return new ActionResult(true, null, State.Log.Skip(logBefore).ToList());
        }

        Step step = TopStep;
        if (!step.IsLegal(ctx, decision, action))
            return ActionResult.Rejected($"{action} isn't a legal answer to {decision.Kind} right now.");

        step.Receive(ctx, decision, action);
        State.Pending.Clear();
        RunUntilInputNeeded();
        return new ActionResult(true, null, State.Log.Skip(logBefore).ToList());
    }

    /// <summary>What one player is allowed to see (Section 20.1).</summary>
    public PlayerView GetView(int viewerId) => PlayerView.Create(State, Content, viewerId);

    private PendingDecision? DecisionFor(int playerId) => State.Pending.FirstOrDefault(d => d.PlayerId == playerId);

    private Step TopStep => State.Stack[^1];

    /// <summary>
    /// The engine's main loop: keep running the step on top of the stack until
    /// one needs a player's choice, or the stack is empty (the game is over).
    /// </summary>
    private void RunUntilInputNeeded()
    {
        // A safety net: if a bug makes steps loop forever, fail clearly instead of freezing.
        const int maxRunsWithoutInput = 100_000;
        for (int runs = 0; runs < maxRunsWithoutInput; runs++)
        {
            if (State.Stack.Count == 0)
                return;

            Step top = TopStep;
            StepResult result = top.Run(ctx);
            switch (result.Status)
            {
                case StepStatus.Done:
                    // Remove this exact step. Anything it pushed stays on the stack and runs next.
                    State.Stack.Remove(top);
                    break;
                case StepStatus.Continue:
                    break;
                case StepStatus.Waiting:
                    State.Pending = result.Decisions.ToList();
                    return;
            }
        }
        throw new InvalidOperationException("The engine ran 100,000 steps without needing a decision. This is a bug.");
    }
}
