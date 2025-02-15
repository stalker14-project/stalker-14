using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.RadioStalker;

[Serializable, NetSerializable]
public enum ConfigurableEncryptionKeyUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SelectEncryptionKeyMessage : BoundUserInterfaceMessage
{
    public string Channel;

    public SelectEncryptionKeyMessage(string channel)
    {
            Channel = channel;
    }
}

