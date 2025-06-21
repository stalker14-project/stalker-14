using Content.Shared.Chat;
using Robust.Shared.Player;

namespace Content.Server._Stalker.Chat;

[ByRefEvent]
public record struct STChatMessageOverrideInVoiceRangeEvent(ICommonSession HearingSession, ChatChannel Channel, EntityUid Source, string Message, string WrappedMessage, bool EntHideChat);
