using Content.Shared.NPC.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;


namespace Content.Shared._Stalker.Bands;

[Prototype("stBand")]
public sealed class STBandPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; } = string.Empty;

    /// <summary>
    /// Band name. Realted to the band where playein in. Example: Freedom, Dolg, Military, etc. 
    /// </summary>
    [DataField]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Faction in terms of factions in SS14 (Nanotrasen, Hostile, etc.) We have Stalker, Mutants, etc. But in the future
    /// Faction can be an allience of bands which we will do in prototypes with factions
    /// </summary>
    [DataField]
    public ProtoId<NpcFactionPrototype> FactionId { get; set; }

    /// <summary>
    /// Hierarchy as a list of jobs. Starts from first level and going up.
    /// </summary>
    [DataField(readOnly: true)]
    public Dictionary<int, ProtoId<JobPrototype>> Hierarchy = new();

    /// <summary>
    /// Max number of members in the faction. We can use this number to allow faction leaders to manage their ranks
    /// </summary>
    [DataField]
    public int MaxMembers { get; set; } = 5;

    /// <summary>
    /// Managing rank Id. The id from hierarhy which we'll have rights to manage the band
    /// Default is null, meaning no one can manage the ranks
    /// </summary>
    [DataField]
    public int? ManagingRankId { get; set; } = null;

}
