using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Views;

namespace CityTycoon.Web.Services;

/// <summary>One notice shown at the top of the board while the game replays what just happened.</summary>
/// <param name="Icon">The picture's name for the Icon component (dice, coin, jail...).</param>
/// <param name="Tone">"good", "bad" or "" (colors the notice).</param>
/// <param name="CardId">A face-up card to show (an Event card or a played card). Null for none.</param>
/// <param name="CardBack">True to show a face-down card instead (a private Opportunity draw).</param>
/// <param name="SpaceId">The board space the notice is about, for its "Show" button. Null for none.</param>
public sealed record Beat(int Number, string Icon, string Text, string Tone, string? CardId = null, bool CardBack = false, string? SpaceId = null);

/// <summary>A floating "+$200" or "-$50" over a player's card.</summary>
public sealed record MoneyPop(int Number, int PlayerId, int Amount);

/// <summary>
/// Slows the game down so people can follow it.
///
/// The Engine finishes a whole move in one go: a roll, the walk, the rent, all at once. After each
/// step, the session hands the new events to this class. It then "replays" them one at a time: the dice
/// tumble, the token walks space by space, money floats up and down, cards flip over. While it replays,
/// <see cref="Apply"/> gives the screen a view where the log, the token positions, cash and
/// property owners show only what has been replayed so far. When the replay ends, the screen snaps back
/// to the true state, so a mistake in here can never change the game, only how it is drawn.
/// </summary>
public sealed class Playback(SettingsService settings, SoundService sounds, GameContent content, Action changed)
{
    private readonly Random cosmetic = new();   // only for the tumbling dice faces; never touches the game's own random numbers
    private PlayerView? live;

    private int logBase;
    private int revealed;
    private bool eventRevealed;
    private Dictionary<int, int> positions = [];
    private Dictionary<int, int> cash = [];
    private Dictionary<string, PropertyInfo> properties = [];
    private bool skip;
    private int nextNumber;

    public bool IsPlaying { get; private set; }

    /// <summary>The newest notice, while a replay is playing.</summary>
    public Beat? Caption { get; private set; }

    /// <summary>The last few notices, newest first. They stay after a replay ends and fade away on screen by themselves.</summary>
    public IReadOnlyList<Beat> Notices => notices;
    private readonly List<Beat> notices = [];
    private const int NoticesKept = 3;
    public bool Rolling { get; private set; }
    public (int, int) RollingFaces { get; private set; } = (1, 1);

    /// <summary>The space to flash on the board, and how ("good", "bad" or "").</summary>
    public string? FlashSpaceId { get; private set; }
    public string FlashTone { get; private set; } = "";

    public IReadOnlyList<MoneyPop> Pops { get; private set; } = [];

    /// <summary>True when events should be replayed at all (Off turns it off).</summary>
    public bool Enabled => settings.Current.Animation != AnimationSpeed.Off;

    private double Scale => settings.AnimationScale;

    /// <summary>Shows a notice. "replace" swaps out the newest one instead (the dice result replaces "rolls the dice...").</summary>
    private void Show(Beat beat, bool replace = false)
    {
        Caption = beat;
        if (replace && notices.Count > 0)
            notices[0] = beat;
        else
            notices.Insert(0, beat);
        if (notices.Count > NoticesKept)
            notices.RemoveRange(NoticesKept, notices.Count - NoticesKept);
    }

    /// <summary>Jump to the end of the replay that is playing now.</summary>
    public void Skip() => skip = true;

    /// <summary>The view the screen should draw: the real one, unless a replay is in progress.</summary>
    public PlayerView Apply(PlayerView real)
    {
        if (!IsPlaying)
            return real;
        var players = real.Players.Select(p => p with
        {
            Cash = cash.GetValueOrDefault(p.Id, p.Cash),
            Position = positions.GetValueOrDefault(p.Id, p.Position),
        }).ToList();
        return real with
        {
            Players = players,
            Properties = properties.Count == 0 ? real.Properties : properties,
            Log = real.Log.Take(Math.Min(real.Log.Count, logBase + revealed)).ToList(),
            Result = null,   // the end screen waits until the last event has been shown
        };
    }

    public IEnumerable<MoneyPop> PopsFor(int playerId) => Pops.Where(p => p.PlayerId == playerId);

    /// <summary>Remember how things looked before a step, so the replay can start from there.</summary>
    public void Prepare(PlayerView before)
    {
        live = before;
        logBase = before.Log.Count;
        revealed = 0;
        positions = before.Players.ToDictionary(p => p.Id, p => p.Position);
        cash = before.Players.ToDictionary(p => p.Id, p => p.Cash);
        properties = new Dictionary<string, PropertyInfo>(before.Properties);
    }

    /// <summary>Replays the events a step just produced, one at a time.</summary>
    public async Task PlayAsync(PlayerView after, IReadOnlyList<GameEvent> events, CancellationToken stop)
    {
        if (live == null || events.Count == 0)
            return;
        skip = false;
        IsPlaying = true;
        try
        {
            foreach (GameEvent e in events)
            {
                if (stop.IsCancellationRequested)
                    return;
                eventRevealed = false;
                await PlayOne(e, after, stop);
                Reveal();   // silent events still count, so the log stays lined up
            }
        }
        finally
        {
            IsPlaying = false;
            Rolling = false;
            Caption = null;
            FlashSpaceId = null;
            Pops = [];
            live = null;
            changed();
        }
    }

    // ------------------------------------------------------------------ one event

    /// <summary>Shows this event's line in the log. Safe to call twice.</summary>
    private void Reveal()
    {
        if (eventRevealed)
            return;
        eventRevealed = true;
        revealed++;
    }

    private async Task PlayOne(GameEvent e, PlayerView after, CancellationToken stop)
    {
        string text = EventDescriber.Describe(e, after, content);
        var (flash, tone) = FlashFor(e, after);
        FlashSpaceId = flash;
        FlashTone = tone;

        // Movement and dice have their own animations; everything else is a caption held for a moment.
        if (e is DiceRolled dice)
        {
            await RollDice(dice, stop);
            Show(new Beat(nextNumber++, "dice", text, ""), replace: true);
            Reveal();
            changed();
            await Wait(550, stop);
            return;
        }
        if (e is PlayerMoved move)
        {
            Show(new Beat(nextNumber++, "walk", text, "", SpaceId: after.Spaces[move.To].Id));
            Reveal();   // the "moved to" line appears as the token starts walking
            changed();
            await Walk(move, stop);
            return;
        }

        var pops = new List<MoneyPop>();
        foreach (var (player, amount) in MoneyChanges(e))
        {
            pops.Add(new MoneyPop(nextNumber++, player, amount));
            cash[player] = cash.GetValueOrDefault(player) + amount;
        }
        Pops = pops;
        ApplyBoard(e, after);

        int pause = PauseFor(e);
        if (text.Length == 0 && pause == 0)
            return;
        var (icon, tone2, cardId, back, sound) = Look(e);
        if (pops.Count > 0 && sound.Length == 0)
            sound = pops.Any(p => p.Amount < 0) ? "coin-out" : "coin-in";
        sounds.Play(sound);
        if (text.Length > 0)
            Show(new Beat(nextNumber++, icon, text, tone2, cardId, back, flash));
        Reveal();   // the log line appears with its caption
        changed();
        await Wait(pause, stop);
    }

    private async Task RollDice(DiceRolled dice, CancellationToken stop)
    {
        Show(new Beat(nextNumber++, "dice", $"{NameOf(dice.PlayerId)} rolls the dice…", ""));
        sounds.Play("dice");
        if (!settings.Current.ReduceMotion)
        {
            Rolling = true;
            int ticks = Math.Max(1, (int)(8 * Scale));
            for (int i = 0; i < ticks && !skip && !stop.IsCancellationRequested; i++)
            {
                RollingFaces = (cosmetic.Next(1, 7), cosmetic.Next(1, 7));
                changed();
                await Wait(95, stop, scaled: false);
            }
            Rolling = false;
        }
        else
        {
            changed();
            await Wait(500, stop);
        }
    }

    /// <summary>Moves a token one space at a time (or jumps, for a long way or with reduced motion).</summary>
    private async Task Walk(PlayerMoved move, CancellationToken stop)
    {
        int count = live!.Spaces.Count;
        int steps = (move.To - move.From + count) % count;
        if (steps == 0 || steps > 14 || settings.Current.ReduceMotion)
        {
            positions[move.PlayerId] = move.To;
            changed();
            await Wait(650, stop);
            return;
        }
        int each = (int)Math.Clamp(1500.0 / steps, 110, 220);
        for (int i = 1; i <= steps; i++)
        {
            positions[move.PlayerId] = (move.From + i) % count;
            FlashSpaceId = live.Spaces[positions[move.PlayerId]].Id;
            FlashTone = "";
            sounds.Play("step");
            changed();
            await Wait(each, stop);
        }
        await Wait(250, stop);
    }

    private async Task Wait(int ms, CancellationToken stop, bool scaled = true)
    {
        double left = ms * (scaled ? Scale : 1.0);
        while (left > 0 && !skip && !stop.IsCancellationRequested)
        {
            double slice = Math.Min(left, 80);
            await Task.Delay((int)Math.Max(1, slice), CancellationToken.None);
            left -= slice;
        }
    }

    // ------------------------------------------------------------------ what each event looks like

    private string NameOf(int id) => live!.Players.First(p => p.Id == id).Name;

    private static int PauseFor(GameEvent e) => e switch
    {
        TurnStarted => 800,
        PassedGo => 1100,
        LandedOn => 350,
        SentToJail => 1600,
        LeftJail => 900,
        StayedInJail => 700,
        AuctionStarted => 900,
        BidRaised => 600,
        AuctionPassed => 400,
        BidsRevealed => 1900,
        AuctionWon => 1600,
        AuctionNoBids => 900,
        DiceTieWon => 1000,
        RentPaid or TaxPaid or BailPaid or HospitalBillPaid or TileFeePaid => 1300,
        BuildingBuilt or BuildingSold or BuildingBurned => 1200,
        TradeProposed => 1300,
        TradeCompleted => 1900,
        TradeDeclined => 1000,
        TradeCountered => 1300,
        EventDrawn => 2300,
        OpportunityDrawn => 1500,
        CardPlayed => 2000,
        CardCancelled => 1300,
        MallCardBought or MallCardSold => 1300,
        CouncilResolved => 600,
        CouncilStarted or OrdinanceProposed => 1200,
        RoundEnded => 600,
        GameStarted => 0,
        _ => 800,
    };

    /// <summary>An icon name, a tone (colors the notice), a card to show, and a sound to play. An empty sound means
    /// "coins, if money moved" (see PlayOne).</summary>
    private (string Icon, string Tone, string? CardId, bool CardBack, string Sound) Look(GameEvent e) => e switch
    {
        TurnStarted => ("turn", "", null, false, "turn"),
        PassedGo => ("coin", "good", null, false, "coin-in"),
        SentToJail => ("police", "bad", null, false, "jail"),
        LeftJail => ("unlock", "good", null, false, "turn"),
        StayedInJail => ("jail", "", null, false, ""),
        RentPaid or TaxPaid or BailPaid or HospitalBillPaid or TileFeePaid or CardPaymentMade or CashToBank => ("coin", "bad", null, false, "coin-out"),
        CashFromBank or PerkIncome or CatchUpPaid or CouncilPayout => ("coin", "good", null, false, "coin-in"),
        AuctionStarted or BidRaised or AuctionPassed or AuctionNoBids or DiceTieWon or BidEdgeWon => ("gavel", "", null, false, ""),
        BidsRevealed => ("gavel", "", null, false, "card"),
        AuctionWon => ("house", "good", null, false, "gavel"),
        BuildingBuilt => ("build", "good", null, false, "build"),
        BuildingSold or BuildingBurned => ("fire", "bad", null, false, "bad"),
        TradeProposed or TradeDeclined or TradeCountered => ("trade", "", null, false, ""),
        TradeCompleted => ("trade", "good", null, false, "coin-in"),
        EventDrawn d => ("event", "", d.CardId, false, "card"),
        OpportunityDrawn => ("star", "", null, true, "card"),
        CardPlayed p => ("card", "", p.CardId, false, "card"),
        CardCancelled c => ("shield", "", c.CardId, false, "card"),
        MallCardBought or MallCardSold => ("shop", "", null, false, ""),
        CouncilStarted or OrdinanceProposed or CouncilResolved or OrdinanceEnded => ("council", "", null, false, ""),
        TileRevealed or TileRevealedByCouncil or TilePeeked => ("gov", "", null, false, "card"),
        MomentumGained or PrecisionRolled or Nudged or Detoured => ("star", "", null, false, ""),
        SentToHospital or InsuranceBought or InsuranceUsed or RecoverySkipped or HospitalShrugged => ("medic", "", null, false, ""),
        CasinoHighLow or CasinoRerolled or DuelChallenged or DuelDeclined or DuelRolled or DuelWon or LotteryRolled or LotteryWon => ("dice", "", null, false, "dice"),
        StadiumChosen or StadiumEnded => ("stadium", "", null, false, ""),
        RoadWorkStarted or RoadWorkCleared => ("cone", "", null, false, ""),
        RentBoostStarted or RentBoostEnded => ("party", "", null, false, ""),
        TransitHopped or TransitRidden or ExpressLineUsed => ("train", "", null, false, ""),
        PropertyMortgaged or PropertyForfeited or LoanTaken => ("coin", "bad", null, false, ""),
        PropertyUnmortgaged or LoanRepaid => ("coin", "", null, false, ""),
        ZoningChanged or PermitsAdded or CouncilCancelled => ("council", "", null, false, ""),
        CharacterChosen => ("star", "", null, false, ""),
        PlayerBankrupt => ("bankrupt", "bad", null, false, "bad"),
        GameEnded => ("flag", "good", null, false, "win"),
        _ => ("dot", "", null, false, ""),
    };

    /// <summary>The space to flash for this event, and whether the news is good or bad for its owner.</summary>
    private (string? SpaceId, string Tone) FlashFor(GameEvent e, PlayerView view) => e switch
    {
        LandedOn l => (l.SpaceId, ""),
        SentToJail => ("jail", "bad"),
        AuctionStarted a => (a.PropertyId, ""),
        AuctionWon w => (w.PropertyId, "good"),
        RentPaid r => (r.PropertyId, "bad"),
        TaxPaid t => (t.SpaceId, "bad"),
        BuildingBuilt b => (b.PropertyId, "good"),
        BuildingSold b => (b.PropertyId, "bad"),
        BuildingBurned b => (b.PropertyId, "bad"),
        PropertyMortgaged m => (m.PropertyId, "bad"),
        PropertyUnmortgaged m => (m.PropertyId, "good"),
        TileRevealed t => (t.SpaceId, ""),
        CardPlayed { TargetSpaceId: string target } => (target, ""),
        _ => (null, ""),
    };

    /// <summary>The money each player gains (positive) or loses (negative) from an event. Bank money has no player.</summary>
    private static IEnumerable<(int Player, int Amount)> MoneyChanges(GameEvent e)
    {
        switch (e)
        {
            case PassedGo g: yield return (g.PlayerId, g.Salary); break;
            case RentPaid r: yield return (r.FromId, -r.Amount); yield return (r.ToId, r.Amount); break;
            case TaxPaid t: yield return (t.PlayerId, -t.Amount); break;
            case BailPaid b: yield return (b.PlayerId, -b.Amount); break;
            case HospitalBillPaid h: yield return (h.PlayerId, -h.Amount); break;
            case AuctionWon w:
                if (w.PaidNow > 0) yield return (w.PlayerId, -w.PaidNow);
                if (w.PaidToId is int seller && w.PaidNow > 0) yield return (seller, w.PaidNow);
                break;
            case DownPaymentPaid d: yield return (d.PlayerId, -d.Amount); break;
            case BuildingBuilt b: yield return (b.PlayerId, -b.Cost); break;
            case BuildingSold b: yield return (b.PlayerId, b.Refund); break;
            case PropertyMortgaged m: yield return (m.PlayerId, m.Amount); break;
            case PropertyUnmortgaged u: yield return (u.PlayerId, -u.Cost); break;
            case LoanTaken l: yield return (l.PlayerId, l.Amount); break;
            case LoanRepaid l: yield return (l.PlayerId, -l.Amount); break;
            case InsuranceBought i: yield return (i.PlayerId, -i.Cost); break;
            case CasinoHighLow c when c.Net != 0: yield return (c.PlayerId, c.Net); break;
            case DuelWon d: yield return (d.WinnerId, d.Stake); yield return (d.LoserId, -d.Stake); break;
            case CashFromBank c: yield return (c.PlayerId, c.Amount); break;
            case CashToBank c: yield return (c.PlayerId, -c.Amount); break;
            case CardPaymentMade p:
                yield return (p.FromId, -p.Amount);
                if (p.ToId is int to) yield return (to, p.Amount);
                break;
            case TileFeePaid f:
                yield return (f.FromId, -f.Amount);
                if (f.ToId is int owner) yield return (owner, f.Amount);
                break;
            case MallCardBought m: yield return (m.PlayerId, -m.Price); break;
            case MallCardSold m: yield return (m.PlayerId, m.Price); break;
            case PerkIncome p: yield return (p.PlayerId, p.Amount); break;
            case PerkPaid p: yield return (p.FromId, -p.Amount); yield return (p.ToId, p.Amount); break;
            case CouncilPayout c: yield return (c.PlayerId, c.Amount); break;
            case CouncilPaymentMade c: yield return (c.PlayerId, -c.Amount); break;
            case CatchUpPaid c: yield return (c.PlayerId, c.Amount); break;
            case TransitHopped t: yield return (t.PlayerId, -t.Cost); break;
            case TransitRidden t: yield return (t.PlayerId, -t.Cost); break;
            case ExpressLineUsed x: yield return (x.PlayerId, -x.Cost); break;
            case LotteryWon l: yield return (l.WinnerId, l.Pot); break;
            case TradeCompleted t:
                int net = t.Offer.GetCash - t.Offer.GiveCash;   // what the proposer gains
                if (net != 0) { yield return (t.Offer.FromId, net); yield return (t.Offer.ToId, -net); }
                break;
        }
    }

    /// <summary>Changes who owns what, and building levels, as the matching events are shown.</summary>
    private void ApplyBoard(GameEvent e, PlayerView after)
    {
        void Set(string id, Func<PropertyInfo, PropertyInfo> change)
        {
            if (properties.TryGetValue(id, out var info))
                properties[id] = change(info);
        }
        switch (e)
        {
            case AuctionWon w:
                Set(w.PropertyId, p => p with { OwnerId = w.PlayerId, IsPending = w.DownPayment });
                break;
            case BuildingBuilt b: Set(b.PropertyId, p => p with { Level = b.NewLevel }); break;
            case BuildingSold b: Set(b.PropertyId, p => p with { Level = b.NewLevel }); break;
            case BuildingBurned b: Set(b.PropertyId, p => p with { Level = b.NewLevel }); break;
            case PropertyMortgaged m: Set(m.PropertyId, p => p with { IsMortgaged = true }); break;
            case PropertyUnmortgaged u: Set(u.PropertyId, p => p with { IsMortgaged = false }); break;
            case TradeCompleted t:
                foreach (string id in t.Offer.GiveProperties) Set(id, p => p with { OwnerId = t.Offer.ToId });
                foreach (string id in t.Offer.GetProperties) Set(id, p => p with { OwnerId = t.Offer.FromId });
                break;
            case SentToJail j:
                positions[j.PlayerId] = after.Spaces.First(s => s.Id == "jail").Index;
                break;
            case TransitRidden r: positions[r.PlayerId] = after.Spaces.First(s => s.Id == r.RailroadId).Index; break;
            case ExpressLineUsed x: positions[x.PlayerId] = after.Spaces.First(s => s.Id == x.RailroadId).Index; break;
        }
    }
}
