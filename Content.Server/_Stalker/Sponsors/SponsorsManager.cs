using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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


    private string _apiUrl = string.Empty;
    private string _apiKey = string.Empty;
    private bool _enabled = false;
    private int _priorityTier = 3;
    private string _guildId = default!;

    private HttpClient _httpClient = new();
    private ISawmill _sawmill = default!;


    private Dictionary<NetUserId, SponsorData> _cachedSponsors = new();

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

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
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

    private async void OnPlayerVerified(object? sender, ICommonSession e)
    {
        if (!_enabled)
            return;

        var roles = await GetRoles(e.UserId);
        if (roles == null)
            return;

        var isGiven = await IsGiven(e.UserId);

        var level = SponsorData.ParseRoles(roles);
        var contrib = SponsorData.ParseContrib(roles);
        if (level == SponsorLevel.None && !contrib)
            return;

        var data = new SponsorData(level, e.UserId, isGiven);
        data.Contributor = contrib;
        _cachedSponsors.Add(e.UserId, data);

        _sawmill.Info($"{e.UserId} is sponsor now.\nUserId: {e.UserId}. Level: {Enum.GetName(data.Level)}:{(int)data.Level}");
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

    private async Task<bool> IsGiven(NetUserId userId)
    {
        // Hotfix.
        // TODO: Use extra table from auth API.
        return true;
    }

    public async Task SetGiven(NetUserId userId, bool given)
    {
        // var requestUrl = $"{_apiUrl}/given?id={userId}&given={(given ? 1 : 0)}&method=ss14&api_token={_apiKey}";
        // var response = await _httpClient.PostAsync(requestUrl, null);
        // if (!response.IsSuccessStatusCode)
        //     _sawmill.Error($"Error setting given value for {userId}");
        //
        // if (TryGetInfo(userId, out var data))
        // {
        //     data.IsGiven = given;
        // }
    }

    public async Task MakeWipe()
    {
        var requestUrl = $"{_apiUrl}/wipe_given?api_token={_apiKey}";
        var response = await _httpClient.PostAsync(requestUrl, null);
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
        ref List<ShopCategory>? sponsorCategories,
        ref List<ShopCategory>? contribCategories,
        ShopComponent comp)
    {
        //sponsorCategories ??= [];
        //contribCategories ??= [];

        //var sponsorDict = comp.Categories.Spons;
        //var contribList = comp.ContributorCategories;

        //if (!TryGetInfo(session.UserId, out var info))
        //{
        //    sponsorCategories = null;
        //    contribCategories = null;
        //    return;
        //}

        //var found = sponsorDict
        //    .Where(kv => kv.Key <= (int) info.Level)
        //    .SelectMany(kv => kv.Value)
        //    .ToList();

        //sponsorCategories = found.Count == 0 ? null : found;
        //contribCategories = !info.Contributor ? null : contribList;
    }

    private sealed class RolesResponse
    {
        [JsonPropertyName("roles")]
        public string[] Roles { get; set; } = [];
    }
}

public sealed class SponsorData
{
    public static readonly Dictionary<string, SponsorLevel> RolesMap = new()
    {
        { "1172510785415684136", SponsorLevel.Bread },
        { "1158896573569318933", SponsorLevel.Backer },
        { "1233831223118266398", SponsorLevel.OMind },
        { "1233831339682172979", SponsorLevel.Pseudogiant }
    };

    public const string ContribRole = "1173624167053140108";

    public static SponsorLevel ParseRoles(List<string> roles)
    {
        var highestRole = SponsorLevel.None;
        foreach (var role in roles)
        {
            if (!RolesMap.ContainsKey(role))
                continue;

            if ((int)RolesMap[role] > (int)highestRole)
                highestRole = RolesMap[role];
        }

        return highestRole;
    }

    public static bool ParseContrib(List<string> roles)
    {
        foreach (var role in roles)
        {
            if (ContribRole == role)
                return true;
        }

        return false;
    }

    public SponsorData(SponsorLevel level, NetUserId userId, bool given)
    {
        Level = level;
        UserId = userId;
        IsGiven = given;
    }

    public SponsorLevel Level;
    public NetUserId UserId;
    public bool IsGiven;
    public bool Contributor = false;
}
