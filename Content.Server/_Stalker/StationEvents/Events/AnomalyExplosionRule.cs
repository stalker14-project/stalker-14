using System.Numerics;
using Content.Server._Stalker.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Camera;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Light.Components;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.StationEvents.Events;

public sealed class AnomalyExplosionRule : StationEventSystem<AnomalyExplosionRuleComponent>
{
    [Dependency] private readonly IPlayerManager _playersManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _sharedCameraRecoil = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    protected override void Added(EntityUid uid, AnomalyExplosionRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        component.DamageStarts = _timing.CurTime + component.DamageStartsDelay;

        SetAmbientLightColor(component.ScreenColor);
        _audio.PlayGlobal(component.SoundStart, Filter.Empty().AddAllPlayers(_playersManager), true, AudioParams.Default.WithVolume(-8f));
    }

    protected override void Ended(EntityUid uid, AnomalyExplosionRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        SetAmbientLightColor();
        _audio.PlayGlobal(component.SoundEnd, Filter.Empty().AddAllPlayers(_playersManager), true, AudioParams.Default.WithVolume(-8f));
    }

    protected override void ActiveTick(EntityUid uid, AnomalyExplosionRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        var doDamage = false;
        if (component.DamageNext < _timing.CurTime && component.DamageStarts < _timing.CurTime)
        {
            doDamage = true;
            component.DamageNext = _timing.CurTime + component.DamageNextDelay;
        }

        var query = EntityQueryEnumerator<BlowoutTargetComponent, TransformComponent, DamageableComponent>();
        while (query.MoveNext(out var target, out _, out var transform, out var damageable))
        {
            if (HasComp<StalkerSafeZoneComponent>(target))
                continue;

            if (HasComp<StalkerSafeZoneComponent>(_mapManager.GetMapEntityId(transform.MapID)))
                continue;

            if (doDamage && component.Damage is not null)
                _damageableSystem.TryChangeDamage(target, component.Damage, interruptsDoAfters: false);

            var kick = new Vector2(_random.NextFloat(), _random.NextFloat()) * component.ShakeStrength;
            _sharedCameraRecoil.KickCamera(target, kick);
        }
    }

    private void SetAmbientLightColor(Color? colorToSet = null)
    {
        var query = EntityQueryEnumerator<LightCycleComponent>();
        while (query.MoveNext(out var uid, out var light))
        {
            light.OriginalColor = colorToSet ?? light.UnchangedOriginalColor;

            Dirty(uid, light);
        }
    }
}

