using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Verbs;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Shared.Armor;

/// <summary>
///     This handles logic relating to <see cref="ArmorComponent" />
/// </summary>
public abstract partial class SharedArmorSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorComponent, MapInitEvent>(OnArmorMapInit); // stalker-changes
        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<CoefficientQueryEvent>>(OnCoefficientQuery);
        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
        SubscribeLocalEvent<ArmorComponent, BorgModuleRelayedEvent<DamageModifyEvent>>(OnBorgDamageModify);
        SubscribeLocalEvent<ArmorComponent, GetVerbsEvent<ExamineVerb>>(OnArmorVerbExamine);
    }

    /// <summary>
    /// Get the total Damage reduction value of all equipment caught by the relay.
    /// </summary>
    /// <param name="ent">The item that's being relayed to</param>
    /// <param name="args">The event, contains the running count of armor percentage as a coefficient</param>
    private void OnCoefficientQuery(Entity<ArmorComponent> ent, ref InventoryRelayedEvent<CoefficientQueryEvent> args)
    {
        if (TryComp<MaskComponent>(ent, out var mask) && mask.IsToggled)
            return;

        if (ent.Comp.Modifiers == null) // Stalker-changes
            return;

        foreach (var armorCoefficient in ent.Comp.Modifiers.Coefficients)
        {
            args.Args.DamageModifiers.Coefficients[armorCoefficient.Key] = args.Args.DamageModifiers.Coefficients.TryGetValue(armorCoefficient.Key, out var coefficient) ? coefficient * armorCoefficient.Value : armorCoefficient.Value;
        }
    }

    private void OnDamageModify(EntityUid uid, ArmorComponent component, InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (TryComp<MaskComponent>(uid, out var mask) && mask.IsToggled)
            return;

        // stalker-changes-start
        if (args.Args.IgnoreResistors.Contains(uid))
        {
            if (component.Modifiers == null)
                return;

            var modifiedModifiers = new DamageModifierSet
            {
                Coefficients = new Dictionary<string, float>(component.Modifiers.Coefficients),
                FlatReduction = new Dictionary<string, float>(component.Modifiers.FlatReduction)
            };

            foreach (var key in modifiedModifiers.Coefficients.Keys.ToList())
            {
                modifiedModifiers.Coefficients[key] = 1f;
            }

            args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, modifiedModifiers);

            return;
        }

        if (component.Modifiers == null)
            return;

        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, component.Modifiers);
        // stalker-changes-end
    }

    private void OnBorgDamageModify(EntityUid uid, ArmorComponent component,
        ref BorgModuleRelayedEvent<DamageModifyEvent> args)
    {
        if (TryComp<MaskComponent>(uid, out var mask) && mask.IsToggled)
            return;

        // stalker-changes-start
        if (args.Args.IgnoreResistors.Contains(uid))
        {
            if (component.Modifiers == null)
                return;

            var modifiedModifiers = new DamageModifierSet
            {
                Coefficients = new Dictionary<string, float>(component.Modifiers.Coefficients),
                FlatReduction = component.Modifiers.FlatReduction
            };

            foreach (var key in modifiedModifiers.Coefficients.Keys.ToList())
            {
                modifiedModifiers.Coefficients[key] = 1f;
            }

            args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, modifiedModifiers);

            return;
        }

        if (component.Modifiers == null)
            return;

        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, component.Modifiers);
        // stalker-changes-end
    }

    private void OnArmorVerbExamine(EntityUid uid, ArmorComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !component.ShowArmorOnExamine || component.Hidden || component.HiddenExamine) // Stalker-Changes
            return;

        var examineMarkup = GetArmorExamine(component.Modifiers ?? component.BaseModifiers, component); // Stalker-Changes

        var ev = new ArmorExamineEvent(examineMarkup);
        RaiseLocalEvent(uid, ref ev);

        _examine.AddDetailedExamineVerb(args, component, examineMarkup,
            Loc.GetString("armor-examinable-verb-text"), "/Textures/Interface/VerbIcons/dot.svg.192dpi.png",
            Loc.GetString("armor-examinable-verb-message"));
    }

    private FormattedMessage GetArmorExamine(DamageModifierSet armorModifiers, ArmorComponent comp)  // Stalker-Changes
    {
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString("armor-examine"));

        msg.PushNewline(); // Stalker-Changes
        msg.AddMarkup(Loc.GetString("armor-class-value", ("value", comp.ArmorClass ?? 0))); // Stalker-Changes

        foreach (var coefficientArmor in armorModifiers.Coefficients)
        {
            msg.PushNewline();

            var armorType = Loc.GetString("armor-damage-type-" + coefficientArmor.Key.ToLower());
            msg.AddMarkupOrThrow(Loc.GetString("armor-coefficient-value",
                ("type", armorType),
                ("value", MathF.Round((1f - coefficientArmor.Value) * 100, 1))
            ));
        }

        foreach (var flatArmor in armorModifiers.FlatReduction)
        {
            msg.PushNewline();

            var armorType = Loc.GetString("armor-damage-type-" + flatArmor.Key.ToLower());
            msg.AddMarkupOrThrow(Loc.GetString("armor-reduction-value",
                ("type", armorType),
                ("value", flatArmor.Value)
            ));
        }

        return msg;
    }
}
