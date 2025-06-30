using System.Linq;
using Content.Shared._Stalker.Sponsors;
using Content.Shared._Stalker.Sponsors.Messages;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Sponsors.SponsorManager;

public sealed partial class SponsorsManager
{
    private HashSet<SponsorSpeciesPrototype> _sponsorSpecies = new();
    
    public HashSet<SponsorSpeciesPrototype> SponsorSpecies => _sponsorSpecies;
    
    private void InitializeSpecies()
    {
        _netMgr.RegisterNetMessage<MsgSponsorRequestSpecies>(OnSpeciesRequest);
        _prototype.PrototypesReloaded += ReEnumeratePrototypes;
        ReEnumeratePrototypes();
    }

    private void OnSpeciesRequest(MsgSponsorRequestSpecies message)
    {
        void SendSpecies(INetChannel channel, List<string> protoIds)
        {
            var msg = new MsgSponsorRequestSpecies { AllowedSpecies = protoIds };
            channel.SendMessage(msg);
        }
        
        var origin = message.MsgChannel;
        if (!TryGetInfo(origin.UserId, out var info) || info.SponsorProtoId is null)
        {
            SendSpecies(origin, new List<string>());
            return;
        }

        var allowedPrototypes = _sponsorSpecies
            .Where(p => p.SponsorIds.Contains(info.SponsorProtoId.Value))
            .Select(p => p.SpeciesId.ToString())
            .ToList();
        
        SendSpecies(origin, allowedPrototypes);
    }

    private void ReEnumeratePrototypes(PrototypesReloadedEventArgs? args = null)
    {
        if (args is null)
        {
            _sponsorSpecies = _prototype
                .EnumeratePrototypes<SponsorSpeciesPrototype>()
                .ToHashSet();

            return;
        }

        if (!args.WasModified<SponsorSpeciesPrototype>())
            return;

        _sponsorSpecies = _prototype
            .EnumeratePrototypes<SponsorSpeciesPrototype>()
            .ToHashSet();
    }
}