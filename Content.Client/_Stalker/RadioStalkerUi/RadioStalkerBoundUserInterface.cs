using Content.Shared._Stalker.RadioStalker;
using JetBrains.Annotations;

namespace Content.Client._Stalker.RadioStalkerUi;

[UsedImplicitly]
public sealed class RadioStalkerBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private RadioStalkerMenu? _menu;

    public RadioStalkerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {

    }

    protected override void Open()
    {
        base.Open();

        _menu = new();

        _menu.OnMicPressed += enabled =>
        {
            SendMessage(new ToggleRadioMicMessage(enabled));
        };
        _menu.OnSpeakerPressed += enabled =>
        {
            SendMessage(new ToggleRadioSpeakerMessage(enabled));
        };
        _menu.InputTextEntered += channel =>
        {
            SendMessage(new SelectRadioChannelMessage(channel));
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

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not RadioStalkerBoundUIState msg)
            return;

        _menu?.Update(msg);
    }
}
