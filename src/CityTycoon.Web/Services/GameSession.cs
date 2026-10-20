using CityTycoon.AI;
using CityTycoon.Engine;
using CityTycoon.Engine.Actions;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Hosting;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Views;

namespace CityTycoon.Web.Services;

public enum SeatKind { Human, EasyBot }

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
public sealed class GameSession
{
    private readonly Dictionary<int, HumanUiController> humans = [];
    private CancellationTokenSource? stopGame;

    public GameEngine? Engine { get; private set; }
    public GameContent Content => GameContent.Default;
    public IReadOnlyDictionary<int, SeatKind> Seats { get; private set; } = new Dictionary<int, SeatKind>();

    /// <summary>The question a person must answer right now, if any.</summary>
    public HumanRequest? Request { get; private set; }

    /// <summary>True while the pass-the-device screen should cover everything.</summary>
    public bool ShowPassScreen { get; private set; }

    /// <summary>Skip the short pause between bot moves.</summary>
    public bool FastBots { get; set; }

    /// <summary>Set if the game crashed, so the page can show what went wrong.</summary>
    public string? Error { get; private set; }

    public bool HasGame => Engine != null;

    /// <summary>Pages subscribe to this and redraw when it fires.</summary>
    public event Action? Changed;

    /// <summary>What everyone at the table can see (no secrets), for drawing the board.</summary>
    public PlayerView PublicView => Engine!.GetView(viewerId: -1);

    public bool IsHuman(int playerId) => Seats.TryGetValue(playerId, out var kind) && kind == SeatKind.Human;

    public void Start(IReadOnlyList<SeatSetup> seats, GameMode mode, int rounds, PrivacyMode privacy, ulong? seed)
    {
        var setup = new GameSetup
        {
            Players = seats.Select(s => new PlayerSetup(s.Name)).ToList(),
            Mode = mode,
            MaxRounds = rounds,
            Privacy = privacy,
            Seed = seed,
        };
        Engine = GameEngine.NewGame(setup, Content);
        Seats = Enumerable.Range(0, seats.Count).ToDictionary(i => i, i => seats[i].Kind);
        humans.Clear();
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
                controllers[id] = new EasyBot(Engine.State.Settings.Seed * 31 + (ulong)id, Content);
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
                if (botsAreUp && !FastBots)
                    await Task.Delay(350); // a short pause so people can follow what the bots did

                await host.StepAsync();
                if (stop.IsCancellationRequested)
                    return;
                Changed?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        Request = null;
        Changed?.Invoke();
    }

    /// <summary>Called by a <see cref="HumanUiController"/> when it needs a person to choose.</summary>
    public void ShowRequest(HumanRequest request)
    {
        Request = request;
        // Hidden mode (Section 25): before a private choice, cover the screen
        // until the right person has the device, unless only one human is playing.
        bool isPrivate = request.Decision.Kind == DecisionKind.SubmitBid;
        bool severalHumans = Seats.Count(s => s.Value == SeatKind.Human) > 1;
        ShowPassScreen = isPrivate && severalHumans && Engine!.State.Settings.Privacy == PrivacyMode.Hidden;
        Changed?.Invoke();
    }

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
