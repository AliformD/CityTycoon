using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Cards;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Modifiers;
using CityTycoon.Engine.Ordinances;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Steps;

/// <summary>The stages of a session. New ones go at the end, so saved games keep their numbers.</summary>
public enum CouncilStage { Start, PickOrdinance, PickDistrict, Vote, Resolve, Reverse, Finish, Deals, DealAnswers }

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
/// district if needed), the other players may bid for the Politician's vote, everyone votes in secret
/// (the Politician may lobby), then a roll decides.
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

    /// <summary>The result of the roll, kept while the City Planner decides whether to reverse it.</summary>
    public bool Passed { get; set; }

    /// <summary>The City Planner being asked whether to reverse the result, or null.</summary>
    public int? ReverserId { get; set; }

    /// <summary>The Politician other players bid to pay for their vote, or null for a plain vote (decision #162).</summary>
    public int? PoliticianId { get; set; }

    /// <summary>Everyone who may bid for the Politician's vote, in the order they are asked.</summary>
    public List<int> ContractBidders { get; set; } = [];

    /// <summary>Bidders who passed. Like an open auction, passing means you are out of this bidding.</summary>
    public List<int> ContractOut { get; set; } = [];

    /// <summary>Where in <see cref="ContractBidders"/> the next question starts.</summary>
    public int ContractTurn { get; set; }

    /// <summary>The highest offer so far, or null before anyone offers.</summary>
    public VoteDeal? TopOffer { get; set; }

    /// <summary>Accepted contracts, by the player who must vote that way (only ever the Politician).</summary>
    public Dictionary<int, VoteDeal> Deals { get; set; } = [];

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

            case CouncilStage.Deals:
                return AskForContract(ctx);

            case CouncilStage.DealAnswers:
                // The Politician takes the top offer or turns it down (decision #162).
                if (TopOffer is VoteDeal top && PoliticianId is int politician && !ctx.Player(politician).IsBankrupt
                    && ctx.Player(top.FromId).Cash >= top.Amount)
                {
                    return StepResult.WaitFor(new PendingDecision(politician, DecisionKind.AnswerVoteDeal,
                        $"{ctx.Player(top.FromId).Name} won the bidding: they offer you ${top.Amount} to vote {top.Choice.ToString().ToUpperInvariant()} on {Definition(ctx).Name}. If you take it, the money is yours, you must vote that way, and it adds {top.Amount * ContractPercent(ctx, politician) / 100} influence to your vote.",
                        OrdinanceId: OrdinanceId, DistrictId: DistrictId, OtherPlayerId: top.FromId, Deal: top));
                }
                Stage = CouncilStage.Vote;
                return StepResult.Continue;

            case CouncilStage.Vote:
                var waiting = ctx.State.ActivePlayers.Where(p => !Votes.ContainsKey(p.Id)).ToList();
                if (waiting.Count == 0)
                {
                    Stage = CouncilStage.Resolve;
                    return StepResult.Continue;
                }
                return StepResult.WaitFor(waiting.Select(p => new PendingDecision(p.Id, DecisionKind.CouncilVote,
                    Deals.TryGetValue(p.Id, out VoteDeal? bound)
                        ? $"Vote on {Definition(ctx).Name}. You took {ctx.Player(bound.FromId).Name}'s contract, so you vote {bound.Choice.ToString().ToUpperInvariant()}. You may still lobby."
                        : CanLobby(ctx, p.Id)
                            ? $"Vote on {Definition(ctx).Name}. Your vote and your lobbying are secret until everyone has voted."
                            : $"Vote on {Definition(ctx).Name}. Your vote is secret until everyone has voted.",
                    OrdinanceId: OrdinanceId, DistrictId: DistrictId,
                    MaxLobby: CanLobby(ctx, p.Id) ? Math.Max(0, p.Cash) : 0, HasLobbyist: LobbyistIn(ctx, p) != null, Deal: bound)));

            case CouncilStage.Resolve:
                return Resolve(ctx);

            case CouncilStage.Reverse:
                if (ReverserId is int planner)
                {
                    return StepResult.WaitFor(new PendingDecision(planner, DecisionKind.ReverseCouncilVote,
                        $"The vote on {Definition(ctx).Name} {(Passed ? "passed" : "failed")}. As the City Planner you may reverse it, so it {(Passed ? "fails" : "passes")} instead. You can do this once per game.",
                        OrdinanceId: OrdinanceId, DistrictId: DistrictId, Passed: Passed));
                }
                return Apply(ctx);

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
        // With a Politician in the game, the other players first bid for their vote (decision #162).
        GameState state = ctx.State;
        PoliticianId = ctx.Rules.VoteContracts
            ? state.ActivePlayers.FirstOrDefault(p => ContractPercent(ctx, p.Id) > 0)?.Id
            : null;
        Stage = CouncilStage.Vote;
        if (PoliticianId is int politician)
        {
            // Asked in turn order, starting with the player after the Politician.
            int count = state.Players.Count;
            int start = state.Players.FindIndex(p => p.Id == politician);
            ContractBidders = Enumerable.Range(1, count - 1)
                .Select(i => state.Players[(start + i) % count])
                .Where(p => !p.IsBankrupt).Select(p => p.Id).ToList();
            if (ContractBidders.Count > 0)
                Stage = CouncilStage.Deals;
        }
        return StepResult.Continue;
    }

    /// <summary>The smallest vote contract offer, and the step offers go up in.</summary>
    public const int MinDeal = 10;

    /// <summary>The least the next offer can be: $10, or $10 more than the top offer.</summary>
    public int MinNextOffer => TopOffer == null ? MinDeal : TopOffer.Amount + MinDeal;

    /// <summary>
    /// Vote contract bidding (decision #162), an open auction for the Politician's vote: in turn, each bidder may beat
    /// the top offer by at least $10, for either side, or pass and be out. It ends when everyone but the top bidder is out.
    /// </summary>
    private StepResult AskForContract(EngineContext ctx)
    {
        int politician = PoliticianId!.Value;
        int count = ContractBidders.Count;
        for (int i = 0; i < count; i++)
        {
            int id = ContractBidders[(ContractTurn + i) % count];
            if (ContractOut.Contains(id) || id == TopOffer?.FromId)
                continue;
            PlayerState player = ctx.Player(id);
            // Bankrupt, or too little cash to beat the top offer: out without being asked.
            if (player.IsBankrupt || player.Cash < MinNextOffer)
            {
                ContractOut.Add(id);
                continue;
            }
            ContractTurn = (ContractTurn + i) % count;
            string top = TopOffer is VoteDeal t
                ? $"The top offer is {ctx.Player(t.FromId).Name}'s ${t.Amount} to vote {t.Choice.ToString().ToUpperInvariant()}. Beat it by at least ${MinDeal}, or pass."
                : "Nobody has made an offer yet.";
            return StepResult.WaitFor(new PendingDecision(id, DecisionKind.OfferVoteDeal,
                $"Before the vote on {Definition(ctx).Name}, you may offer {ctx.Player(politician).Name} (the Politician) cash to vote your way. {top} If you pass, you are out of the bidding.",
                OrdinanceId: OrdinanceId, DistrictId: DistrictId, MinBid: MinNextOffer, MaxBid: player.Cash, BidIncrement: MinDeal,
                OtherPlayerId: politician, Deal: TopOffer));
        }
        Stage = CouncilStage.DealAnswers;
        return StepResult.Continue;
    }

    /// <summary>Influence each dollar paid into this player's vote buys, in percent, or 0 if they can't lobby (decision #162).</summary>
    private static int ContractPercent(EngineContext ctx, int playerId) => RuleModifiers.ContractPercent(ctx.State, ctx.Content, playerId);

    /// <summary>Only the Politician may lobby (decision #162).</summary>
    public static bool CanLobby(EngineContext ctx, int playerId) => ContractPercent(ctx, playerId) > 0;

    // ---------------------------------------------------------- answers

    public override IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) => decision.Kind switch
    {
        DecisionKind.ReverseCouncilVote => [new ReverseVote(), new NoThanks()],
        DecisionKind.ChooseOrdinance => Options.Select(id => (GameAction)new ChooseOrdinance(id)).ToList(),
        DecisionKind.ChooseDistrict => (decision.Districts ?? []).Select(id => (GameAction)new ChooseDistrict(id)).ToList(),
        DecisionKind.AnswerVoteDeal => [new AcceptVoteDeal(), new DeclineVoteDeal()],
        // Any deal within the rules is legal too (see IsLegal). This is the "no deal" answer.
        DecisionKind.OfferVoteDeal => [new NoThanks()],
        DecisionKind.CouncilVote when decision.Deal is VoteDeal bound => [new CastVote(bound.Choice)],
        // Any lobbying amount is legal too (see IsLegal). These are examples.
        _ => [new CastVote(VoteChoice.Yes), new CastVote(VoteChoice.No), new CastVote(VoteChoice.Abstain)],
    };

    public override bool IsLegal(EngineContext ctx, PendingDecision decision, GameAction action)
    {
        if (decision.Kind == DecisionKind.OfferVoteDeal && action is OfferVoteDeal offer)
        {
            VoteDeal d = offer.Deal;
            return d.FromId == decision.PlayerId && d.ToId == PoliticianId
                && d.Amount >= MinNextOffer && d.Amount % MinDeal == 0 && d.Amount <= ctx.Player(d.FromId).Cash
                && d.Choice != VoteChoice.Abstain;
        }
        if (decision.Kind != DecisionKind.CouncilVote)
            return LegalActions(ctx, decision).Contains(action);
        if (action is not CastVote vote)
            return false;
        PlayerState player = ctx.Player(decision.PlayerId);
        if (Deals.TryGetValue(decision.PlayerId, out VoteDeal? deal) && vote.Choice != deal.Choice)
            return false;
        if (vote.Lobby < 0 || vote.Lobby > Math.Max(0, player.Cash) || vote.Lobby > 0 && !CanLobby(ctx, player.Id))
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

            case ReverseVote:
                PlayerState planner = ctx.Player(decision.PlayerId);
                planner.VoteReversalUsed = true;
                Passed = !Passed;
                ctx.Emit(new CouncilReversed(planner.Id, OrdinanceId!, DistrictId, Passed));
                ReverserId = null;
                break;

            case NoThanks when decision.Kind == DecisionKind.ReverseCouncilVote:
                ReverserId = null;
                break;

            case OfferVoteDeal offer:
                TopOffer = offer.Deal;
                ctx.Emit(new VoteContractBid(offer.Deal));
                ContractTurn = (ContractBidders.IndexOf(decision.PlayerId) + 1) % ContractBidders.Count;
                break;

            case NoThanks when decision.Kind == DecisionKind.OfferVoteDeal:
                ContractOut.Add(decision.PlayerId);
                ContractTurn = (ContractBidders.IndexOf(decision.PlayerId) + 1) % ContractBidders.Count;
                break;

            case AcceptVoteDeal:
            {
                VoteDeal deal = TopOffer!;
                ctx.Player(deal.FromId).Cash -= deal.Amount;
                ctx.Player(deal.ToId).Cash += deal.Amount;
                Deals[deal.ToId] = deal;
                ctx.Emit(new VoteDealMade(deal));
                Stage = CouncilStage.Vote;
                break;
            }

            case DeclineVoteDeal:
                ctx.Emit(new VoteDealDeclined(TopOffer!));
                Stage = CouncilStage.Vote;
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
            int contract = Deals.TryGetValue(player.Id, out VoteDeal? deal) && deal.Choice == vote.Choice ? deal.Amount : 0;
            if (vote.Choice != VoteChoice.Abstain)
            {
                // Every vote counts the same. Each dollar the Politician lobbies or took in a contract adds to theirs (decision #162).
                influence = RuleModifiers.VoteInfluence(state, ctx.Content, player.Id)
                    + (vote.Lobby + contract) * ContractPercent(ctx, player.Id) / 100;
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
            reveals.Add(new VoteReveal(player.Id, vote.Choice, vote.Choice == VoteChoice.Abstain ? 0 : vote.Lobby, vote.LobbyistCardId != null, influence, contract));
        }

        int roll = ctx.Dice.RollBelow(yes + no);
        Passed = roll < yes;
        ctx.Emit(new CouncilResolved(def.Id, DistrictId, reveals, yes, no, roll, Passed));
        state.OrdinanceDiscard.Add(def.Id);

        // The City Planner may turn the result around, once per game (decision #152).
        ReverserId = state.ActivePlayers
            .FirstOrDefault(p => !p.VoteReversalUsed && RuleModifiers.VoteReversal(state, ctx.Content, p.Id) != null)?.Id;
        Stage = CouncilStage.Reverse;
        return StepResult.Continue;
    }

    /// <summary>The result is final: a passed ordinance takes effect.</summary>
    private StepResult Apply(EngineContext ctx)
    {
        OrdinanceDefinition def = Definition(ctx);
        if (Passed)
        {
            if (def.StaysActive)
                OrdinanceRules.Activate(ctx.State, def, DistrictId);
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
