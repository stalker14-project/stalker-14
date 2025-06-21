using Content.Shared._Stalker.Deafness;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Client._Stalker.Deafness;

public sealed class STDeafSystem : EntitySystem
{
    [Dependency] private readonly IAudioManager _audio = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private float _volume = 1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STDeafComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<STDeafComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);


        SubscribeLocalEvent<STDeafComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<STDeafComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        Subs.CVar(_cfg, CVars.AudioMasterVolume, value => _volume = value, true);
    }

    private void OnStartup(Entity<STDeafComponent> entity, ref ComponentStartup _)
    {
        if (_player.LocalEntity == entity)
            _audio.SetMasterGain(0);
    }

    private void OnPlayerAttached(Entity<STDeafComponent> entity, ref LocalPlayerAttachedEvent _)
    {
        _audio.SetMasterGain(0);
    }

    private void OnShutdown(Entity<STDeafComponent> entity, ref ComponentShutdown _)
    {
        if (_player.LocalEntity == entity)
            _audio.SetMasterGain(_volume);
    }

    private void OnPlayerDetached(Entity<STDeafComponent> entity, ref LocalPlayerDetachedEvent _)
    {
        _audio.SetMasterGain(_volume);
    }
}
