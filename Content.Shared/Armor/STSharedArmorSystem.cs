using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.Armor;

public abstract partial class SharedArmorSystem : EntitySystem
{
    public void OnArmorInit(EntityUid uid, ArmorComponent component, ComponentInit? args = null)
    {
        component.Modifiers = component.BaseModifiers;
        if (component.STArmorLevels == null || component.Modifiers == null)
            return;
        component.Modifiers = component.STArmorLevels.ApplyLevels(component.BaseModifiers);
    }
}
