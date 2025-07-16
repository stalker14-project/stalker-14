using Content.Shared._Stalker.Anomaly;
using Robust.Client.Graphics;
using Robust.Shared.Player;

namespace Content.Client._Stalker.Anomaly;

public sealed class STAnomalyTipsSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalyTipsViewingComponent, PlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<STAnomalyTipsViewingComponent, PlayerDetachedEvent>(OnDetached);
    }

    private void OnAttached(Entity<STAnomalyTipsViewingComponent> viewing, ref PlayerAttachedEvent args)
    {
        _overlay.AddOverlay(new STAnomalyTipsOverlay());
    }

    private void OnDetached(Entity<STAnomalyTipsViewingComponent> viewing, ref PlayerDetachedEvent args)
    {
        _overlay.RemoveOverlay(new STAnomalyTipsOverlay());
    }
}
