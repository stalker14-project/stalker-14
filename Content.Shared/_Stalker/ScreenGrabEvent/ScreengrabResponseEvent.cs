using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.ScreenGrabEvent
{
    [Serializable, NetSerializable]
    public sealed class ScreengrabResponseEvent : EntityEventArgs
    {
        // allocate лишнего места плохо, но списки юзать ещё хуже из-за их подхода к расширению каждую итерацию. Надо в jpeg работать, палитра не так важна
        public byte[] Screengrab = new byte[2700000];
        public Guid Token { get; set; }
    }
}
