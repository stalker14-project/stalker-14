using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.WarZone.Requirenments;

[Serializable, NetSerializable]
public enum CaptureBlockReason
{
    None, // Capture can proceed
    Cooldown, // Zone is on capture cooldown
    Ownership, // Attacker does not own required zones
    CaptureTime, // Capture time requirement not met
    Other // Generic block reason
}