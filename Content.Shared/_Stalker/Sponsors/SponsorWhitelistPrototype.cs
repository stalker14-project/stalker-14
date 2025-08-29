using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Sponsors;

[Prototype("sponsorWhitelist")]
public sealed class SponsorWhitelistPrototype : IPrototype
{
    [IdDataField] 
    public string ID { get; private set; } = default!;
    
    [DataField]
    public ProtoId<SponsorPrototype> SponsorId = default!;
    
    [DataField]
    public List<ProtoId<JobPrototype>> Jobs = new();
}