using Content.Shared._Stalker.Anomaly;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._Stalker.Anomaly;

public sealed class STAnomalyTipsSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalyTipsViewingComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<STAnomalyTipsViewingComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<STAnomalyTipsViewingComponent, PlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<STAnomalyTipsViewingComponent, PlayerDetachedEvent>(OnDetached);
    }

    private void OnInit(Entity<STAnomalyTipsViewingComponent> entity, ref ComponentInit args)
    {
        if (_player.LocalEntity is null)
            return;

        if (_player.LocalEntity != entity)
            return;

        _overlay.AddOverlay(new STAnomalyTipsOverlay());
    }

    private void OnShutdown(Entity<STAnomalyTipsViewingComponent> entity, ref ComponentShutdown args)
    {
        if (_player.LocalEntity is null)
            return;

        if (_player.LocalEntity != entity)
            return;

        _overlay.RemoveOverlay(new STAnomalyTipsOverlay());
    }

    private void OnAttached(Entity<STAnomalyTipsViewingComponent> entity, ref PlayerAttachedEvent args)
    {
        if (_player.LocalEntity is null)
            return;

        if (_player.LocalEntity != entity)
            return;

        _overlay.AddOverlay(new STAnomalyTipsOverlay());
    }

    private void OnDetached(Entity<STAnomalyTipsViewingComponent> entity, ref PlayerDetachedEvent args)
    {
        if (_player.LocalEntity is null)
            return;

        if (_player.LocalEntity != entity)
            return;

        _overlay.RemoveOverlay(new STAnomalyTipsOverlay());
    }
}
