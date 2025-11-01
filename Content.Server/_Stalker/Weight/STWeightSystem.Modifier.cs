using Content.Shared._Stalker.Modifier;
using Content.Shared._Stalker.Weight;

namespace Content.Server._Stalker.Weight;

public sealed partial class STWeightSystemModifier : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<STWeightComponent, UpdatedFloatModifierEvent<Modifier.STWeightMaximumModifierComponent>>(OnUpdatedMaximum);
    }

    private void OnUpdatedMaximum(Entity<STWeightComponent> weight, ref UpdatedFloatModifierEvent<Modifier.STWeightMaximumModifierComponent> args)
    {
        weight.Comp.MaximumModifier = args.Modifier;
        Dirty(weight.Owner, weight.Comp);
    }
}
