using Robust.Shared.Serialization;

namespace Content.Shared._DZ.FarGunshot;

[Serializable, NetSerializable]
public sealed class FargunshotEvent : EntityEventArgs
{
    public int GunUid { get; set; }

    public FargunshotEvent(int gunUid)
    {
        GunUid = gunUid;
    }
    private FargunshotEvent() {}
}

