using CityTycoon.AI;
using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Views;
using Microsoft.JSInterop;

namespace CityTycoon.Web.Services;

public enum SeatKind { Human, EasyBot, NormalBot, HardBot }

/// <summary>One seat on the new-game screen.</summary>
public sealed class SeatSetup
{
    public string Name { get; set; } = "";
    public SeatKind Kind { get; set; } = SeatKind.EasyBot;
}

/// <summary>
/// The browser's link to one running game. It creates the engine and the
/// controllers, runs the game host in the background, and tells the pages
/// when something changed so they redraw.
///
/// This lives in the Web project because it's about the screen. The rules
/// themselves stay in the Engine.
/// </summary>
public sealed class GameSession(SettingsService settings, IJSRuntime js)
{
    private const string SaveKey = "citytycoon.save";

    private readonly Dictionary<int, HumanUiController> humans = [];
    private CancellationTokenSource? stopGame;

    public GameEngine? Engine { get; private set; }
    public GameContent Content => GameContent.Default;
    public IReadOnlyDictionary<int, SeatKind> Seats { get; private set; } = new Dictionary<int, SeatKind>();

    /// <summary>The question a person must answer right now, if any.</summary>
    public HumanRequest? Request { get; private set; }

    /// <summary>True while the pass-the-device screen should cover everything.</summary>
    public bool ShowPassScreen { get; private set; }

    /// <summary>A City Council result waiting to be shown with the big reveal. The game pauses until someone dismisses it.</summary>
    public CouncilResolved? Reveal { get; private set; }

    private TaskCompletionSource? revealDismissed;
    private int logScanned;

    /// <summary>Skip the short pause between bot moves (kept in Settings, so it is remembered).</summary>
    public bool FastBots
    {
        get => settings.Current.FastBots;
        set
        {
            settings.Current.FastBots = value;
            _ = settings.SaveAsync();
        }
    }

    /// <summary>Humans who chose "Auto-pass reactions" (Section 17.2): they're never asked about Reaction cards.</summary>
    private readonly HashSet<int> autoPassReactions = [];

    /// <summary>The last human shown a question, so Hidden mode knows when the device changes hands.</summary>
    private int lastHumanId = -1;

    public bool AutoPassesReactions(int playerId) => autoPassReactions.Contains(playerId);

    public void SetAutoPassReactions(int playerId, bool on)
    {
        if (on)
            autoPassReactions.Add(playerId);
        else
            autoPassReactions.Remove(playerId);
        Changed?.Invoke();
    }

    /// <summary>Set if the game crashed, so the page can show what went wrong.</summary>
    public string? Error { get; private set; }

    public bool HasGame => Engine != null;

    /// <summary>Pages subscribe to this and redraw when it fires.</summary>
    public event Action? Changed;

    /// <summary>What everyone at the table can see (no secrets), for drawing the board.</summary>
    public PlayerView PublicView => Engine!.GetView(viewerId: -1);

    public bool IsHuman(int playerId) => Seats.TryGetValue(playerId, out var kind) && kind == SeatKind.Human;

    public void Start(IReadOnlyList<SeatSetup> seats, GameMode mode, int rounds, PrivacyMode privacy, ulong? seed, CharacterMode characters = CharacterMode.None)
    {
        var setup = new GameSetup
        {
            Players = seats.Select(s => new PlayerSetup(s.Name)).ToList(),
            Mode = mode,
            MaxRounds = rounds,
            Privacy = privacy,
            Seed = seed,
            Characters = characters,
        };
        Launch(GameEngine.NewGame(setup, Content), Enumerable.Range(0, seats.Count).ToDictionary(i => i, i => seats[i].Kind), settings.Current.AutoPassReactions
            ? Enumerable.Range(0, seats.Count).Where(i => seats[i].Kind == SeatKind.Human) : []);
    }

    /// <summary>Carries on a saved game exactly where it stopped.</summary>
    public void Resume(SavedGame save)
    {
        var engine = new GameEngine(GameStateSerializer.FromJson(save.State), Content);
        Launch(engine, save.Seats, save.AutoPassReactions);
    }

    /// <summary>Starts running a game (new or loaded): gives every seat its controller and starts the host.</summary>
    private void Launch(GameEngine engine, IReadOnlyDictionary<int, SeatKind> seats, IEnumerable<int> autoPass)
    {
        Engine = engine;
        Seats = seats;
        humans.Clear();
        autoPassReactions.Clear();
        foreach (int id in autoPass)
            autoPassReactions.Add(id);
        lastHumanId = -1;
        logScanned = Engine.State.Log.Count;   // a loaded game's old reveals aren't shown again
        Reveal = null;
        Request = null;
        ShowPassScreen = false;
        Error = null;
        stopGame?.Cancel();
        stopGame = new CancellationTokenSource();

        // Give every seat its controller. The engine never knows which kind it is.
        var controllers = new Dictionary<int, IPlayerController>();
        foreach (var (id, kind) in Seats)
        {
            if (kind == SeatKind.Human)
            {
                var human = new HumanUiController(this);
                humans[id] = human;
                controllers[id] = human;
            }
            else
            {
                ulong botSeed = Engine.State.Settings.Seed * 31 + (ulong)id;
                controllers[id] = kind switch
                {
                    SeatKind.HardBot => new HardBot(botSeed, Content),
                    SeatKind.NormalBot => new NormalBot(botSeed, Content),
                    _ => new EasyBot(botSeed, Content),
                };
            }
        }

        var host = new GameHost(Engine, controllers);
        _ = RunAsync(host, stopGame.Token); // runs in the background; "_ =" means we don't wait for it here
        Changed?.Invoke();
    }

    private async Task RunAsync(GameHost host, CancellationToken stop)
    {
        try
        {
            while (!host.Engine.State.IsOver)
            {
                if (stop.IsCancellationRequested)
                    return;
                bool botsAreUp = host.Engine.GetPendingDecisions().All(d => !IsHuman(d.PlayerId));
                if (botsAreUp && settings.BotDelayMs > 0)
                    await Task.Delay(settings.BotDelayMs); // a short pause so people can follow what the bots did

                await host.StepAsync();
                if (stop.IsCancellationRequested)
                    return;
                Changed?.Invoke();
                await ShowCouncilResults(host, stop);
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        if (host.Engine.State.IsOver)
            await ForgetBrowserSaveAsync();   // a finished game has nothing to continue
        Request = null;
        Changed?.Invoke();
    }

    /// <summary>
    /// The City Council reveal (Section 11.3): when a vote has just been counted, the game waits until someone
    /// clicks Continue, so nobody misses the votes or the roll. Games with no humans, or Fast bots, skip the wait.
    /// </summary>
    private async Task ShowCouncilResults(GameHost host, CancellationToken stop)
    {
        var log = host.Engine.State.Log;
        for (; logScanned < log.Count; logScanned++)
        {
            if (log[logScanned] is not CouncilResolved result)
                continue;
            if (FastBots || !Seats.Values.Any(k => k == SeatKind.Human))
                continue;
            Reveal = result;
            revealDismissed = new TaskCompletionSource();
            Changed?.Invoke();
            using (stop.Register(() => revealDismissed.TrySetCanceled()))
            {
                try { await revealDismissed.Task; }
                catch (TaskCanceledException) { return; }
            }
        }
    }

    public void DismissReveal()
    {
        Reveal = null;
        revealDismissed?.TrySetResult();
        Changed?.Invoke();
    }

    /// <summary>Called by a <see cref="HumanUiController"/> when it needs a person to choose.</summary>
    public void ShowRequest(HumanRequest request)
    {
        if (request.Decision.Kind == DecisionKind.PlayReaction && AutoPassesReactions(request.PlayerId))
        {
            humans[request.PlayerId].Answer(new NoThanks());
            return;
        }

        Request = request;
        // A quiet autosave at the start of each person's turn, so closing the tab never loses much.
        if (request.Decision.Kind == DecisionKind.ChooseMoveMethod)
            _ = SaveInBrowserAsync();
        // Hidden mode (Section 25): every question can show the player's secret
        // hand, so cover the screen whenever the device passes to a different
        // person, and before every sealed bid or Council vote (open auctions are public, so they need no cover). Not needed with only one human.
        bool hidden = Engine!.State.Settings.Privacy == PrivacyMode.Hidden;
        bool isBid = (request.Decision.Kind == DecisionKind.SubmitBid && !request.Decision.OpenAuction) || request.Decision.Kind == DecisionKind.CouncilVote;
        bool newPerson = request.PlayerId != lastHumanId;
        bool severalHumans = Seats.Count(s => s.Value == SeatKind.Human) > 1;
        // A trade offer from one person to another means handing the device over too.
        bool tradeBetweenHumans = (request.Decision.Trade is { } offer && IsHuman(offer.FromId))
            || (request.Decision.Duel is { } duel && IsHuman(duel.ChallengerId));
        ShowPassScreen = severalHumans && ((hidden && (isBid || newPerson)) || (newPerson && tradeBetweenHumans));
        lastHumanId = request.PlayerId;
        Changed?.Invoke();
    }

    // ---------------------------------------------------------- saving

    /// <summary>The game as a save file's text.</summary>
    public string CreateSaveText() =>
        new SavedGame(SavedGame.CurrentVersion, DateTime.Now.ToString("d MMM, HH:mm"), Seats.ToDictionary(s => s.Key, s => s.Value),
            autoPassReactions.ToList(), GameStateSerializer.ToJson(Engine!.State)).ToJson();

    /// <summary>Saves in the browser. Returns false if the browser refused (full or blocked storage).</summary>
    public async Task<bool> SaveInBrowserAsync() =>
        HasGame && await js.InvokeAsync<bool>("ct.set", SaveKey, CreateSaveText());

    public async Task DownloadSaveAsync() =>
        await js.InvokeVoidAsync("ct.download", $"city-tycoon-round-{Engine!.State.Round}.json", CreateSaveText());

    /// <summary>The save kept in the browser, or null if there isn't one (or it can't be read).</summary>
    public async Task<SavedGame?> FindBrowserSaveAsync()
    {
        try
        {
            string? json = await js.InvokeAsync<string?>("ct.get", SaveKey);
            return string.IsNullOrEmpty(json) ? null : SavedGame.FromJson(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task ForgetBrowserSaveAsync() => await js.InvokeVoidAsync("ct.remove", SaveKey);

    public void DismissPassScreen()
    {
        ShowPassScreen = false;
        Changed?.Invoke();
    }

    /// <summary>The person clicked an answer.</summary>
    public void Answer(GameAction action)
    {
        if (Request is not { } request)
            return;
        Request = null;
        Changed?.Invoke();
        humans[request.PlayerId].Answer(action);
    }

    public void Quit()
    {
        stopGame?.Cancel();
        Engine = null;
        Request = null;
        Changed?.Invoke();
    }
}
