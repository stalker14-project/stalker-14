using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.ScreenGrabEvent
{
    [Serializable, NetSerializable]
    public sealed class ScreengrabResponseEvent : EntityEventArgs
    {
        //why 1.5 - each time it gets n/n+1 it multiplies by 4, so we can skip some not-needed iterations by allocating 1.5
        public List<byte> Screengrab = new List<byte>(150000);
        public Guid Token { get; set; }
    }
}
