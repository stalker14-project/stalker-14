using Content.Server.Administration.Logs;
using Content.Server.CartridgeLoader;
using Content.Server.Discord;
using Content.Server.PDA;
using Content.Server.PDA.Ringer;
using Content.Shared._Stalker.PdaMessenger;
using Content.Shared.CartridgeLoader;
using Content.Shared._Stalker.CCCCVars;
using Content.Shared.Database;
using Content.Shared.PDA;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Content.Server.Mind;

namespace Content.Server._Stalker.PdaMessenger;

public sealed class PdaMessengerSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly DiscordWebhook _discord = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly PdaSystem _pda = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RingerSystem _ringer = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly List<PdaChat> _chats = new() { new PdaChat("Общий") };
    private WebhookIdentifier? _webhookIdentifier;

    public override void Initialize()
    {
        SubscribeLocalEvent<PdaMessengerComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<PdaMessengerComponent, CartridgeUiReadyEvent>(OnUiReady);

        _configurationManager.OnValueChanged(CCCCVars.DiscordPdaMessageWebhook, value =>
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _discord.GetWebhook(value, data => _webhookIdentifier = data.ToIdentifier());
            }
        }, true);
    }

    private void OnUiReady(Entity<PdaMessengerComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUiState(ent, args.Loader, ent.Comp);
    }

    private void OnUiMessage(Entity<PdaMessengerComponent> messenger, ref CartridgeMessageEvent args)
    {
        if (messenger.Comp.NextSendTime > _timing.CurTime)
            return;

        messenger.Comp.NextSendTime = _timing.CurTime + messenger.Comp.SendTimeCooldown;
        SendMessage(messenger, ref args);
    }

    private void SendMessage(Entity<PdaMessengerComponent> messenger, ref CartridgeMessageEvent args)
    {
        var user = messenger.Owner;
        if (!Exists(user))
            return;

        if (!TryComp<PdaComponent>(GetEntity(args.LoaderUid), out var senderPda))
            return;

        if (!_mind.TryGetMind(args.Actor, out _, out var mindComp))
            return;

        senderPda.OwnerName = mindComp.CharacterName;

        if (args is MessengerUiSetLoginEvent messageOwner)
        {
            //var meta = MetaData(messageOwner); ST-TODO: Need to check fo owner
            //_pda.SetOwner(GetEntity(args.LoaderUid), senderPda, messageOwner, meta.EntityName);
            UpdateUiState(messenger, GetEntity(args.LoaderUid), messenger.Comp);
        }

        if (args is not MessengerUiMessageEvent message)
            return;

        _adminLogger.Add(LogType.PdaMessage, LogImpact.Medium, $"{ToPrettyString(user):player} send message to {message.Message.Receiver}, title: {message.Message.Title}, content: {message.Message.Content}");

        message.Message.Title = $"{senderPda.OwnerName}: {message.Message.Title}";
        if (message.Message.Receiver == "Общий")
        {
            _chats[0].Messages.Add(message.Message);
            SendMessageDiscordMessage(message.Message, senderPda.OwnerName);
            UpdateUiState(messenger, GetEntity(args.LoaderUid), messenger.Comp);
            TryNotify();
            return;
        }

        var query = EntityQueryEnumerator<PdaComponent>();
        while (query.MoveNext(out var uid, out var pda))
        {
            if (message.Message.Receiver != pda.OwnerName)
                continue;

            var sended = false;
            foreach (var chat in _chats)
            {
                if (chat.Sender != senderPda.OwnerName || chat.Receiver != message.Message.Receiver)
                    continue;

                chat.Messages.Add(message.Message);
                sended = true;
                break;
            }

            if (sended)
                continue;

            var newChat = new PdaChat($"Отправитель {senderPda.OwnerName}", message.Message.Receiver, senderPda.OwnerName);
            newChat.Messages.Add(message.Message);
            _chats.Add(newChat);
        }

        if (TryComp<RingerComponent>(GetEntity(args.LoaderUid), out var ringer))
           _ringer.RingerPlayRingtone((GetEntity(args.LoaderUid), ringer));

        UpdateUiState(messenger, GetEntity(args.LoaderUid), messenger.Comp);
    }

    private void UpdateUiState(EntityUid uid, EntityUid loaderUid, PdaMessengerComponent component)
    {
        if (!TryComp<PdaComponent>(loaderUid, out var pda))
            return;

        var chats = new List<PdaChat>();
        foreach (var chat in _chats)
        {
            if (chat.Receiver is not null && chat.Receiver != pda.OwnerName)
                continue;

            chats.Add(chat);
        }

        var state = new MessengerUiState(chats);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

    private void TryNotify()
    {
        var query = EntityQueryEnumerator<CartridgeLoaderComponent, RingerComponent, ContainerManagerComponent>();
        while (query.MoveNext(out var uid, out var comp, out var ringer, out var cont))
        {
            if (!_cartridgeLoader.TryGetProgram<PdaMessengerComponent>(uid, out _, out _, false, comp, cont))
                continue;

            _ringer.RingerPlayRingtone((uid, ringer));
        }
    }

    private async void SendMessageDiscordMessage(PdaMessage message, string? author)
    {
        try
        {
            if (_webhookIdentifier is null)
                return;

            var payload = new WebhookPayload
            {
                Content = $"### {message.Title}\n``{message.Content}``",
            };

            await _discord.CreateMessage(_webhookIdentifier.Value, payload);
        }
        catch (Exception e)
        {
            Log.Error($"Error while sending discord round start message:\n{e}");
        }
    }
}
