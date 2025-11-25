using Content.Shared._Stalker.Anomaly;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._Stalker.Anomaly;

public sealed class STAnomalyTipsSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private STAnomalyTipsOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalyTipsViewingComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<STAnomalyTipsViewingComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<STAnomalyTipsViewingComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<STAnomalyTipsViewingComponent, LocalPlayerDetachedEvent>(OnDetached);
    }

    private void OnInit(Entity<STAnomalyTipsViewingComponent> entity, ref ComponentInit args)
    {
        if (_player.LocalEntity is null)
            return;

        if (_player.LocalEntity != entity)
            return;

        AddOverlay();
    }

    private void OnShutdown(Entity<STAnomalyTipsViewingComponent> entity, ref ComponentShutdown args)
    {
        if (_player.LocalEntity is null)
            return;

        if (_player.LocalEntity != entity)
            return;

        RemoveOverlay();
    }

    private void OnAttached(Entity<STAnomalyTipsViewingComponent> entity, ref LocalPlayerAttachedEvent args)
    {
        AddOverlay();
    }

    private void OnDetached(Entity<STAnomalyTipsViewingComponent> entity, ref LocalPlayerDetachedEvent args)
    {
        RemoveOverlay();
    }

    private void AddOverlay()
    {
        if (_overlay != null)
            return;

        _overlay = new STAnomalyTipsOverlay();
        _overlayMan.AddOverlay(_overlay);
    }

    private void RemoveOverlay()
    {
        if (_overlay == null)
            return;

        _overlayMan.RemoveOverlay(_overlay);
        _overlay = null;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<STAnomalyTipsOverlay>();
    }
}
