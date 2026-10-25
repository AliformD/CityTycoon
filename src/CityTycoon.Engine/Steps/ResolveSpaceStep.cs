using CityTycoon.Engine.Cards;
using CityTycoon.Engine.Content;
using CityTycoon.Engine.Events;
using CityTycoon.Engine.Rules;
using CityTycoon.Engine.State;
using CityTycoon.Engine.Tiles;

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
                ctx.Push(new CityParkStep { PlayerId = PlayerId });
                break;

            case SpaceKind.Opportunity:
                CardRules.DrawOpportunity(ctx, player, "Opportunity space");
                break;

            case SpaceKind.Event:
                CardRules.DrawEvent(ctx, player);
                break;

            case SpaceKind.Landmark:
                switch (space.Landmark)
                {
                    case LandmarkType.Hospital: ctx.Push(new HospitalVisitStep { PlayerId = PlayerId }); break;
                    case LandmarkType.Casino: ctx.Push(new CasinoStep { PlayerId = PlayerId }); break;
                    case LandmarkType.Stadium: ctx.Push(new StadiumStep { PlayerId = PlayerId }); break;
                    case LandmarkType.Mall: ctx.Push(new MallStep { PlayerId = PlayerId }); break;
                }
                break;

            case SpaceKind.GovernmentTile:
                TileRules.Land(ctx, player, space);
                break;

            // GO was already paid while moving. Jail is "Just Visiting".
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
            // Nobody owns it: everyone gets a chance to bid (Section 8.1). With the first-lap house rule,
            // the lander may first buy it at the listed price (the last piece of a set still goes to auction).
            bool fixedPrice = ctx.Rules.FirstLapFixedPrice && FirstLap.IsOn(ctx, player)
                && (ctx.Rules.FixedPriceLastPiece || !AuctionStep.IsLastPiece(ctx, propertyId));
            ctx.Push(new AuctionStep { PropertyId = propertyId, Kind = fixedPrice ? AuctionKind.FixedPrice : AuctionKind.Normal, TieBreakStartId = PlayerId });
            return;
        }

        if (owned.OwnerId == PlayerId)
            return;

        int rent = RentCalculator.Calculate(ctx.State, ctx.Content, propertyId, DiceTotal);
        if (rent <= 0)
            return;

        // Next to a revealed Transit Hub you may hop onto it instead of paying (Section 12.3).
        if (TileRules.TransitHubNextTo(ctx.State, ctx.Content, propertyId) is { } hub)
        {
            int cost = ctx.Content.GovTile(hub.TileId!).Neighbor!.TransitHopCost;
            if (player.Cash >= cost)
            {
                ctx.Push(new TransitHopStep { PlayerId = PlayerId, PropertyId = propertyId, HubSpaceId = hub.Id, Cost = cost, Rent = rent });
                return;
            }
        }
        ChargeRent(ctx, PlayerId, propertyId, rent);
    }

    /// <summary>Asks the player to pay rent to the property's owner (a Rent Strike may still change it).</summary>
    public static void ChargeRent(EngineContext ctx, int playerId, string propertyId, int rent) =>
        ctx.Push(new PaymentStep
        {
            FromId = playerId,
            ToId = ctx.State.Properties[propertyId].OwnerId,
            Amount = rent,
            Reason = PaymentReason.Rent,
            SpaceId = propertyId,
        });
}
