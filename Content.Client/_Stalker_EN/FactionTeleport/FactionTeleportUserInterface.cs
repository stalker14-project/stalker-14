using Content.Shared._Stalker_EN.FactionTeleport;
using Robust.Client.UserInterface;

namespace Content.Client._Stalker_EN.FactionTeleport;

/// <summary>
/// Client bound UI that owns the teleport window.
/// The prototype UI mapping should reference this type name.
/// </summary>
public sealed class FactionTeleportUserInterface : BoundUserInterface
{
    private FactionTeleportWindow? _window;
    private Action<NetEntity>? _teleportHandler;

    public FactionTeleportUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FactionTeleportWindow>();

        _teleportHandler = destination =>
        {
            SendMessage(new FactionTeleportRequestTeleportMessage(destination));
        };
        _window.OnTeleportPressed += _teleportHandler;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null)
            return;

        if (state is not FactionTeleportUiState ui)
            return;

        _window.Populate(ui.Destinations);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        if (_window != null)
        {
            if (_teleportHandler != null)
                _window.OnTeleportPressed -= _teleportHandler;

            _window.Close();
            _window = null;
            _teleportHandler = null;
        }
    }
}
