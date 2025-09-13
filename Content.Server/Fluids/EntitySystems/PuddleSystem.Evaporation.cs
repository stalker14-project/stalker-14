using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent; // stalker-changes
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;

namespace Content.Server.Fluids.EntitySystems;

public sealed partial class PuddleSystem
{
    private static readonly TimeSpan EvaporationCooldown = TimeSpan.FromSeconds(1);

    // stalker-changes-start
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Absinthe = "Absinthe";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string BlueCuracao = "BlueCuracao";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Sugar = "Sugar";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string Champagne = "Champagne";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Cognac = "Cognac";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Grenadine = "Grenadine";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Gin = "Gin";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Gildlager = "Gildlager";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string CoffeeLiqueur = "CoffeeLiqueur";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string MelonLiquor = "MelonLiquor";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Patron = "Patron";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string PoisonWine = "PoisonWine";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Rum = "Rum";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string SpaceMountainWind = "SpaceMountainWind";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string SpaceUp = "SpaceUp";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Tequila = "Tequila";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Vermouth = "Vermouth";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Vodka = "Vodka";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Whiskey = "Whiskey";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Wine = "Wine";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Beer = "Beer";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Ale = "Ale";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Water = "Water";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string SodaWater = "SodaWater";

    [ValidatePrototypeId<ReagentPrototype>]
    private const string TonicWater = "TonicWater";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Sake = "Sake";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string JuiceLime = "JuiceLime";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string JuiceOrange = "JuiceOrange";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Cream = "Cream";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string LemonLime = "LemonLime";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Mead = "Mead";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Ice = "Ice";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string CoconutWater = "CoconutWater";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Coffee = "Coffee";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Tea = "Tea";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string GreenTea = "GreenTea";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string IcedTea = "IcedTea";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string DrGibb = "DrGibb";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string RootBeer = "RootBeer";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string JuiceWatermelon = "JuiceWatermelon";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string EnergyDrink = "EnergyDrink";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string STWater = "STWater";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Lemonade = "Lemonade";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string STTaurine = "STTaurine";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Milk = "Milk";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string SolDry = "SolDry";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string JuiceTomato = "JuiceTomato";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string JuiceLemon = "JuiceLemon";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string STNavoz = "STNavoz";


    private static string[] evaporationReagents = new[] {Absinthe, BlueCuracao, Champagne, Cognac,
                                                        Grenadine, Gin, Gildlager, CoffeeLiqueur, MelonLiquor,
                                                        Patron, PoisonWine, Rum, SpaceMountainWind, SpaceUp, Tequila,
                                                        Vermouth, Vodka, Whiskey, Wine, Beer, Ale,
                                                        Water, SodaWater, TonicWater, Sake, JuiceLime, JuiceOrange, JuiceLemon,JuiceTomato,
                                                        Cream, Sugar, LemonLime, Mead, Ice, CoconutWater, Coffee,
                                                        Tea, GreenTea, IcedTea, DrGibb, RootBeer, JuiceWatermelon, EnergyDrink,
                                                        STWater,Blood,Lemonade, STTaurine, Milk, SolDry, STNavoz};

    public static global::System.String[] EvaporationReagents { get => evaporationReagents; set => evaporationReagents = value; }

    //stalker-changes-end
    private void OnEvaporationMapInit(Entity<EvaporationComponent> entity, ref MapInitEvent args)
    {
        entity.Comp.NextTick = _timing.CurTime + EvaporationCooldown;
    }

    private void UpdateEvaporation(EntityUid uid, Solution solution)
    {
        if (HasComp<EvaporationComponent>(uid))
        {
            return;
        }

        if (solution.GetTotalPrototypeQuantity(EvaporationReagents) > FixedPoint2.Zero)
        {
            var evaporation = AddComp<EvaporationComponent>(uid);
            evaporation.NextTick = _timing.CurTime + EvaporationCooldown;
            return;
        }

        RemComp<EvaporationComponent>(uid);
    }

    private void TickEvaporation()
    {
        var query = EntityQueryEnumerator<EvaporationComponent, PuddleComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();
        var curTime = _timing.CurTime;
        while (query.MoveNext(out var uid, out var evaporation, out var puddle))
        {
            if (evaporation.NextTick > curTime)
                continue;

            evaporation.NextTick += EvaporationCooldown;

            if (!_solutionContainerSystem.ResolveSolution(uid, puddle.SolutionName, ref puddle.Solution, out var puddleSolution))
                continue;

            var reagentTick = evaporation.EvaporationAmount * EvaporationCooldown.TotalSeconds;
            puddleSolution.SplitSolutionWithOnly(reagentTick, EvaporationReagents);

            // Despawn if we"re done
            if (puddleSolution.Volume == FixedPoint2.Zero)
            {
                // Spawn a *sparkle*
                Spawn("PuddleSparkle", xformQuery.GetComponent(uid).Coordinates);
                QueueDel(uid);
            }
        }
    }
}
