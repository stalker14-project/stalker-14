using Content.Shared._Stalker.Modifier;
using Content.Shared._Stalker.Weight.Modifier;

namespace Content.Shared._Stalker.Weight;

public sealed partial class STWeightSystemModifier : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<STWeightComponent, UpdatedFloatModifierEvent<STWeightMaximumModifierComponent>>(OnUpdatedMaximum);
    }

    private void OnUpdatedMaximum(Entity<STWeightComponent> weight, ref UpdatedFloatModifierEvent<STWeightMaximumModifierComponent> args)
    {
        weight.Comp.MaximumModifier = args.Modifier;
        Dirty(weight.Owner, weight.Comp);
    }
}
