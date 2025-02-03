using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.ScreenGrabEvent
{
    [Serializable, NetSerializable]
    public sealed class ScreengrabResponseEvent : EntityEventArgs
    {
        public byte[] Screengrab = new byte[5000000];
        public Guid Token { get; set; }
    }
}
