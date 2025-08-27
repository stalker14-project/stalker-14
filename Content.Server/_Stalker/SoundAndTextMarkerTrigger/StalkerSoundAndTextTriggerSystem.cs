using Robust.Server.GameObjects;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Content.Shared.Sound;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using System.Threading.Tasks;
using Content.Server.Database;
using Robust.Shared.Prototypes;
using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Server.Chat.Systems;
using System.Linq;
using Content.Shared.Popups;
using Robust.Shared.Timing;

using Content.Shared.Access.Systems;  

namespace Content.Server._Stalker.SoundAndTextTrigger;

public sealed class StalkerSoundAndTextTriggerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StalkerSoundAndTextTriggerComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<StalkerSoundAndTextTriggerComponent, EndCollideEvent>(OnEndCollide);
    }

    private void OnStartCollide(EntityUid uid, StalkerSoundAndTextTriggerComponent component, ref StartCollideEvent args)
    {
        if (!component.AllowAll)
        {
            if (!_accessReaderSystem.IsAllowed(args.OtherEntity, args.OurEntity))
                return;
        }

        if (_timing.CurTime < component.CooldownTime + component.LastUsed)
            return;

        if (!_random.Prob(Math.Clamp(component.Chance, 0f, 1f)))
            return;

        if (component.Sound == null)
            return;

        _audioSystem.PlayPvs(component.Sound, uid);

        if (component.Text != null)
        {
            var message = component.Text;
            var mapCoords = _transformSystem.GetMapCoordinates(uid);
            var filter = Filter.Empty().AddInRange(mapCoords, ChatSystem.VoiceRange);
            _chatManager.ChatMessageToManyFiltered(
                filter,
                ChatChannel.Emotes,
                message,
                message,
                uid,
                false,
                true,
                colorOverride: Color.Gold);
        }
        component.LastUsed = _timing.CurTime;
    }

    private void OnEndCollide(EntityUid uid, StalkerSoundAndTextTriggerComponent component, ref EndCollideEvent args)
    {
        if (!component.AllowAll)
        {
            if (!_accessReaderSystem.IsAllowed(args.OtherEntity, args.OurEntity))
                return;
        }

        if (_timing.CurTime < component.CooldownTime + component.LastUsed)
            return;

            if (!_random.Prob(Math.Clamp(component.Chance, 0f, 1f)))
                return;

        if (component.SoundExit == null)
            return;

        _audioSystem.PlayPvs(component.SoundExit, uid);

        component.LastUsed = _timing.CurTime;
    }
}

