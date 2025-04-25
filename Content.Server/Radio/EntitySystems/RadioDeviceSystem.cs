using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Interaction;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.Components;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Shared._Stalker.RadioStalker;
using Content.Shared._Stalker.RadioStalker.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Radio;
using Content.Shared.Chat;
using Content.Shared.Radio.Components;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Radio.EntitySystems;

/// <summary>
///     This system handles radio speakers and microphones (which together form a hand-held radio).
/// </summary>
public sealed class RadioDeviceSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!; // Stalker-Changes

    // Used to prevent a shitter from using a bunch of radios to spam chat.
    private HashSet<(string, EntityUid, RadioChannelPrototype)> _recentlySent = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadioMicrophoneComponent, ComponentInit>(OnMicrophoneInit);
        SubscribeLocalEvent<RadioMicrophoneComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<RadioMicrophoneComponent, ActivateInWorldEvent>(OnActivateMicrophone);
        SubscribeLocalEvent<RadioMicrophoneComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<RadioMicrophoneComponent, ListenAttemptEvent>(OnAttemptListen);
        SubscribeLocalEvent<RadioMicrophoneComponent, PowerChangedEvent>(OnPowerChanged);

        SubscribeLocalEvent<RadioSpeakerComponent, ComponentInit>(OnSpeakerInit);
        SubscribeLocalEvent<RadioSpeakerComponent, ActivateInWorldEvent>(OnActivateSpeaker);
        SubscribeLocalEvent<RadioSpeakerComponent, RadioReceiveEvent>(OnReceiveRadio);

        SubscribeLocalEvent<IntercomComponent, EncryptionChannelsChangedEvent>(OnIntercomEncryptionChannelsChanged);
        SubscribeLocalEvent<IntercomComponent, ToggleIntercomMicMessage>(OnToggleIntercomMic);
        SubscribeLocalEvent<IntercomComponent, ToggleIntercomSpeakerMessage>(OnToggleIntercomSpeaker);
        SubscribeLocalEvent<IntercomComponent, SelectIntercomChannelMessage>(OnSelectIntercomChannel);

        SubscribeLocalEvent<RadioStalkerComponent, BeforeActivatableUIOpenEvent>(OnBeforeRadioUiOpen); // Stalker-Changes
        SubscribeLocalEvent<RadioStalkerComponent, ToggleRadioMicMessage>(OnToggleRadioMic); // Stalker-Changes
        SubscribeLocalEvent<RadioStalkerComponent, ToggleRadioSpeakerMessage>(OnToggleRadioSpeaker); // Stalker-Changes
        SubscribeLocalEvent<RadioStalkerComponent, SelectRadioChannelMessage>(OnSelectRadioChannel); // Stalker-Changes
        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnStalkerReceiveAttempt, before: new []{typeof(RadioSystem)}); // Stalker-Changes - Add attempt handler
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _recentlySent.Clear();
    }


    #region Component Init
    private void OnMicrophoneInit(EntityUid uid, RadioMicrophoneComponent component, ComponentInit args)
    {
        if (component.Enabled)
            EnsureComp<ActiveListenerComponent>(uid).Range = component.ListenRange;
        else
            RemCompDeferred<ActiveListenerComponent>(uid);
    }

    private void OnSpeakerInit(EntityUid uid, RadioSpeakerComponent component, ComponentInit args)
    {
        if (component.Enabled)
            EnsureComp<ActiveRadioComponent>(uid).Channels.UnionWith(component.Channels);
        else
            RemCompDeferred<ActiveRadioComponent>(uid);
    }
    #endregion

    #region Toggling
    private void OnActivateMicrophone(EntityUid uid, RadioMicrophoneComponent component, ActivateInWorldEvent args)
    {
        if (!args.Complex)
            return;

        if (!component.ToggleOnInteract)
            return;

        ToggleRadioMicrophone(uid, args.User, args.Handled, component);
        args.Handled = true;
    }

    private void OnActivateSpeaker(EntityUid uid, RadioSpeakerComponent component, ActivateInWorldEvent args)
    {
        if (!args.Complex)
            return;

        if (!component.ToggleOnInteract)
            return;

        ToggleRadioSpeaker(uid, args.User, args.Handled, component);
        args.Handled = true;
    }

    public void ToggleRadioMicrophone(EntityUid uid, EntityUid user, bool quiet = false, RadioMicrophoneComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        SetMicrophoneEnabled(uid, user, !component.Enabled, quiet, component);
    }

    private void OnPowerChanged(EntityUid uid, RadioMicrophoneComponent component, ref PowerChangedEvent args)
    {
        if (args.Powered)
            return;
        SetMicrophoneEnabled(uid, null, false, true, component);
    }

    public void SetMicrophoneEnabled(EntityUid uid, EntityUid? user, bool enabled, bool quiet = false, RadioMicrophoneComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (component.PowerRequired && !this.IsPowered(uid, EntityManager))
            return;

        component.Enabled = enabled;

        if (!quiet && user != null)
        {
            var state = Loc.GetString(component.Enabled ? "handheld-radio-component-on-state" : "handheld-radio-component-off-state");
            var message = Loc.GetString("handheld-radio-component-on-use", ("radioState", state));
            _popup.PopupEntity(message, user.Value, user.Value);
        }

        _appearance.SetData(uid, RadioDeviceVisuals.Broadcasting, component.Enabled);
        if (component.Enabled)
            EnsureComp<ActiveListenerComponent>(uid).Range = component.ListenRange;
        else
            RemCompDeferred<ActiveListenerComponent>(uid);
    }

    public void ToggleRadioSpeaker(EntityUid uid, EntityUid user, bool quiet = false, RadioSpeakerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        SetSpeakerEnabled(uid, user, !component.Enabled, quiet, component);
    }

    public void SetSpeakerEnabled(EntityUid uid, EntityUid? user, bool enabled, bool quiet = false, RadioSpeakerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Enabled = enabled;

        if (!quiet && user != null)
        {
            var state = Loc.GetString(component.Enabled ? "handheld-radio-component-on-state" : "handheld-radio-component-off-state");
            var message = Loc.GetString("handheld-radio-component-on-use", ("radioState", state));
            _popup.PopupEntity(message, user.Value, user.Value);
        }

        _appearance.SetData(uid, RadioDeviceVisuals.Speaker, component.Enabled);
        if (component.Enabled)
            EnsureComp<ActiveRadioComponent>(uid).Channels.UnionWith(component.Channels);
        else
            RemCompDeferred<ActiveRadioComponent>(uid);
    }
    #endregion

    private void OnExamine(EntityUid uid, RadioMicrophoneComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var proto = _protoMan.Index<RadioChannelPrototype>(component.BroadcastChannel);

        using (args.PushGroup(nameof(RadioMicrophoneComponent)))
        {
            args.PushMarkup(Loc.GetString("handheld-radio-component-on-examine", ("frequency", proto.Frequency)));
            args.PushMarkup(Loc.GetString("handheld-radio-component-chennel-examine",
                ("channel", proto.LocalizedName)));
        }
    }

    private void OnListen(EntityUid uid, RadioMicrophoneComponent component, ListenEvent args)
    {
        if (HasComp<RadioSpeakerComponent>(args.Source))
            return; // no feedback loops please.

        var channel = _protoMan.Index<RadioChannelPrototype>(component.BroadcastChannel)!;
        if (_recentlySent.Add((args.Message, args.Source, channel)))
            _radio.SendRadioMessage(args.Source, args.Message, channel, uid);
    }
    private void OnAttemptListen(EntityUid uid, RadioMicrophoneComponent component, ListenAttemptEvent args)
    {
        if (component.PowerRequired && !this.IsPowered(uid, EntityManager)
            || component.UnobstructedRequired && !_interaction.InRangeUnobstructed(args.Source, uid, 0))
        {
            args.Cancel();
        }
    }

    private void OnReceiveRadio(EntityUid uid, RadioSpeakerComponent component, ref RadioReceiveEvent args)
    {
        if (uid == args.RadioSource)
            return;
        // Stalker-Changes-Start
        string name;
        if (args.Channel.ID == "StalkerInternal" && TryComp<RadioStalkerComponent>(uid, out var receiverStalkerComp) && receiverStalkerComp.CurrentFrequency != null)
        {
            // Frequencies matched in OnStalkerReceiveAttempt.
            var speechVerb = _chat.GetSpeechVerb(args.MessageSource, args.Message);
            var wrappedMessage = Loc.GetString("chat-radio-message-wrap",
                ("channel", string.Empty), // receiverStalkerComp.CurrentFrequency
                ("fontType", speechVerb.FontId),
                ("fontSize", speechVerb.FontSize),
                ("verb", string.Empty), // Loc.GetString(speechVerb.ID)
                ("color", args.Channel.Color),
                ("name", Name(args.MessageSource)),
                ("message", args.Message));

            _chat.TrySendInGameICMessage(uid, wrappedMessage, InGameICChatType.Whisper, ChatTransmitRange.GhostRangeLimit, checkRadioPrefix: false);
            return;
        }
        else
        {
            // Default handling for non-StalkerInternal channels
        // Stalker-Changes-End
            var nameEv = new TransformSpeakerNameEvent(args.MessageSource, Name(args.MessageSource));
            RaiseLocalEvent(args.MessageSource, nameEv);
            name = Loc.GetString("speech-name-relay",
                ("speaker", Name(uid)),
                ("originalName", nameEv.VoiceName));

             _chat.TrySendInGameICMessage(uid, args.Message, InGameICChatType.Whisper, ChatTransmitRange.GhostRangeLimit, nameOverride: name, checkRadioPrefix: false);
        }
    }

    private void OnIntercomEncryptionChannelsChanged(Entity<IntercomComponent> ent, ref EncryptionChannelsChangedEvent args)
    {
        ent.Comp.SupportedChannels = args.Component.Channels.Select(p => new ProtoId<RadioChannelPrototype>(p)).ToList();

        var channel = args.Component.DefaultChannel;
        if (ent.Comp.CurrentChannel != null && ent.Comp.SupportedChannels.Contains(ent.Comp.CurrentChannel.Value))
            channel = ent.Comp.CurrentChannel;

        SetIntercomChannel(ent, channel);
    }

    private void OnToggleIntercomMic(Entity<IntercomComponent> ent, ref ToggleIntercomMicMessage args)
    {
        if (ent.Comp.RequiresPower && !this.IsPowered(ent, EntityManager))
            return;

        SetMicrophoneEnabled(ent, args.Actor, args.Enabled, true);
        ent.Comp.MicrophoneEnabled = args.Enabled;
        Dirty(ent);
    }

    private void OnToggleIntercomSpeaker(Entity<IntercomComponent> ent, ref ToggleIntercomSpeakerMessage args)
    {
        if (ent.Comp.RequiresPower && !this.IsPowered(ent, EntityManager))
            return;

        SetSpeakerEnabled(ent, args.Actor, args.Enabled, true);
        ent.Comp.SpeakerEnabled = args.Enabled;
        Dirty(ent);
    }

    private void OnSelectIntercomChannel(Entity<IntercomComponent> ent, ref SelectIntercomChannelMessage args)
    {
        if (ent.Comp.RequiresPower && !this.IsPowered(ent, EntityManager))
            return;

        if (!_protoMan.HasIndex<RadioChannelPrototype>(args.Channel) || !ent.Comp.SupportedChannels.Contains(args.Channel))
            return;

        SetIntercomChannel(ent, args.Channel);
    }

    private void SetIntercomChannel(Entity<IntercomComponent> ent, ProtoId<RadioChannelPrototype>? channel)
    {
        ent.Comp.CurrentChannel = channel;

        if (channel == null)
        {
            SetSpeakerEnabled(ent, null, false);
            SetMicrophoneEnabled(ent, null, false);
            ent.Comp.MicrophoneEnabled = false;
            ent.Comp.SpeakerEnabled = false;
            Dirty(ent);
            return;
        }

        if (TryComp<RadioMicrophoneComponent>(ent, out var mic))
            mic.BroadcastChannel = channel;
        if (TryComp<RadioSpeakerComponent>(ent, out var speaker))
            speaker.Channels = new() { channel };
        Dirty(ent);
    }

 // Stalker-Changes
    private void OnSelectRadioChannel(EntityUid uid, RadioStalkerComponent comp, SelectRadioChannelMessage msg)
    {
        if (comp.RequiresPower && !this.IsPowered(uid, EntityManager))
            return;

        // Store the raw frequency string
        comp.CurrentFrequency = msg.Channel;

        UpdateRadioUi(uid, comp); // Pass component
    }
    private void OnBeforeRadioUiOpen(EntityUid uid, RadioStalkerComponent component, BeforeActivatableUIOpenEvent args)
    {
        UpdateRadioUi(uid, component);
    }
    private void OnToggleRadioMic(EntityUid uid, RadioStalkerComponent component, ToggleRadioMicMessage args)
    {
        if (component.RequiresPower && !this.IsPowered(uid, EntityManager))
            return;

        SetMicrophoneEnabled(uid, args.Actor, args.Enabled, true);
        SetSpeakerEnabled(uid, args.Actor, false, true);
        UpdateRadioUi(uid, component);
    }
    private void OnToggleRadioSpeaker(EntityUid uid, RadioStalkerComponent component, ToggleRadioSpeakerMessage args)
    {
        if (component.RequiresPower && !this.IsPowered(uid, EntityManager))
            return;

        SetSpeakerEnabled(uid, args.Actor, args.Enabled, true);
        SetMicrophoneEnabled(uid, args.Actor, false, true);
        UpdateRadioUi(uid, component);
    }


    private void UpdateRadioUi(EntityUid uid, RadioStalkerComponent? stalkerComp = null)
    {
        if (!Resolve(uid, ref stalkerComp))
            return;

        var micComp = CompOrNull<RadioMicrophoneComponent>(uid);
        var speakerComp = CompOrNull<RadioSpeakerComponent>(uid);

        var micEnabled = micComp?.Enabled ?? false;
        var speakerEnabled = speakerComp?.Enabled ?? false;
        var state = new RadioStalkerBoundUIState(micEnabled, speakerEnabled, stalkerComp.CurrentFrequency);
        _ui.SetUiState(uid, RadioStalkerUiKey.Key, state);
    }
 // Stalker-Changes-Ends

    // Stalker-Changes: New handler to filter messages based on frequency before they are fully processed
    private void OnStalkerReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        // Only filter messages on the internal channel
        if (args.Channel.ID != "StalkerInternal")
            return;

        // Check if the intended receiver is a stalker radio and has a frequency set
        if (!TryComp<RadioStalkerComponent>(args.RadioReceiver, out var receiverStalkerComp) || receiverStalkerComp.CurrentFrequency == null)
        {
            args.Cancelled = true; // Cancel the event for this receiver
            return;
        }

        // Check if the sending radio device is a stalker radio and has a frequency set
        if (!TryComp<RadioStalkerComponent>(args.RadioSource, out var senderStalkerComp) || senderStalkerComp.CurrentFrequency == null)
        {
            args.Cancelled = true; // Cancel the event for this receiver
            return;
        }

        // Check if frequencies match
        if (receiverStalkerComp.CurrentFrequency != senderStalkerComp.CurrentFrequency)
        {
            args.Cancelled = true; // Cancel the event for this receiver
            return;
        }

        // If we reach here, frequencies match, so allow the message to proceed for this receiver.
    }
}
