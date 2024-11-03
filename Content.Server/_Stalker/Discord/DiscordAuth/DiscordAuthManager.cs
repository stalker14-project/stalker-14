using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._Stalker.CCCCVars;
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

    public const string AuthErrorLink = "https://stalkers14.xyz/auth_unavailable.html";

    private readonly Dictionary<NetUserId, DiscordUserData> _cachedDiscordUsers = new();
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

        _netMgr.RegisterNetMessage<MsgDiscordAuthRequired>();
        _netMgr.RegisterNetMessage<MsgDiscordAuthCheck>(OnAuthCheck);
        _netMgr.Disconnect += OnDisconnect;

        _playerMgr.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    private void OnDisconnect(object? sender, NetDisconnectedArgs e)
    {
        _cachedDiscordUsers.Remove(e.Channel.UserId);
    }

    private async void OnAuthCheck(MsgDiscordAuthCheck msg)
    {
        var data = await IsVerified(msg.MsgChannel.UserId);
        if (data is null)
            return;

        var session = _playerMgr.GetSessionById(msg.MsgChannel.UserId);
        _cachedDiscordUsers.TryAdd(session.UserId, data);
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
        if (data is not null)
        {
            _cachedDiscordUsers.TryAdd(args.Session.UserId, data);
            PlayerVerified?.Invoke(this, args.Session);
            return;
        }

        var link = await GenerateLink(args.Session.UserId) ?? AuthErrorLink;
        var message = new MsgDiscordAuthRequired() {Link = link};
        args.Session.Channel.SendMessage(message);
    }

    public async Task<DiscordUserData?> IsVerified(NetUserId userId, CancellationToken cancel = default)
    {
        _sawmill.Debug($"Player {userId} check Discord verification");

        var requestUrl = $"{_apiUrl}/check?userid={userId}&api_token={_apiKey}";

        // try catch block to catch HttpRequestExceptions due to remote service unavailability
        try
        {
            var response = await _httpClient.GetAsync(requestUrl, cancel);
            if (!response.IsSuccessStatusCode)
                return null;

            var discordData = await response.Content.ReadFromJsonAsync<DiscordUserData>(cancel);
            return discordData;
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

    public async Task<string?> GenerateLink(NetUserId userId, CancellationToken cancel = default)
    {
        _sawmill.Debug($"Generating link for {userId}");
        var requestUrl = $"{_apiUrl}/link?userid={userId}&api_token={_apiKey}";

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
}

public sealed class DiscordUserData()
{
    public NetUserId UserId { get; set; }
    public string DiscordId { get; set; } = default!;
}

public sealed class DiscordLinkResponse()
{
    public string Link { get; set; } = default!;
}
