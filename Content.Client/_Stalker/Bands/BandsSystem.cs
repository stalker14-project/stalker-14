using Content.Shared._Stalker.Bands;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Stalker.Bands;
/// <summary>
/// Applies status icons for specified band
/// </summary>
public sealed class BandsSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BandsComponent, GetStatusIconsEvent>(OnGetStatusIcon);
    }

    private void OnGetStatusIcon(EntityUid uid, BandsComponent component, ref GetStatusIconsEvent args)
    {
        var ent = _player.LocalSession?.AttachedEntity;

        // Check if band of local entity is equal to other's entity band
        if (!TryComp<BandsComponent>(ent, out var band))
            return;

        if (!component.Enabled)
            return;

        if (component.BandName != band.BandName)
            return;

        // Apply status icon
        args.StatusIcons.Add(_proto.Index<JobIconPrototype>(component.BandStatusIcon));
    }
}
