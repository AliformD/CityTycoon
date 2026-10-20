using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Cards;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.Ordinances;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Steps;

public enum CouncilStage { Start, PickOrdinance, PickDistrict, Vote, Resolve, Finish }

/// <summary>One player's sealed vote, kept until everyone has voted.</summary>
public sealed class CouncilVoteRecord
{
    public VoteChoice Choice { get; set; }
    public int Lobby { get; set; }

    /// <summary>The Lobbyist card this player plays with their vote, or null.</summary>
    public string? LobbyistCardId { get; set; }
}

/// <summary>
/// A City Council session (Section 11): draw two ordinances, the agenda setter picks one (and a
/// district if needed), everyone votes in secret and lobbies, then a roll decides.
/// The step moves through its <see cref="Stage"/>s; each time it runs it does the work of the
/// current stage, and waits for answers where it needs them.
/// </summary>
public sealed class CouncilStep : Step
{
    /// <summary>True for an Emergency session (City Hall, or the Emergency Session card).</summary>
    public bool Emergency { get; set; }

    /// <summary>For an Emergency session: the player who caused it. They set the agenda.</summary>
    public int TriggeredById { get; set; }

    public CouncilStage Stage { get; set; }
    public int SetterId { get; set; }

    /// <summary>The ordinances the setter can pick from (the ones that could do something right now).</summary>
    public List<string> Options { get; set; } = [];

    public string? OrdinanceId { get; set; }
    public string? DistrictId { get; set; }

    /// <summary>Sealed votes, by player ID. They are only counted once everyone has voted.</summary>
    public Dictionary<int, CouncilVoteRecord> Votes { get; set; } = [];

    public override StepResult Run(EngineContext ctx)
    {
        switch (Stage)
        {
            case CouncilStage.Start:
                return Start(ctx);

            case CouncilStage.PickOrdinance:
                return StepResult.WaitFor(new PendingDecision(SetterId, DecisionKind.ChooseOrdinance,
                    "You set the agenda. Pick one ordinance to put to a vote. The other goes to the bottom of the deck.",
                    Ordinances: Options.ToList()));

            case CouncilStage.PickDistrict:
                IOrdinanceEffect effect = EffectOf(ctx);
                return StepResult.WaitFor(new PendingDecision(SetterId, DecisionKind.ChooseDistrict,
                    $"Pick the district for {Definition(ctx).Name}.", OrdinanceId: OrdinanceId,
                    Districts: effect.Districts(ctx, Definition(ctx)).ToList()));

            case CouncilStage.Vote:
                var waiting = ctx.State.ActivePlayers.Where(p => !Votes.ContainsKey(p.Id)).ToList();
                if (waiting.Count == 0)
                {
                    Stage = CouncilStage.Resolve;
                    return StepResult.Continue;
                }
                return StepResult.WaitFor(waiting.Select(p => new PendingDecision(p.Id, DecisionKind.CouncilVote,
                    $"Vote on {Definition(ctx).Name}. Your vote and your lobbying are secret until everyone has voted.",
                    OrdinanceId: OrdinanceId, DistrictId: DistrictId,
                    MaxLobby: Math.Max(0, p.Cash), HasLobbyist: LobbyistIn(ctx, p) != null)));

            case CouncilStage.Resolve:
                return Resolve(ctx);

            default:
                return StepResult.Done;
        }
    }

    // ---------------------------------------------------------- starting a session

    private StepResult Start(EngineContext ctx)
    {
        GameState state = ctx.State;
        SetterId = Emergency && !ctx.Player(TriggeredById).IsBankrupt ? TriggeredById : LowestNetWorth(ctx);

        Options = DrawOptions(ctx);
        if (Options.Count == 0)
        {
            ctx.Emit(new CouncilCancelled(state.OrdinanceDeck.Count + state.OrdinanceDiscard.Count == 0
                ? "there are no ordinances to vote on."
                : "no ordinance in the deck could change anything right now."));
            Stage = CouncilStage.Finish;
            return StepResult.Done;
        }

        ctx.Emit(new CouncilStarted(Emergency, SetterId));
        if (Options.Count == 1)
        {
            OrdinanceId = Options[0];
            Options.Clear();
            return AfterOrdinanceChosen(ctx);
        }
        Stage = CouncilStage.PickOrdinance;
        return StepResult.Continue;
    }

    /// <summary>
    /// The lowest net worth sets the agenda. A tie goes to whoever comes next in turn order after the
    /// player whose turn it is (Section 11.1), so the current player is the very last to be picked.
    /// </summary>
    private static int LowestNetWorth(EngineContext ctx)
    {
        GameState state = ctx.State;
        int count = state.Players.Count;
        int Distance(int index)
        {
            int d = (index - state.ActivePlayerIndex + count) % count;
            return d == 0 ? count : d;
        }
        return state.Players
            .Select((p, index) => (Player: p, Index: index))
            .Where(x => !x.Player.IsBankrupt)
            .OrderBy(x => NetWorth.Calculate(state, ctx.Content, x.Player.Id))
            .ThenBy(x => Distance(x.Index))
            .First().Player.Id;
    }

    /// <summary>
    /// Draws two ordinances and keeps the ones that could do something (Section 11.2). An ordinance
    /// that can't be picked goes to the bottom of the deck. If neither can be picked, draw again.
    /// </summary>
    private static List<string> DrawOptions(EngineContext ctx)
    {
        GameState state = ctx.State;
        int attempts = (state.OrdinanceDeck.Count + state.OrdinanceDiscard.Count) / 2 + 3;
        for (int i = 0; i < attempts; i++)
        {
            EnsureOrdinances(ctx, 2);
            int take = Math.Min(2, state.OrdinanceDeck.Count);
            if (take == 0)
                return [];
            var drawn = state.OrdinanceDeck.Take(take).ToList();
            state.OrdinanceDeck.RemoveRange(0, take);

            var pickable = drawn.Where(id => CanPick(ctx, ctx.Content.Ordinance(id))).ToList();
            foreach (string id in drawn.Where(id => !pickable.Contains(id)))
                state.OrdinanceDeck.Add(id);
            if (pickable.Count > 0)
                return pickable;
        }
        return [];
    }

    /// <summary>Makes sure the draw pile has enough cards, shuffling the discard pile in underneath when it runs low.</summary>
    private static void EnsureOrdinances(EngineContext ctx, int wanted)
    {
        GameState state = ctx.State;
        if (state.OrdinanceDeck.Count >= wanted || state.OrdinanceDiscard.Count == 0)
            return;
        state.Rng.Shuffle(state.OrdinanceDiscard);
        state.OrdinanceDeck.AddRange(state.OrdinanceDiscard);
        state.OrdinanceDiscard.Clear();
    }

    /// <summary>True if the ordinance could change something right now (and has a district to aim at, if it needs one).</summary>
    public static bool CanPick(EngineContext ctx, OrdinanceDefinition ordinance)
    {
        IOrdinanceEffect effect = OrdinanceEffects.Find(ordinance.EffectType)!;
        return effect.CanPick(ctx, ordinance)
            && (ordinance.Target != OrdinanceTarget.District || effect.Districts(ctx, ordinance).Count > 0);
    }

    private StepResult AfterOrdinanceChosen(EngineContext ctx)
    {
        OrdinanceDefinition def = Definition(ctx);
        if (def.Target == OrdinanceTarget.District)
        {
            var districts = EffectOf(ctx).Districts(ctx, def);
            if (districts.Count == 1)
                DistrictId = districts[0];
            else
            {
                Stage = CouncilStage.PickDistrict;
                return StepResult.Continue;
            }
        }
        return BeginVote(ctx);
    }

    private StepResult BeginVote(EngineContext ctx)
    {
        ctx.Emit(new OrdinanceProposed(SetterId, OrdinanceId!, DistrictId));
        Stage = CouncilStage.Vote;
        return StepResult.Continue;
    }

    // ---------------------------------------------------------- answers

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) => decision.Kind switch
    {
        DecisionKind.ChooseOrdinance => Options.Select(id => (GameAction)new ChooseOrdinance(id)).ToList(),
        DecisionKind.ChooseDistrict => (decision.Districts ?? []).Select(id => (GameAction)new ChooseDistrict(id)).ToList(),
        // Any lobbying amount is legal too (see IsLegal). These are examples.
        _ => [new CastVote(VoteChoice.Yes), new CastVote(VoteChoice.No), new CastVote(VoteChoice.Abstain)],
    };

    public override bool IsLegal(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        if (decision.Kind != DecisionKind.CouncilVote)
            return LegalActions(ctx, decision).Contains(action);
        if (action is not CastVote vote)
            return false;
        PlayerState player = ctx.Player(decision.PlayerId);
        if (vote.Lobby < 0 || vote.Lobby > Math.Max(0, player.Cash))
            return false;
        if (vote.PlayLobbyist && LobbyistIn(ctx, player) == null)
            return false;
        // Abstaining means no lobbying (Section 11.3).
        return vote.Choice != VoteChoice.Abstain || (vote.Lobby == 0 && !vote.PlayLobbyist);
    }

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        switch (action)
        {
            case ChooseOrdinance chosen:
                OrdinanceId = chosen.OrdinanceId;
                // The one that wasn't picked goes to the bottom of the deck.
                foreach (string other in Options.Where(id => id != chosen.OrdinanceId))
                    ctx.State.OrdinanceDeck.Add(other);
                Options.Clear();
                AfterOrdinanceChosen(ctx);   // moves on to the district or the vote
                break;

            case ChooseDistrict district:
                DistrictId = district.DistrictId;
                BeginVote(ctx);
                break;

            case CastVote vote:
                Votes[decision.PlayerId] = new CouncilVoteRecord
                {
                    Choice = vote.Choice,
                    Lobby = vote.Lobby,
                    LobbyistCardId = vote.PlayLobbyist ? LobbyistIn(ctx, ctx.Player(decision.PlayerId)) : null,
                };
                break;
        }
    }

    // ---------------------------------------------------------- counting the votes

    private StepResult Resolve(EngineContext ctx)
    {
        GameState state = ctx.State;
        OrdinanceDefinition def = Definition(ctx);
        int yes = ctx.Rules.PublicOpinionInfluence, no = ctx.Rules.PublicOpinionInfluence;
        var reveals = new List<VoteReveal>();

        foreach (PlayerState player in state.ActivePlayers.Where(p => Votes.ContainsKey(p.Id)))
        {
            CouncilVoteRecord vote = Votes[player.Id];
            int influence = 0;
            if (vote.Choice != VoteChoice.Abstain)
            {
                influence = RuleModifiers.VoteInfluence(state, ctx.Content, player.Id) + vote.Lobby;
                if (vote.LobbyistCardId is string cardId)
                {
                    influence += ctx.Content.Card(cardId).Int("amount");
                    player.Hand.Remove(cardId);
                    CardRules.Discard(ctx, cardId);
                }
                // All lobbying money goes to the bank, whether the ordinance passes or fails.
                player.Cash -= vote.Lobby;
                if (vote.Choice == VoteChoice.Yes)
                    yes += influence;
                else
                    no += influence;
            }
            reveals.Add(new VoteReveal(player.Id, vote.Choice, vote.Choice == VoteChoice.Abstain ? 0 : vote.Lobby, vote.LobbyistCardId != null, influence));
        }

        int roll = ctx.Dice.RollBelow(yes + no);
        bool passed = roll < yes;
        ctx.Emit(new CouncilResolved(def.Id, DistrictId, reveals, yes, no, roll, passed));
        state.OrdinanceDiscard.Add(def.Id);

        if (passed)
        {
            if (def.StaysActive)
                OrdinanceRules.Activate(state, def, DistrictId);
            EffectOf(ctx).Apply(new OrdinanceContext(ctx, def, SetterId, DistrictId));
        }
        Stage = CouncilStage.Finish;
        return StepResult.Continue;
    }

    // ---------------------------------------------------------- small helpers

    private OrdinanceDefinition Definition(EngineContext ctx) => ctx.Content.Ordinance(OrdinanceId!);

    private IOrdinanceEffect EffectOf(EngineContext ctx) => OrdinanceEffects.Find(Definition(ctx).EffectType)!;

    /// <summary>A Lobbyist card in this player's hand, or null (Section 11.3).</summary>
    public static string? LobbyistIn(EngineContext ctx, PlayerState player) =>
        player.Hand.FirstOrDefault(id => ctx.Content.Card(id).EffectType == "AddInfluence");
}

/// <summary>
/// The agenda setter breaks a tie between districts (for example the Infrastructure Bond when several
/// districts share the lowest zoning). Pushed by the ordinance, answered like any other choice.
/// </summary>
public sealed class PickDistrictStep : Step
{
    public int PlayerId { get; set; }
    public string OrdinanceId { get; set; } = "";
    public List<string> Districts { get; set; } = [];
    public bool Answered { get; set; }

    public override StepResult Run(EngineContext ctx) => Answered
        ? StepResult.Done
        : StepResult.WaitFor(new PendingDecision(PlayerId, DecisionKind.ChooseDistrict,
            $"Several districts are tied for the lowest zoning. Pick the one that goes up a level.",
            OrdinanceId: OrdinanceId, Districts: Districts.ToList()));

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) =>
        Districts.Select(id => (GameAction)new ChooseDistrict(id)).ToList();

    public override void Receive(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        Answered = true;
        OrdinanceRules.ChangeZoning(ctx, ((ChooseDistrict)action).DistrictId, 1, OrdinanceId);
    }
}
