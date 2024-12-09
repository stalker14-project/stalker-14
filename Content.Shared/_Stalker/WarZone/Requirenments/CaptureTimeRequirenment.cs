using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Stalker.WarZone.Requirenments;

[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class CaptureTimeRequirenment : BaseWarZoneRequirenment
{
    /// How long you need to spent capturing the spot
    [DataField(required: true)]
    public TimeSpan CaptureTime;

    public override bool Check(IEntityManager entManager,
        IPrototypeManager protoManager,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = new FormattedMessage();

        return true;
    }
}
