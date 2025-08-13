using Content.Shared._Stalker.Bands;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Stalker.Bands;
/// <summary>
/// Applies status icons for specified band
/// </summary>
public sealed class BandsSystem : SharedBandsSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BandsComponent, GetStatusIconsEvent>(OnGetStatusIcon);
    }

    private void OnGetStatusIcon(EntityUid uid, BandsComponent component, ref GetStatusIconsEvent args)
    {
        args.StatusIcons.Add(_proto.Index<JobIconPrototype>(component.BandStatusIcon));
    }
}
