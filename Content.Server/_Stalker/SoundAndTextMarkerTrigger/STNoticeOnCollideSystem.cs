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

namespace Content.Server._Stalker.NoticeOnCollide;
public sealed class STNoticeOnCollideSystem : EntitySystem
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
        SubscribeLocalEvent<STNoticeOnCollideComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<STNoticeOnCollideComponent, EndCollideEvent>(OnEndCollide);
    }

    private void OnStartCollide(Entity<STNoticeOnCollideComponent> ent, ref StartCollideEvent args)
    {
        if (!_accessReaderSystem.IsAllowed(args.OtherEntity, args.OurEntity))
            return;

        if (_timing.CurTime < ent.Comp.CooldownTime + ent.Comp.LastUsed)
            return;

        if (!_random.Prob(Math.Clamp(ent.Comp.Chance, 0f, 1f)))
            return;

        if (ent.Comp.SoundEnter == null)
            return;

        _audioSystem.PlayPvs(ent.Comp.SoundEnter, ent);

        if (ent.Comp.Text != null)
        {
            var message = ent.Comp.Text;
            var mapCoords = _transformSystem.GetMapCoordinates(ent);
            var filter = Filter.Empty().AddInRange(mapCoords, ChatSystem.VoiceRange);
            _chatManager.ChatMessageToManyFiltered(
                filter,
                ChatChannel.Emotes,
                message,
                message,
                ent,
                false,
                true,
                colorOverride: Color.Gold);
        }
        ent.Comp.LastUsed = _timing.CurTime;
    }

    private void OnEndCollide(Entity<STNoticeOnCollideComponent> ent, ref EndCollideEvent args)
    {
        if (!_accessReaderSystem.IsAllowed(args.OtherEntity, args.OurEntity))
            return;

        if (_timing.CurTime < ent.Comp.CooldownTime + ent.Comp.LastUsed)
            return;

        if (!_random.Prob(Math.Clamp(ent.Comp.Chance, 0f, 1f)))
            return;

        if (ent.Comp.SoundExit == null)
            return;

        _audioSystem.PlayPvs(ent.Comp.SoundExit, ent);

        ent.Comp.LastUsed = _timing.CurTime;
    }
}

