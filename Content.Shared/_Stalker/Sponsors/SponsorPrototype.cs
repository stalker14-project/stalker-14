using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Sponsors;

[Prototype("sponsor"), Serializable]
public sealed class SponsorPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = null!;

    [DataField(serverOnly: true)]
    public float RepositoryWeight;

    [DataField(serverOnly: true)]
    public HashSet<ProtoId<EntityPrototype>> RepositoryItems = new();

    [DataField(serverOnly: true)]
    public bool HasPriorityJoin;

    [DataField(serverOnly: true)]
    public string DiscordRoleId = null!;

    [DataField(serverOnly: true)]
    public int SponsorPriority;
}

[Prototype("sponsorSpecies"), Serializable]
public sealed class SponsorSpeciesPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = null!;

    [DataField]
    public ProtoId<SpeciesPrototype> SpeciesId;

    [DataField]
    public HashSet<ProtoId<SponsorPrototype>> SponsorIds = new();
}

[Prototype("contributor"), Serializable]
public sealed class ContributorPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = null!;

    [DataField(serverOnly: true)]
    public string DiscordRoleId = null!;

    [DataField(serverOnly: true)]
    public float RepositoryWeight;

    [DataField(serverOnly: true)]
    public bool HasPriorityJoin;

    [DataField(serverOnly: true)]
    public HashSet<ProtoId<EntityPrototype>> ContributorItems = new();
}
