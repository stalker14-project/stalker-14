using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.Armor;

public abstract partial class SharedArmorSystem : EntitySystem
{
    public void OnArmorMapInit(EntityUid uid, ArmorComponent component, MapInitEvent args)
    {
        ApplyLevels(component);
    }

    public void ApplyLevels(ArmorComponent component)
    {
        component.Modifiers = new DamageModifierSet
        {
            Coefficients = new Dictionary<string, float>(component.BaseModifiers.Coefficients),
            FlatReduction = new Dictionary<string, float>(component.BaseModifiers.FlatReduction)
        };

        if (component.STArmorLevels != null)
        {
            component.Modifiers = component.STArmorLevels.ApplyLevels(component.BaseModifiers);
        }
    }
}
