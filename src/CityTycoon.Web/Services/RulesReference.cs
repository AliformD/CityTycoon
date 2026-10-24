using CityTycoon.Engine.Content;

namespace CityTycoon.Web.Services;

/// <summary>One part of the in-app rules reference. The text is plain so it also reads well out loud.</summary>
public sealed record RulesSection(string Id, string Title, IReadOnlyList<string> Paragraphs)
{
    /// <summary>The whole section as one piece of text, for read-aloud.</summary>
    public string SpokenText => Title + ". " + string.Join(" ", Paragraphs);
}

/// <summary>
/// A short, friendly summary of the rules (docs/GAME_DESIGN.md has the full version). Numbers come from
/// the game's rules file, so if a rule is tuned, this page stays right.
/// </summary>
public static class RulesReference
{
    public static IReadOnlyList<RulesSection> Sections(GameContent content)
    {
        RulesConfig r = content.Rules;
        string money(int amount) => "$" + amount;
        return
        [
            new("goal", "The goal", [
                $"Everyone starts with {money(r.StartingCash)}. Buy properties, collect rent, build, and watch your net worth. Net worth is your cash, plus the value of your properties, plus everything you built, plus {money(r.HandCardNetWorth)} for each Opportunity card in your hand, minus what you owe on loans.",
                $"In a Timed game (the default is {r.TimedModeDefaultRounds} rounds) the highest net worth wins when the last round ends. If there is a tie, the player with more cash wins. In a Classic game you play until only one player is left.",
            ]),
            new("turn", "Your turn", [
                "Before you roll you can build, sell buildings, mortgage, take or repay loans, and offer trades. Then you roll two dice and move. After you move, whatever you landed on happens, and you can manage your money again before you end your turn.",
                $"Rolling doubles gives you another turn. Three doubles in a row sends you to Jail. Passing or landing on GO pays {money(r.GoSalary)}{(r.GoMomentumGain > 0 ? $" and gives you {r.GoMomentumGain} Momentum token" : "")}.{(r.CatchUpBonus > 0 ? $" If you have the lowest net worth when you pass GO, you also get a {money(r.CatchUpBonus)} catch-up bonus." : "")}",
            ]),
            new("momentum", "Momentum tokens", [
                $"Momentum tokens let you bend the dice. After rolling, a Nudge moves you further or back, {r.NudgeCost} token for each space: 1 token moves you 1 space, 2 tokens 2 spaces (up to {r.NudgeMaxSpaces}, or 4 for the Commuter). A Precision Roll costs {r.PrecisionRollCost} tokens: you roll {r.PrecisionRollDice} dice and keep the two you like. You start with {r.StartingMomentum} and can hold up to {r.MomentumCap} (the Commuter more).",
                $"You earn more at City Park, the Fire Station, the Fast Track card and the Momentum Surge event. A Detour card played on you cancels your Nudge (the token stays spent) and moves you 1 space from your roll.",
            ]),
            new("auctions", "Buying property and auctions", [
                "When you land on a property nobody owns, it goes to auction. The first property of a color group, the first railroad and the first utility are silent auctions (sealed bids): everyone secretly bids, and the highest bid wins.",
                $"After that, the rest of the group, the railroads and the utilities are open auctions. Players take turns to raise by at least {money(r.BidIncrement)} or drop out. Once you drop out, you are out for good. The last player left pays their bid.",
                $"You can pay with a down payment: {r.DownPaymentPercent}% now and the rest when you next pass GO. Until it is paid, the property collects no rent and can't be built on. The minimum bid is {r.AuctionMinBidPercent}% of the property's assessed value.",
            ]),
            new("rent", "Rent", [
                "When you land on a property someone else owns, you pay them rent. Rent goes up when the owner has the whole color group, and again with each house, hotel and skyscraper. Railroads pay more the more of them one player owns. Utilities pay a multiple of your dice roll.",
                $"Rent is halved while you are in Jail. Mortgaged properties collect no rent. Government tiles and City Council ordinances can raise or lower rent.",
            ]),
            new("building", "Building and zoning", [
                $"Own a whole color group to build on it. You build evenly across the group, and at most {r.BuildLimitPerTurn} levels in a turn. Levels one to four are houses, level five is a hotel, and level six is a skyscraper. Hotels and skyscrapers need a permit from a shared supply.",
                "Each district has a zoning level that limits how tall buildings can be: Restricted allows 2 houses, Residential 4 houses, Commercial hotels, and High-Rise skyscrapers. If a district is downzoned, buildings already there stay, but nobody can add more above the new limit.",
            ]),
            new("council", "The City Council", [
                $"At the end of every {r.CouncilEveryNRounds}th round, the City Council meets. The player with the lowest net worth sets the agenda. They draw two ordinances, and choose one to put to a vote. If it needs a district, they choose that too.",
                $"Everyone who isn't bankrupt votes in secret: Yes, No, or Abstain. Every vote is worth the same, {r.VoteInfluence} influence. Public opinion adds {r.PublicOpinionInfluence} to each side, so nothing is certain. A Lobbyist card, ticked on your vote, adds 200 free influence to your side.",
                "Money only talks through the Politician. If one is playing, before the vote the other players bid, like an open auction, to pay the Politician to vote their way: beat the top offer by at least $10 for either side, or pass and you are out. The Politician may take the winning offer: the money is theirs, they must vote that way, and every dollar adds 2 influence to their vote. Only the Politician may lobby (pay the bank to make their own vote bigger). With no Politician, it is a plain vote.",
                "The chance an ordinance passes is the Yes influence divided by the Yes plus No influence. A roll decides. Lobbying money goes to the bank, whether the ordinance passes or fails.",
                "Landing on a revealed City Hall tile, or drawing the Emergency Session card, calls an Emergency session straight away. The player who caused it sets the agenda.",
            ]),
            new("jail", "Jail", [
                $"You go to Jail for three doubles, the Go To Jail corner, or some cards. In Jail you can pay {money(r.JailBail)} bail, use a Get Out of Jail Free card, or try to roll doubles. After {r.MaxJailTurns} turns you pay bail and leave. You can't build in Jail, and your rents are halved.",
            ]),
            new("spaces", "Other spaces", [
                $"Tax spaces cost {money(r.IncomeTax)} (Income Tax) and {money(r.LuxuryTax)} (Luxury Tax). The Hospital sells an Insured marker for {money(r.InsuredMarkerCost)}. At the Casino you can make one High or Low bet, or challenge another player to a Dice Duel. At the Stadium you double a color group's rent until your next turn. At City Park you take a Momentum token or draw an Opportunity card. At the Mall you can buy a face-up Opportunity card or sell one of yours.",
                "Opportunity cards are kept in your hand, secret, and played later. You can hold three. Event cards happen as soon as they are drawn. A used card goes to the bottom of its deck, never reshuffled, so remembering what was drawn tells you what comes back around. Hidden Government tiles on the board are a surprise: the first player to land on one reveals it for everybody.",
            ]),
            new("money", "Loans, mortgages and bankruptcy", [
                $"The bank lends {string.Join(", ", r.LoanOptions.Select(o => $"{money(o.Amount)} (repay {money(o.Repay)})"))}. Bigger loans cost more interest. You can hold {r.MaxLoans} loans at once. Mortgaging a property pays {r.MortgagePercent}% of its value, and unmortgaging costs that plus {r.UnmortgageFeePercent}%.",
                "If you owe more than you have, you must raise money by selling buildings, mortgaging or taking loans. If you still can't pay, you go bankrupt and your things go to the person you owed, or to auction if you owed the bank.",
            ]),
            new("characters", "Characters", content.Characters.Select(c => $"{c.Name}: {c.Text}").ToList()),
            new("perks", "Color perks", content.Groups.Values.Select(g => $"{g.Name} ({g.Perk.Name}): {g.Perk.Text}").ToList()),
        ];
    }
}
