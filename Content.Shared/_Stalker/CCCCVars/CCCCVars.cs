using Robust.Shared.Configuration;

namespace Content.Shared._Stalker.CCCCVars;
/// <summary>
/// Stalker modules console variables
/// </summary>
[CVarDefs]
// ReSharper disable once InconsistentNaming
public sealed partial class CCCCVars
{
    /*
     * Bans
     */

    /// <summary>
    /// URL where all ban messages will be relayed
    /// </summary>
    public static readonly CVarDef<bool> DiscordBanWebhookEnabled =
        CVarDef.Create("stalker.ban_webhook_enabled", false, CVar.SERVERONLY);

    /// <summary>
    /// URL where all ban messages will be relayed
    /// </summary>
    public static readonly CVarDef<string> DiscordBanWebhook =
        CVarDef.Create("stalker.ban_webhook", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// The server icon to use in the Discord ban embed footer.
    /// </summary>
    public static readonly CVarDef<string> DiscordBanFooterIcon =
        CVarDef.Create("stalker.ban_footer_icon", string.Empty, CVar.SERVERONLY);

    /// <summary>
    /// The avatar to use for the webhook. Should be an URL.
    /// </summary>
    public static readonly CVarDef<string> DiscordBanAvatar =
        CVarDef.Create("stalker.ban_avatar", string.Empty, CVar.SERVERONLY);

    /*
     * Server API
     */
    public static readonly CVarDef<string> ServerAPIToken =
        CVarDef.Create("stalker.server_token", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /*
     * Stalker sin
     */

    /// <summary>
    /// Webhook url to send notification messages
    /// </summary>
    public static readonly CVarDef<string> DiscordSinLightMessageWebhook =
        CVarDef.Create("stalker.sin_light_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /*
     * Discord Auth
     */
    public static readonly CVarDef<bool> DiscordAuthEnabled =
        CVarDef.Create("stalker.discord_auth_enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<string> DiscordAuthUrl =
        CVarDef.Create("stalker.discord_auth_url", "http://127.0.0.1:2424/api", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> DiscordAuthToken =
        CVarDef.Create("stalker.discord_auth_token", "key", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /*
     * Stalker Queue
     */
    public static readonly CVarDef<bool> QueueEnabled =
        CVarDef.Create("stalker.queue_enabled", false, CVar.SERVERONLY);

    /*
    * Stalker PDA Messenger
    */

    public static readonly CVarDef<string> DiscordPdaMessageWebhook =
        CVarDef.Create("discord.round_pda_message_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /*
    * Stalker OpenRouter KEY
    */
    public static readonly CVarDef<string> OpenRouterApiKey =
        CVarDef.Create("openrouter.apikey", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> OpenRouterModel =
        CVarDef.Create("openrouter.model", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> OpenRouterUrl =
        CVarDef.Create("openrouter.url", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);
}
