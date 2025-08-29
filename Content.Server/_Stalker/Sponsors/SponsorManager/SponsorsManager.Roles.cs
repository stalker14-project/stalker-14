using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Content.Shared._Stalker.Sponsors;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Sponsors.SponsorManager;

public sealed partial class SponsorsManager
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    
    private SponsorPrototype? TryGetSponsorPrototype(List<string> roles)
    {
        return roles
            .SelectMany(role => 
                _sponsorPrototypes.Where(sponsor => sponsor.DiscordRoleId == role)
            )
            .FirstOrDefault();
    }

    private ContributorPrototype? TryGetContributorPrototype(List<string> roles)
    {
        if (_contributorPrototype is null)
            return null;

        return roles.Any(role => role == _contributorPrototype.DiscordRoleId) ? _contributorPrototype : null;
    }

    private async Task<List<string>?> GetRoles(NetUserId userId)
    {
        var requestUrl = $"{_apiUrl}/roles?method=uid&id={userId}&guildId={_guildId}";
        var response = await _httpClient.GetAsync(requestUrl);

        if (!response.IsSuccessStatusCode)
        {
            _sawmill.Error($"Failed to retrieve roles for user {userId}: {response.StatusCode}");
            return null;
        }

        var responseContent = await response.Content.ReadFromJsonAsync<RolesResponse>();

        if (responseContent is not null)
            return responseContent.Roles.ToList();
        

        _sawmill.Error($"Roles not found in response for user {userId}");
        return null;
    }
}