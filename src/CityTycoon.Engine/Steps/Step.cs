using System.Text.Json.Serialization;
using CityTycoon.Engine.Actions;

namespace CityTycoon.Engine.Steps;

/// <summary>
/// One piece of work on the resolution stack (Section 20.3).
///
/// Picture a stack of plates. The engine always works on the top plate. A step
/// can put new plates on top ("push" a child step), for example landing on a
/// space pushes an auction. The engine finishes the new top plate first, then
/// comes back to the one underneath. When a step needs a player to choose
/// something, it says "wait", and the engine pauses until the answer arrives.
///
/// Steps hold their own progress in properties (for example which stage of
/// the turn we're in), so the whole stack can be saved and loaded.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(GameStep), nameof(GameStep))]
[JsonDerivedType(typeof(TurnStep), nameof(TurnStep))]
[JsonDerivedType(typeof(ResolveSpaceStep), nameof(ResolveSpaceStep))]
[JsonDerivedType(typeof(AuctionStep), nameof(AuctionStep))]
[JsonDerivedType(typeof(PaymentStep), nameof(PaymentStep))]
[JsonDerivedType(typeof(TradeResponseStep), nameof(TradeResponseStep))]
[JsonDerivedType(typeof(DownPaymentDueStep), nameof(DownPaymentDueStep))]
[JsonDerivedType(typeof(HospitalVisitStep), nameof(HospitalVisitStep))]
[JsonDerivedType(typeof(CasinoStep), nameof(CasinoStep))]
[JsonDerivedType(typeof(StadiumStep), nameof(StadiumStep))]
[JsonDerivedType(typeof(CardPlayStep), nameof(CardPlayStep))]
[JsonDerivedType(typeof(CityParkStep), nameof(CityParkStep))]
[JsonDerivedType(typeof(MallStep), nameof(MallStep))]
[JsonDerivedType(typeof(ChooseFirePropertyStep), nameof(ChooseFirePropertyStep))]
[JsonDerivedType(typeof(ArrangeEventsStep), nameof(ArrangeEventsStep))]
[JsonDerivedType(typeof(DiceDuelStep), nameof(DiceDuelStep))]
[JsonDerivedType(typeof(HandLimitStep), nameof(HandLimitStep))]
[JsonDerivedType(typeof(TransitHopStep), nameof(TransitHopStep))]
[JsonDerivedType(typeof(TransitRideStep), nameof(TransitRideStep))]
[JsonDerivedType(typeof(CharacterPickStep), nameof(CharacterPickStep))]
[JsonDerivedType(typeof(PeekTileStep), nameof(PeekTileStep))]
[JsonDerivedType(typeof(KeepOneCardStep), nameof(KeepOneCardStep))]
[JsonDerivedType(typeof(CouncilStep), nameof(CouncilStep))]
[JsonDerivedType(typeof(PickDistrictStep), nameof(PickDistrictStep))]
public abstract class Step
{
    /// <summary>Called whenever this step is on top of the stack. Do some work, then say what's next.</summary>
    public abstract StepResult Run(EngineContext ctx);

    /// <summary>Every legal answer to one of this step's decisions (for UI buttons and bots).</summary>
    public virtual IReadOnlyList<GameAction> LegalActions(EngineContext ctx, PendingDecision decision) => [];

    /// <summary>
    /// Is this answer allowed? By default it must be one of <see cref="LegalActions"/>.
    /// Steps with number answers (like bids) override this to check a range instead.
    /// </summary>
    public virtual bool IsLegal(EngineContext ctx, PendingDecision decision, GameAction action) =>
        LegalActions(ctx, decision).Contains(action);

    /// <summary>A player answered one of this step's decisions. Record it; <see cref="Run"/> is called next.</summary>
    public virtual void Receive(EngineContext ctx, PendingDecision decision, GameAction action) =>
        throw new InvalidOperationException($"{GetType().Name} doesn't take answers.");
}

public enum StepStatus
{
    /// <summary>This step is finished; remove it from the stack.</summary>
    Done,

    /// <summary>Keep going: run whatever is now on top (this step, or a child it pushed).</summary>
    Continue,

    /// <summary>Pause until the listed players answer.</summary>
    Waiting,
}

public sealed record StepResult(StepStatus Status, IReadOnlyList<PendingDecision> Decisions)
{
    public static readonly StepResult Done = new(StepStatus.Done, []);
    public static readonly StepResult Continue = new(StepStatus.Continue, []);
    public static StepResult WaitFor(params PendingDecision[] decisions) => new(StepStatus.Waiting, decisions);
    public static StepResult WaitFor(IEnumerable<PendingDecision> decisions) => new(StepStatus.Waiting, decisions.ToList());
}
