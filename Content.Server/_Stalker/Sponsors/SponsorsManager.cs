using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Server._Stalker.Discord.DiscordAuth;
using Content.Shared._Stalker.CCCCVars;
using Content.Shared._Stalker.Shop;
using Content.Shared._Stalker.Shop.Prototypes;
using Content.Shared._Stalker.Sponsors;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Sponsors;

public sealed class SponsorsManager
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly DiscordAuthManager _discordAuthManager = default!;
    [Dependency] private readonly INetManager _netMgr = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    private readonly HttpClient _httpClient = new();
    private readonly Dictionary<NetUserId, SponsorData> _cachedSponsors = new();

    private string _apiUrl = string.Empty;
    private string _apiKey = string.Empty;
    private bool _enabled;
    private int _priorityTier = 3;
    private string _guildId = null!;
    private ISawmill _sawmill = null!;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("sponsors");
        _cfg.OnValueChanged(CCCCVars.DiscordAuthEnabled, val => { _enabled = val; }, true);
        _cfg.OnValueChanged(CCCCVars.SponsorsApiUrl, val => { _apiUrl = val; }, true);
        _cfg.OnValueChanged(CCCCVars.SponsorsApiKey, val => { _apiKey = val; }, true);
        _cfg.OnValueChanged(CCCCVars.PriorityJoinTier, val => { _priorityTier = val; }, true);
        _cfg.OnValueChanged(CCCCVars.SponsorsGuildId, val => { _guildId = val; }, true);

        _discordAuthManager.PlayerVerified += OnPlayerVerified;
        _netMgr.Disconnect += OnDisconnect;

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    private void OnDisconnect(object? sender, NetDisconnectedArgs e)
    {
        _cachedSponsors.Remove(e.Channel.UserId);
    }

    public bool TryGetInfo(NetUserId userId, [NotNullWhen(true)] out SponsorData? sponsorInfo)
    {
        return _cachedSponsors.TryGetValue(userId, out sponsorInfo);
    }

    public Dictionary<NetUserId, SponsorData> GetSponsors()
    {
        return _cachedSponsors;
    }

    private async Task<bool> IsGiven(NetUserId userId)
    {
        var requestUrl = $"{_apiUrl}/extra?method=uid&id={userId}";
        var response = await _httpClient.GetAsync(requestUrl);
        if (!response.IsSuccessStatusCode)
        {
            _sawmill.Error($"Failed to retrieve user with id {userId}");
            return false;
        }

        var responseContent = await response.Content.ReadFromJsonAsync<ExtraPatchBody>();

        if (responseContent is not null)
            return responseContent.LoadoutGiven == 1;

        _sawmill.Error($"Failed to parse response content {userId}");
        return false;

    }

    public async Task SetGiven(NetUserId userId, bool given)
    {
        var requestUrl = $"{_apiUrl}/extra?method=uid&id={userId}";
        var body = new ExtraPatchBody { LoadoutGiven = given ? 1 : 0 };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _httpClient.PatchAsync(requestUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            _sawmill.Error($"Failed to set given user {userId}: {response.StatusCode}");
            return;
        }

        if (TryGetInfo(userId, out var info))
            info.IsGiven = given;
    }

    public async Task MakeWipe()
    {
        var requestUrl = $"{_apiUrl}/each/extra";
        var request = new HttpRequestMessage(HttpMethod.Delete, requestUrl)
        {
            Content = new StringContent("{\"fields\": [\"loadout_given\"]}", Encoding.UTF8, "application/json"),
        };

        var response = await _httpClient.SendAsync(request);
        _sawmill.Debug($"Status Code: {response.StatusCode}");
        if (!response.IsSuccessStatusCode)
            _sawmill.Error("Error wiping given records.");

        foreach (var data in _cachedSponsors)
        {
            data.Value.IsGiven = false;
        }
    }

    public bool HavePriorityJoin(NetUserId userId)
    {
        if (!TryGetInfo(userId, out var sponsorInfo))
            return false;
        return (int) sponsorInfo.Level >= _priorityTier || sponsorInfo.Contributor;
    }

    public void RepositoryMaxWeight(ref float maxWeight, NetUserId userId)
    {
        if (!TryGetInfo(userId, out var sponsorData))
            return;

        if (sponsorData.Contributor)
            maxWeight += 250f;

        var prototypes = _prototype.EnumeratePrototypes<SponsorPrototype>().ToList();

        foreach (var prototype in prototypes)
        {
            if (!prototype.RepositoryWeight.ContainsKey((int) sponsorData.Level))
                continue;

            maxWeight = 0;
            maxWeight = prototype.RepositoryWeight[(int) sponsorData.Level];
            if (sponsorData.Contributor)
                maxWeight += 250f;

            return;
        }
    }

    public void FillSponsorCategories(
        ICommonSession session,
        ref List<CategoryInfo>? sponsorCategories,
        ref List<CategoryInfo>? contribCategories,
        ShopComponent comp)
    {
        sponsorCategories ??= [];
        contribCategories ??= [];

        var sponsorDict = comp.ShopSponsorCategories;
        var contribList = comp.ContributorCategories;

        if (!TryGetInfo(session.UserId, out var info))
        {
            sponsorCategories = null;
            contribCategories = null;
            return;
        }

        var found = sponsorDict
            .Where(kv => kv.Key <= (int) info.Level)
            .SelectMany(kv => kv.Value)
            .ToList();

        sponsorCategories = found.Count == 0 ? null : found;
        contribCategories = !info.Contributor ? null : contribList;
    }

    private async void OnPlayerVerified(object? sender, ICommonSession e)
    {
        if (!_enabled)
            return;

        var roles = await GetRoles(e.UserId);
        if (roles is null)
            return;

        var isGiven = await IsGiven(e.UserId);

        var level = SponsorData.ParseRoles(roles);
        var contrib = SponsorData.ParseContrib(roles);
        if (level is SponsorLevel.None && !contrib)
            return;

        var data = new SponsorData(level, e.UserId, isGiven, contrib);
        _cachedSponsors.Add(e.UserId, data);

        _sawmill.Debug($"{e.UserId} is sponsor now. UserId: {e.UserId}. Level: {Enum.GetName(data.Level)}:{(int)data.Level}");
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
        {
            return responseContent.Roles.ToList();
        }

        _sawmill.Error($"Roles not found in response for user {userId}");
        return null;
    }

    private sealed class RolesResponse
    {
        [JsonPropertyName("roles")]
        public string[] Roles { get; set; } = [];
    }

    private sealed class ExtraPatchBody
    {
        [JsonPropertyName("loadout_given")]
        public int? LoadoutGiven { get; set; } = 0;
    }
}
