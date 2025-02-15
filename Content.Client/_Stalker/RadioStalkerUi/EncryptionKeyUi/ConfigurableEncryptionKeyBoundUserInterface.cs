using Content.Shared._Stalker.RadioStalker;
using JetBrains.Annotations;

namespace Content.Client._Stalker.RadioStalkerUi.EncryptionKeyUi;

[UsedImplicitly]
public sealed class ConfigurableEncryptionKeyBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ConfigurableEncryptionKeyMenu? _menu;

    public ConfigurableEncryptionKeyBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {

    }

    protected override void Open()
    {
        base.Open();

        _menu = new();

        _menu.ChannelInputEntered += channel =>
        {
            SendMessage(new SelectEncryptionKeyMessage(channel));
        };

        _menu.OnClose += Close;
        _menu.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        _menu?.Close();
    }
}
