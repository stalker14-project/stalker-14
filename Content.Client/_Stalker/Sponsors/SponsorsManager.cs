using System.Diagnostics.CodeAnalysis;
using Content.Shared._Stalker.Sponsors;
using Content.Shared._Stalker.Sponsors.Messages;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Client._Stalker.Sponsors;

public sealed class SponsorsManager
{
    [Dependency] private readonly IClientNetManager _netMgr = default!;

    private SponsorInfo? _info;
    private List<ProtoId<SpeciesPrototype>>? _allowedSpecies;

    public List<ProtoId<SpeciesPrototype>>? AllowedSpecies => _allowedSpecies;
    public event Action? SponsorSpeciesUpdated;

    public void Initialize()
    {
        // _netMgr.RegisterNetMessage<MsgSponsorInfo>(msg => _info = msg.Info);
        _netMgr.RegisterNetMessage<MsgSponsorVerified>(OnSponsorVerified);
        _netMgr.RegisterNetMessage<MsgSponsorRequestSpecies>(msg =>
        {
            _allowedSpecies = msg.AllowedSpecies.ConvertAll(p => new ProtoId<SpeciesPrototype>(p));
            SponsorSpeciesUpdated?.Invoke();
        });

        _netMgr.Disconnect += (_, _) => _allowedSpecies = null;
    }

    public bool TryGetInfo([NotNullWhen(true)] out SponsorInfo? sponsor)
    {
        sponsor = _info;
        return _info != null;
    }

    public void RequestSpeciesInfo()
    {
        _netMgr.ClientSendMessage(new MsgSponsorRequestSpecies());
    }

    private void OnSponsorVerified(MsgSponsorVerified msg)
    {
        RequestSpeciesInfo();
    }
}
