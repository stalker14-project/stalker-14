using Content.Shared._DZ.FarGunshot;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._DZ.FarGunshot;

public sealed class FarGunshotSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly TransformSystem _transform = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FarGunshotComponent, FargunshotEvent>(OnFarGunshot);

    }

    public void OnFarGunshot(EntityUid uid, FarGunshotComponent component, FargunshotEvent args)
    {
        if (uid == EntityUid.Invalid || args.GunUid != uid.Id)
            return;

        var shootPos = _transform.GetMapCoordinates(uid);

        var range = component.Range * component.SilencerDecrease;

        if (component.Range <= 14f) // we need this since i want to decrease number of uselles iterations
            return;


        // Create a filter for players who are far enough to hear the distant gunshot,
        // excluding those within close range (14)
        var farSoundFilter = Filter.Empty()
            .AddInRange(shootPos, range)
            .RemoveInRange(shootPos, 14f);

        // TODO:
        // Actually, i think we need to override .AddInRange(MapCoordinates mappos, float range)
        // so it would skip vanilla tiles, so we could decrease amount of iterations for each gunshot
        // but i want to take a look and how it actually work, and maybe after perfomance issue start to
        // rewritign this (торнадыч сказал что лагать не будет)

        // Play the distant gunshot sound globally:
        // - Enabled for replay recording
        // - Not toggleable by the player (forced playback)
        _audio.PlayGlobal(
            component.Sound,
            farSoundFilter,
            recordReplay: true,
            AudioParams.Default
        );
    }

}
