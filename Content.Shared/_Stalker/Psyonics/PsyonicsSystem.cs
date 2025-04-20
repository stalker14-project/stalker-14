using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared._Stalker.Psyonics;

public sealed partial class PsyonicsSystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeSource();
        SubscribeLocalEvent<PsyonicsComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, PsyonicsComponent component, ComponentStartup args)
    {
        var psyMessage = new PsyEnergyChangedMessage(component.PsyMax, component.Psy, component.Psy);
        RaiseNetworkEvent(psyMessage);
    }

    public float GetPsy(Entity<PsyonicsComponent> psionics)
    {
        return psionics.Comp.Psy;
    }

    public bool HasPsy(Entity<PsyonicsComponent> psionics, int value)
    {
        return psionics.Comp.Psy >= value;
    }

    public int RegenPsy(Entity<PsyonicsComponent> psionics, int value)
    {
        return SetPsy(psionics, psionics.Comp.Psy + value);
    }

    public int RemovePsy(Entity<PsyonicsComponent> psionics, int value)
    {
        return SetPsy(psionics, psionics.Comp.Psy - value);
    }

    private int SetPsy(Entity<PsyonicsComponent> psionics, int value)
    {
        var newValue = Math.Clamp(value, 0, psionics.Comp.PsyMax);
        var psyMessage = new PsyEnergyChangedMessage(psionics.Comp.PsyMax, psionics.Comp.Psy, newValue);
        psionics.Comp.Psy = newValue;
        RaiseNetworkEvent(psyMessage);
        Dirty(psionics);
        return newValue;
    }
}
