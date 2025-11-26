using Content.Server.Explosion.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Tag;

namespace Content.Server._Stalker.TriggerOnInteract;

public sealed class TriggerOnInteractSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TriggerOnInteractComponent, InteractUsingEvent>(OnInteract);
    }

    private void OnInteract(Entity<TriggerOnInteractComponent> entity, ref InteractUsingEvent args)
    {
        var comp = entity.Comp;
        if (CheckTags(entity, args.User, args.Used))
            return;

        _trigger.Trigger(entity, args.User);
    }

    private bool CheckTags(Entity<TriggerOnInteractComponent> entity, EntityUid user, EntityUid used)
    {
        if (entity.Comp.Tags == null)
            return false;

        foreach (var tag in entity.Comp.Tags)
        {
            if (!_tags.HasTag(used, tag))
                continue;

            _trigger.Trigger(entity, user);
        }

        // Tags field is not empty, so it means that we don't need to trigger entity in any case, tag only
        return true;
    }
}

