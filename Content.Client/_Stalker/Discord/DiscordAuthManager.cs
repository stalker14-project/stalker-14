using Content.Shared._Stalker.Discord.Messages;
using Robust.Client.State;
using Robust.Shared.Network;

namespace Content.Client._Stalker.Discord;

public sealed class DiscordAuthManager
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IStateManager _state = default!;

    public string AuthLink = default!;
    public string ErrorMessage = default!;
    public const string DiscordServerLink = "https://discord.gg/pBFv9pDuqK";

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgDiscordAuthRequired>(OnDiscordAuthRequired);
    }

    public void OnDiscordAuthRequired(MsgDiscordAuthRequired args)
    {
        AuthLink = args.Link;
        ErrorMessage = args.ErrorMessage;
        _state.RequestStateChange<DiscordAuthState>();
    }
}
