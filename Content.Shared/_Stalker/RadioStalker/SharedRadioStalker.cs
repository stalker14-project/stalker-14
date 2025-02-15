using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.RadioStalker;

[Serializable, NetSerializable]
public enum RadioStalkerUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class RadioStalkerBoundUIState : BoundUserInterfaceState
{
    public bool MicEnabled;
    public bool SpeakerEnabled;

    public RadioStalkerBoundUIState(bool micEnabled, bool speakerEnabled)
    {
        MicEnabled = micEnabled;
        SpeakerEnabled = speakerEnabled;
    }
}

[Serializable, NetSerializable]
public sealed class ToggleRadioMicMessage : BoundUserInterfaceMessage
{
    public bool Enabled;

    public ToggleRadioMicMessage(bool enabled)
    {
        Enabled = enabled;
    }
}

[Serializable, NetSerializable]
public sealed class ToggleRadioSpeakerMessage : BoundUserInterfaceMessage
{
    public bool Enabled;

    public ToggleRadioSpeakerMessage(bool enabled)
    {
        Enabled = enabled;
    }
}

[Serializable, NetSerializable]
public sealed class SelectRadioChannelMessage : BoundUserInterfaceMessage
{
    public string Channel;

    public SelectRadioChannelMessage(string channel)
    {
        Channel = channel;
    }
}
