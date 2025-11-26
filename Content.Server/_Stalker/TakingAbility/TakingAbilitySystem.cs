using Content.Server.Forensics;
using Content.Shared.Examine;
using Content.Shared.Interaction.Components;
using Content.Shared.Tag;
using Content.Shared.Verbs;

namespace Content.Server._Stalker.TakingAbility;

public sealed class TakingAbilitySystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TakingAbilityComponent, GetVerbsEvent<AlternativeVerb>>(OnAlt);
        SubscribeLocalEvent<TakingAbilityComponent, ExaminedEvent>(OnExamine);
    }

    private void OnAlt(Entity<TakingAbilityComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;

        if (!_tag.HasTag(user, entity.Comp.Tag))
            return;

        AlternativeVerb verb = new()
        {
            Text = "Переключить блокировку",
            Act = () =>
            {
                ToggleRemovable(entity);
            },
            Message = "Переключить блокировку"
        };
        args.Verbs.Add(verb);
    }

    private void OnExamine(Entity<TakingAbilityComponent> entity, ref ExaminedEvent args)
    {
        switch (HasComp<UnremoveableComponent>(entity))
        {
            case true:
            {
                args.PushMarkup("Предмет заблокирован");
                break;
            }
            case false:
            {
                args.PushMarkup("Предмет разблокирован");
                break;
            }
        }
    }

    private void ToggleRemovable(EntityUid entity)
    {
        var xform = Transform(entity);

        // Check for wearing by human
        if (!HasComp<DnaComponent>(xform.ParentUid))
            return;

        if (HasComp<UnremoveableComponent>(entity))
        {
            RemComp<UnremoveableComponent>(entity);
            return;
        }
        AddComp<UnremoveableComponent>(entity);
    }
}
