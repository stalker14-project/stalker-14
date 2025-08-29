using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Server._Stalker.Discord.DiscordAuth;
using Content.Shared._Stalker.CCCCVars;
using Content.Shared._Stalker.Sponsors.Messages;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Stalker.Sponsors.SponsorManager;

public sealed partial class SponsorsManager
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly DiscordAuthManager _discordAuthManager = default!;
    [Dependency] private readonly INetManager _netMgr = default!;
    private readonly HttpClient _httpClient = new();
    private readonly Dictionary<NetUserId, SponsorData> _cachedSponsors = new();

    private string _apiUrl = string.Empty;
    private string _apiKey = string.Empty;
    private bool _enabled;
    private string _guildId = null!;
    private ISawmill _sawmill = null!;

    public event Action<NetUserId>? SponsorPlayerCached;

    public Dictionary<NetUserId, SponsorData> Sponsors => _cachedSponsors;

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("sponsors");
        _netMgr.RegisterNetMessage<MsgSponsorVerified>();

        _cfg.OnValueChanged(CCCCVars.DiscordAuthEnabled, val => { _enabled = val; }, true);
        _cfg.OnValueChanged(CCCCVars.SponsorsApiUrl, val => { _apiUrl = val; }, true);
        _cfg.OnValueChanged(CCCCVars.SponsorsApiKey, val => { _apiKey = val; }, true);
        _cfg.OnValueChanged(CCCCVars.SponsorsGuildId, val => { _guildId = val; }, true);

        _discordAuthManager.PlayerVerified += OnPlayerVerified;
        _netMgr.Disconnect += OnDisconnect;

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiKey);

        InitializeHelpers();
        InitializeSpecies();
    }

    private void OnDisconnect(object? sender, NetDisconnectedArgs e)
    {
        _cachedSponsors.Remove(e.Channel.UserId);
    }

    public bool TryGetInfo(NetUserId userId, [NotNullWhen(true)] out SponsorData? sponsorInfo)
    {
        return _cachedSponsors.TryGetValue(userId, out sponsorInfo);
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

    private async void OnPlayerVerified(object? sender, ICommonSession e)
    {
        if (!_enabled)
            return;

        var roles = await GetRoles(e.UserId);
        if (roles is null)
            return;

        var isGiven = await IsGiven(e.UserId);

        var sponsorPrototype = TryGetSponsorPrototype(roles);
        var contributorPrototype = TryGetContributorPrototype(roles);
        if (sponsorPrototype is null &&
            contributorPrototype is null)
            return;

        var data = new SponsorData(
            sponsorPrototype?.ID,
            e.UserId,
            isGiven,
            contributorPrototype is not null
        );

        _cachedSponsors.Add(e.UserId, data);
        SponsorPlayerCached?.Invoke(e.UserId);
        _netMgr.ServerSendMessage(new MsgSponsorVerified(), e.Channel);

        _sawmill.Debug($"{e.UserId} is sponsor now. PrototypeID: {data.SponsorProtoId}");
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
