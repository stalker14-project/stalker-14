using Content.Shared.DoAfter;
using Robust.Shared.Network;
using Robust.Shared.Serialization;


namespace Content.Shared._Stalker.ZoneAnomaly
{
    [Serializable, NetSerializable]
    public sealed partial class InteractionDoAfterEvent : SimpleDoAfterEvent { }
}

/// <summary>
/// Raised when a ZoneAnomalyDestructor finishes interacting after a delay.
/// This event must be serializable so it can sync across client/server.
/// </summary>
//[Serializable, NetSerializable, DataDefinition]
//public sealed partial class InteractionDoAfterEvent : SimpleDoAfterEvent { }
