using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Server.Players.JobWhitelist;

public sealed partial class JobWhitelistManager
{
    public void AddSponsorWhitelist(NetUserId userId, List<ProtoId<JobPrototype>> whitelist)
    {
        if (!_whitelists.TryGetValue(userId, out var whitelistSet)) 
            return;
        
        foreach (var job in whitelist)
        {
            whitelistSet.Add(job);
        }

        if (!_player.TryGetSessionById(userId, out var session))
            return;
        
        SendJobWhitelist(session);
    }
}