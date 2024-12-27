using Content.Shared.Access;
using Content.Shared.Guidebook;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Roles
{
    /// <summary>
    ///     Describes information for a single job on the station.
    /// </summary>
    [Prototype("whitelistGroup")]
    public sealed class WhitelistGroupPrototype : IPrototype
    {
        [IdDataField]
        public string ID { get; } = string.Empty;

        [DataField("jobs", required: true)]
        public List<string> Jobs { get; } = new();
    }

}
// - type: whitelistGroup
//  id: exampleId
//  jobs:
//    - exampleJobId
//    - anotherJobId
