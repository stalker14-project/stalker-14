using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.ScreenGrabEvent
{
    [Serializable, NetSerializable]
    public sealed class ScreengrabResponseEvent : EntityEventArgs
    {
        public byte[] Screengrab = new byte[2500000];
    }
}
