using Content.Shared.Item.ItemToggle.Components;
using Content.Shared._Stalker.Components;
using Robust.Shared.Physics.Events;
using Content.Shared.Throwing;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Alert;
using Content.Shared.Rejuvenate;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;

namespace Content.Shared._Stalker;

/// <summary>
/// Handles <see cref="StalkerArtifactComponent"/> component manipulation.
/// </summary>
public sealed class StalkerArtifactSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StalkerArtifactComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<StalkerArtifactComponent, GotEquippedHandEvent>(OnGotEquipped);
        SubscribeLocalEvent<StalkerArtifactComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<StalkerArtifactComponent, ThrownEvent>(OnThrown);
    }
        [Dependency] private readonly IComponentFactory _componentFactory = default!;
    private void OnThrown(Entity<StalkerArtifactComponent> ent, ref ThrownEvent args)
    {
        var target = ent.Comp.Parent ? Transform(ent).ParentUid : ent.Owner;

        if (!EntityManager.HasComponent<StealthComponent>(ent))
            EntityManager.AddComponents(target, ent.Comp.Components);
    }
    private void OnMapInit(Entity<StalkerArtifactComponent> ent, ref MapInitEvent args)
    {
        var target = ent.Comp.Parent ? Transform(ent).ParentUid : ent.Owner;

        if (!EntityManager.HasComponent<StealthComponent>(ent))
            EntityManager.AddComponents(target, ent.Comp.Components);
    }
    private void OnDropped(Entity<StalkerArtifactComponent> ent, ref DroppedEvent args)
    {
        var target = ent.Comp.Parent ? Transform(ent).ParentUid : ent.Owner;

        if (EntityManager.HasComponent<StealthComponent>(ent))
            EntityManager.RemoveComponents(target, ent.Comp.RemoveComponents ?? ent.Comp.Components);
    }
    private void OnGotEquipped(Entity<StalkerArtifactComponent> ent, ref GotEquippedHandEvent args)
    {
        var target = ent.Comp.Parent ? Transform(ent).ParentUid : ent.Owner;

        if (EntityManager.HasComponent<StealthComponent>(ent))
            EntityManager.RemoveComponents(target, ent.Comp.RemoveComponents ?? ent.Comp.Components);
    }
}
