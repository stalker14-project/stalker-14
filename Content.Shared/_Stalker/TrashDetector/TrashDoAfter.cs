using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.TrashDetector
{

    [Serializable, NetSerializable]
    public sealed partial class GetTrashDoAfterEvent : SimpleDoAfterEvent { }

}
