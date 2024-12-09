using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Stalker.WarZone.Requirenments;

[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class ZoneOwnershipRequirenment : BaseWarZoneRequirenment
{
    /// <summary>
    /// Required zones which should be captured before this one can be handled
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<STWarZonePrototype>> RequiredZones { get; set; } = new();

    public override bool Check(IEntityManager entManager,
        IPrototypeManager protoManager,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = new FormattedMessage();

        return true;
    }
}
