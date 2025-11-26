using Content.Client._Stalker.Shop.Ui;
using Content.Shared._Stalker.Shop;
using Content.Shared._Stalker.StalkerRepository;
using JetBrains.Annotations;

namespace Content.Client._Stalker.StalkerRepository;

/// <summary>
/// Stalker shops BUI to handle events raising and send data to server.
/// </summary>
[UsedImplicitly]
public sealed class StalkerRepositoryBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private StalkerRepositoryMenu? _menu;

    public StalkerRepositoryBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        _menu = new StalkerRepositoryMenu();
        _menu.OpenCentered();

        _menu.OnClose += Close;

        _menu.RepositoryButtonPutPressed += (item, count) =>
        {
            SendMessage(new RepositoryInjectFromUserMessage(item, count));
        };
        _menu.RepositoryButtonGetPressed += (item, count) =>
        {
            SendMessage(new RepositoryEjectMessage(item, count));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu is not { } menu)
            return;

        // Basic place for handling states
        switch (state)
        {
            case RepositoryUpdateState msg:
                menu.UpdateAll(msg.Items, msg.UserItems, msg.MaxWeight);
                break;
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _menu?.Close();
        _menu?.Dispose();
    }
}

