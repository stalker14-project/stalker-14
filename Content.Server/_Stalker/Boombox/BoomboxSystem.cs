using System.Linq;
using Content.Server._Stalker.Boombox.Cassetes;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Content.Shared.Verbs;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Stalker.Boombox;

public sealed class BoomboxSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BoomboxComponent, ItemSlotInsertAttemptEvent>(OnInject);
        SubscribeLocalEvent<BoomboxComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<BoomboxComponent, GetVerbsEvent<ActivationVerb>>(AddRepeatToogleVerb);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BoomboxComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.CurrentPlaying == null)
                continue;

            if (!TryComp<ItemSlotsComponent>(uid, out var itemSlots))
                continue;

            var cassete = itemSlots.Slots.First().Value.Item;
            if (!TryComp<CasseteComponent>(cassete, out var casseteComponent))
            {
                QueueDel(component.CurrentPlaying.Value.Item1);
                component.CurrentPlaying = null;
                component.SoundTime = TimeSpan.Zero;
                continue;
            }

            if (component.SoundEnd <= _timing.CurTime)
                switch (component.RepeatOn)
                {
                    case true:
                        var entComp = _audio.PlayPvs(casseteComponent.Music, uid, AudioParams.Default.WithVolume(component.Volume).WithMaxDistance(component.MaxDistance));
                        if (entComp == null)
                            break;
                        component.CurrentPlaying = entComp.Value;
                        component.SoundEnd += component.SoundTime; //Add one more time to play to this entity
                        break;
                    case false:
                        component.CurrentPlaying = null;
                        component.SoundTime = TimeSpan.Zero;
                        break;
                }
        }
    }

    private void OnInject(EntityUid uid, BoomboxComponent component, ref ItemSlotInsertAttemptEvent args)
    {
        if (!TryComp<ItemSlotsComponent>(uid, out var _))
            return;
        if (component.CurrentPlaying is null)
            return;

        QueueDel(component.CurrentPlaying.Value.Item1);
        component.CurrentPlaying = null;
        component.SoundTime = TimeSpan.Zero;
    }

    private void OnActivate(EntityUid uid, BoomboxComponent component, ActivateInWorldEvent args)
    {
        if (!TryComp<ItemSlotsComponent>(uid, out var itemSlots))
            return;

        var cassete = itemSlots.Slots.First().Value.Item;
        if (!TryComp<CasseteComponent>(cassete, out var casseteComponent))
            return;

        if (component.CurrentPlaying != null)
        {
            QueueDel(component.CurrentPlaying.Value.Item1);
            component.CurrentPlaying = null;
            component.SoundTime = TimeSpan.Zero;
            return;
        }

        var entComp = _audio.PlayPvs(casseteComponent.Music, uid, AudioParams.Default.WithVolume(component.Volume).WithMaxDistance(component.MaxDistance));
        if (entComp == null)
            return;

        component.CurrentPlaying = entComp;

        component.SoundTime = _audio.GetAudioLength(_audio.GetSound(casseteComponent.Music));
        // And add this time to component.SoundEnd timeSpan (Щоб у майбутньому фіксувати кінець музики для цього компонента)
        component.SoundEnd = component.SoundTime + _timing.CurTime;
    }
    private void AddRepeatToogleVerb(Entity<BoomboxComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        ActivationVerb verb = new()
        {
            Text = ent.Comp.RepeatOn
                ? Loc.GetString("Повтор \u2717")
                : Loc.GetString("Повтор \u2713"),

            Icon = ent.Comp.RepeatOn
                ? new SpriteSpecifier.Texture(new("/Textures/_Stalker/Interface/VerbIcons/refresh-slased.svg.192dpi.png"))
                : new SpriteSpecifier.Texture(new("/Textures/_Stalker/Interface/VerbIcons/refresh.svg.192dpi.png")),

            Act = () => ent.Comp.RepeatOn = !ent.Comp.RepeatOn
        };

        args.Verbs.Add(verb);
    }
}
