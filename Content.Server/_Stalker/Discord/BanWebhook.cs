using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Content.Server.Discord;
using Content.Shared._Stalker.CCCCVars;
using Robust.Shared.Configuration;

namespace Content.Server._Stalker.Discord;
public sealed class BanWebhook
{
    [Dependency] private readonly IConfigurationManager _config = default!;

    private ISawmill _sawmill = default!;
    private readonly HttpClient _httpClient = new();
    private string _webhookUrl = string.Empty; // TODO: This shit had to be used in GenerateWebhook()
    private string _footerIconUrl = string.Empty;
    private string _avatarUrl = string.Empty;
    private bool _enabled = false;

    public void Initialize()
    {
        _config.OnValueChanged(CCCCVars.DiscordBanWebhookEnabled, val => _enabled = val, true);
        _config.OnValueChanged(CCCCVars.DiscordBanWebhook, OnWebhookChanged, true);
        _config.OnValueChanged(CCCCVars.DiscordBanFooterIcon, OnFooterIconChanged, true);
        _config.OnValueChanged(CCCCVars.DiscordBanAvatar, OnAvatarChanged, true);

        _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("BAN");
    }

    private async void OnWebhookChanged(string url)
    {
        _webhookUrl = url;

        if (url == string.Empty)
            return;

        var match = Regex.Match(url, @"^https://discord\.com/api/webhooks/(\d+)/((?!.*/).*)$");

        if (!match.Success)
        {
            _sawmill.Warning("Webhook URL does not appear to be valid. Using anyways...");
        }
    }

    public async void GenerateWebhook(string admin, string user, string? banId, string severity, uint? minutes, string reason)
    {
        if (!_enabled)
            return;

        var payload = GenerateBanPayload(admin, user, banId, severity, minutes, reason);
        var request = await _httpClient.PostAsync($"{_config.GetCVar(CCCCVars.DiscordBanWebhook)}?wait=true", // Idk why, but it doesn't set _webhookUrl xd
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        var content = await request.Content.ReadAsStringAsync();

        if (request.IsSuccessStatusCode)
            return;

        _sawmill.Log(LogLevel.Error, $"Discord returned bad status code when posting message (perhaps the message is too long?): {request.StatusCode}\nResponse: {content}");
    }
    private WebhookPayload GenerateBanPayload(string admin, string user, string? banId, string severity, uint? minutes, string reason)
    {
        if (minutes == null)
            return new WebhookPayload();

        var banType = Loc.GetString("ban-embed-perm");
        var timeNow = DateTime.Now.ToUniversalTime();
        var color = 0xFF0000;
        var expires = string.Empty;

        if (minutes != 0)
        {
            var expirationDate = DateTime.UtcNow.Add(TimeSpan.FromMinutes((double) minutes));
            banType = Loc.GetString("ban-embed-temp", ("time", minutes));
            expires = $"**Истекает:** {expirationDate}\n";
            color = 0xFF9900;
        }

        return new WebhookPayload
        {
            AvatarUrl = string.IsNullOrWhiteSpace(_avatarUrl) ? null : _avatarUrl,
            Embeds = new List<WebhookEmbed>
            {
                new()
                {
                    Color = color,
                    Title = $"{banType} #{banId ?? "error"}",
                    Footer = new WebhookEmbedFooter
                    {
                        Text = Loc.GetString("ban-embed-footer", ("severity", severity)),
                        IconUrl = string.IsNullOrWhiteSpace(_footerIconUrl) ? null : _footerIconUrl
                    },
                    Description =
                        $"**Нарушитель**: {user.Split(" ")[0]}\n" +
                        $"**Администратор:** {admin}\n" +
                        $"\n" +
                        $"**Выдан:** {timeNow}\n" +
                        expires +
                        $"\n" +
                        $"**Причина:** {reason}"
                }
            },
        };
    }
    private void OnFooterIconChanged(string url)
    {
        _footerIconUrl = url;
    }

    private void OnAvatarChanged(string url)
    {
        _avatarUrl = url;
    }
}
