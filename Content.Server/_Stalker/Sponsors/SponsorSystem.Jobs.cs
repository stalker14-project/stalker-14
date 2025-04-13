using System.Linq;
using Content.Server.Players.JobWhitelist;
using Content.Shared._Stalker.Sponsors;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Stalker.Sponsors;

public sealed partial class SponsorSystem
{
    [Dependency] private readonly JobWhitelistManager _jobWhitelist = default!;
    
    private Dictionary<Enum, List<ProtoId<JobPrototype>>> _whitelist = new();

    private void InitializeJobs()
    {
        _prototype.PrototypesReloaded += ReloadPrototypes;
        _sponsors.SponsorPlayerCached += ReloadWhitelist;
        
        EnumeratePrototypes();
    }

    private void ReloadPrototypes(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<SponsorWhitelistPrototype>())
            return;
        
        EnumeratePrototypes();
    }

    private void ReloadWhitelist(NetUserId userId)
    {
        if (!_sponsors.TryGetInfo(userId, out var info))
            return;

        if (!_whitelist.TryGetValue(info.Level, out var jobs))
            return;

        _jobWhitelist.AddSponsorWhitelist(userId, jobs);
    }

    private void EnumeratePrototypes()
    {
        var prototypes = _prototype.EnumeratePrototypes<SponsorWhitelistPrototype>();
        
        foreach (var prototype in prototypes)
        {
            var list = _whitelist.GetOrNew(prototype.Level);
            list.AddRange(prototype.Jobs);
        }
    }
}