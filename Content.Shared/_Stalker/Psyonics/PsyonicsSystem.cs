using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared._Stalker.Psyonics;

public sealed partial class PsyonicsSystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeSource();

        SubscribeLocalEvent<PsyonicsComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<PsyonicsComponent> ent, ref ComponentStartup args)
    {
        SetPsy(ent, ent.Comp.Psy);
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
        Dirty(psionics);

        if (_mind.TryGetMind(psionics, out _, out var mind) && mind.Session is not null)
            RaiseNetworkEvent(psyMessage, mind.Session);

        return newValue;
    }
}
