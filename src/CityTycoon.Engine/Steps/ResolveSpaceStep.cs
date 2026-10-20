using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;

namespace CityTycoon.Engine.Steps;

/// <summary>
/// Works out what happens on the space a player landed on (Section 7.2, step 7).
/// It pushes whatever comes next (an auction, a payment) and removes itself.
/// </summary>
public sealed class ResolveSpaceStep : Step
{
    public int PlayerId { get; set; }

    /// <summary>The dice total that brought the player here (utilities use it for rent).</summary>
    public int DiceTotal { get; set; }

    public override StepResult Run(EngineContext ctx)
    {
        PlayerState player = ctx.Player(PlayerId);
        BoardSpace space = ctx.State.Board[player.Position];
        ctx.Emit(new LandedOn(PlayerId, space.Id));

        switch (space.Kind)
        {
            case SpaceKind.Property:
                ResolveProperty(ctx, player, space.Id);
                break;

            case SpaceKind.Tax:
                int tax = space.Tax == TaxType.Income ? ctx.Rules.IncomeTax : ctx.Rules.LuxuryTax;
                ctx.Push(new PaymentStep { FromId = PlayerId, Amount = tax, Reason = PaymentReason.Tax, SpaceId = space.Id });
                break;

            case SpaceKind.GoToJail:
                Movement.SendToJail(ctx, player, "landed on Go To Jail");
                break;

            case SpaceKind.CityPark:
                // City Park offers a token or an Opportunity card. Cards arrive in
                // Phase 5, so for now the token is the only choice (docs/DECISIONS.md).
                MomentumRules.Gain(ctx, player, ctx.Rules.CityParkMomentum, "City Park");
                break;

            case SpaceKind.Landmark:
                switch (space.Landmark)
                {
                    case LandmarkType.Hospital: ctx.Push(new HospitalVisitStep { PlayerId = PlayerId }); break;
                    case LandmarkType.Casino: ctx.Push(new CasinoStep { PlayerId = PlayerId }); break;
                    case LandmarkType.Stadium: ctx.Push(new StadiumStep { PlayerId = PlayerId }); break;
                    // The Mall sells Opportunity cards, which arrive in Phase 5.
                }
                break;

            // GO was already paid while moving. Jail is "Just Visiting".
            // Event and Opportunity cards and Government tiles arrive in later phases.
            default:
                break;
        }

        // Removing this step leaves anything it pushed (an auction, a payment) on the stack.
        return StepResult.Done;
    }

    private void ResolveProperty(EngineContext ctx, PlayerState player, string propertyId)
    {
        PropertyState owned = ctx.State.Properties[propertyId];
        if (owned.OwnerId == null)
        {
            // Nobody owns it: everyone gets a chance to bid (Section 8.1).
            ctx.Push(new AuctionStep { PropertyId = propertyId, Kind = AuctionKind.Normal, TieBreakStartId = PlayerId });
            return;
        }

        if (owned.OwnerId == PlayerId)
            return;

        int rent = RentCalculator.Calculate(ctx.State, ctx.Content, propertyId, DiceTotal);
        if (rent > 0)
        {
            ctx.Push(new PaymentStep
            {
                FromId = PlayerId,
                ToId = owned.OwnerId,
                Amount = rent,
                Reason = PaymentReason.Rent,
                SpaceId = propertyId,
            });
        }
    }
}
