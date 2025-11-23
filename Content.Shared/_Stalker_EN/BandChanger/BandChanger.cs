using Content.Shared._Stalker.Bands;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;

namespace Content.Shared._Stalker_EN.BandChanger;

/// <summary>
/// This handles...
/// </summary>
public sealed class BandChanger : EntitySystem
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<BandChangerComponent, StartCollideEvent>(OnCollide);
    }

    private void OnCollide(EntityUid uid, BandChangerComponent component, StartCollideEvent args)
    {
        if (!TryComp(args.OtherEntity, out ActorComponent? actor))
            return;

        if (TryComp(args.OtherEntity, out BandsComponent? band) && component.BandName != "")
        {
            band.BandStatusIcon = component.BandName;
        }
    }
}
