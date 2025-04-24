using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._Stalker.CCCCVars;
using Content.Shared._Stalker.Discord;
using Content.Shared._Stalker.Discord.Messages;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Stalker.Discord.DiscordAuth;

public sealed class DiscordAuthManager : IPostInjectInit
{
    [Dependency] private readonly IServerNetManager _netMgr = default!;
    [Dependency] private readonly IPlayerManager _playerMgr = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private ISawmill _sawmill = default!;

    private readonly HttpClient _httpClient = new();

    private bool _enabled = false;
    private string _apiUrl = string.Empty;
    private string _apiKey = string.Empty;
    private string _discordGuild = String.Empty;
    public event EventHandler<ICommonSession>? PlayerVerified;

    public void PostInject()
    {
        IoCManager.InjectDependencies(this);
    }

    public void Initialize()
    {
        _sawmill = Logger.GetSawmill("discordAuth");

        _cfg.OnValueChanged(CCCCVars.DiscordAuthEnabled, v => _enabled = v, true);
        _cfg.OnValueChanged(CCCCVars.DiscordAuthUrl, v => _apiUrl = v, true);
        _cfg.OnValueChanged(CCCCVars.DiscordAuthToken, v => _apiKey = v, true);
        _cfg.OnValueChanged(CCCCVars.SponsorsGuildId, v => _discordGuild = v, true);

        _netMgr.RegisterNetMessage<MsgDiscordAuthRequired>();
        _netMgr.RegisterNetMessage<MsgDiscordAuthCheck>(OnAuthCheck);

        _playerMgr.PlayerStatusChanged += OnPlayerStatusChanged;

        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
    }


    private async void OnAuthCheck(MsgDiscordAuthCheck msg)
    {
        var data = await IsVerified(msg.MsgChannel.UserId);
        if (!data.Status)
            return;

        var session = _playerMgr.GetSessionById(msg.MsgChannel.UserId);
        PlayerVerified?.Invoke(this, session);
    }

    private async void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Connected)
            return;

        if (!_enabled)
        {
            PlayerVerified?.Invoke(this, args.Session);
            return;
        }


        var data = await IsVerified(args.Session.UserId);
        if (data.Status)
        {
            PlayerVerified?.Invoke(this, args.Session);
            return;
        }

        var link = await GenerateLink(args.Session.UserId);
        var message = new MsgDiscordAuthRequired
        {
            Link = link ?? "",
            ErrorMessage = data.ErrorMessage ?? "",
        };
        args.Session.Channel.SendMessage(message);
    }

    public async Task<DiscordData> IsVerified(NetUserId userId, CancellationToken cancel = default)
    {
        _sawmill.Debug($"Player {userId} check Discord verification");

        var requestUrl = $"{_apiUrl}/uuid?method=uid&id={userId}";
        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

        // try catch block to catch HttpRequestExceptions due to remote service unavailability
        try
        {
            var response = await _httpClient.SendAsync(request, cancel);
            if (!response.IsSuccessStatusCode)
                return new DiscordData(false, null, "You are not authorized!");

            var discordUuid = await response.Content.ReadFromJsonAsync<DiscordUuidResponse>(cancel);

            var isInGuild = await CheckGuild(userId, cancel);
            if (!isInGuild)
                return new DiscordData(false, null, "You are not in our guild!");

            if (discordUuid is null)
                return new DiscordData(false, null, "Response from authorization is empty!");

            return new DiscordData(true, new DiscordUserData(userId, discordUuid.DiscordId));
        }
        catch (HttpRequestException)
        {
            _sawmill.Error("Remote auth service is unreachable. Check if its online!");
            return new DiscordData(false, null, "Remote auth service is unreachable.");
        }
        catch (Exception e)
        {
            _sawmill.Error($"Unexpected error verifying user via auth service. Error: {e.Message}. Stack: \n{e.StackTrace}");
            return new DiscordData(false, null, "Unexpected error verifying user via auth service.");
        }
    }

    private async Task<bool> CheckGuild(NetUserId userId, CancellationToken cancel = default)
    {
        var requestUrl = $"{_apiUrl}/guilds?method=uid&id={userId}";
        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        var response = await _httpClient.SendAsync(request, cancel);
        if (!response.IsSuccessStatusCode)
            return false;

        var guilds = await response.Content.ReadFromJsonAsync<DiscordGuildsResponse>(cancel);
        if (guilds is null)
            return false;

        return guilds.Guilds.Any(guild => guild.Id == _discordGuild);
    }

    public async Task<string?> GenerateLink(NetUserId userId, CancellationToken cancel = default)
    {
        _sawmill.Debug($"Generating link for {userId}");
        var requestUrl = $"{_apiUrl}/link?uid={userId}";

        // try catch block to catch HttpRequestExceptions due to remote service unavailability
        try
        {
            var response = await _httpClient.GetAsync(requestUrl, cancel);
            if (!response.IsSuccessStatusCode)
                return null;

            var link = await response.Content.ReadFromJsonAsync<DiscordLinkResponse>(cancel);
            return link!.Link;
        }
        catch (HttpRequestException)
        {
            _sawmill.Error("Remote auth service is unreachable. Check if its online!");
            return null;
        }
        catch (Exception e)
        {
            _sawmill.Error($"Unexpected error verifying user via auth service. Error: {e.Message}. Stack: \n{e.StackTrace}");
            return null;
        }
    }

    private sealed class DiscordUuidResponse
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; } = null!;

        [JsonPropertyName("discord_id")]
        public string DiscordId { get; set; } = null!;
    }

    private sealed class DiscordGuildsResponse
    {
        [JsonPropertyName("guilds")]
        public DiscordGuild[] Guilds { get; set; } = [];
    }

    private sealed class DiscordGuild
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = null!;
    }
}

public sealed class DiscordLinkResponse()
{
    public string Link { get; set; } = default!;
}
