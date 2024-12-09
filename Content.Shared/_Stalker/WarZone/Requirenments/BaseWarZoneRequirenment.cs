using System.Diagnostics.CodeAnalysis;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Stalker.WarZone.Requirenments;

public static class WarZoneRequirements
{
    public static bool TryRequirementsMet(
        STWarZonePrototype warZone,
        IEntityManager entManager,
        IPrototypeManager protoManager,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        var requirements = warZone.Requirements;
        reason = new FormattedMessage();

        foreach (var requirement in requirements ?? [])
        {
            if (!requirement.Check(entManager, protoManager, out reason))
                return false;
        }

        return true;
    }
}

/// <summary>
/// Abstract class for requirements for the war zone ownership.
/// </summary>
[ImplicitDataDefinitionForInheritors]
[Serializable, NetSerializable]
public abstract partial class BaseWarZoneRequirenment
{
    public abstract bool Check(
        IEntityManager entManager,
        IPrototypeManager protoManager,
        [NotNullWhen(false)] out FormattedMessage? reason);
}
