using Content.Server._Stalker.Anomaly.Effects.Components;
using Content.Server._Stalker.Dimension;
using Content.Server._Stalker.Utils;
using Content.Shared._Stalker.Anomaly.Triggers.Events;
using Content.Shared.Whitelist;

namespace Content.Server._Stalker.Anomaly.Effects.Systems;

public sealed class STAnomalyEffectEnterDimensionSystem : EntitySystem
{
    [Dependency] private readonly STDimensionSystem _dimension = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalyEffectEnterDimensionComponent, STAnomalyTriggerEvent>(OnTriggered);
    }

    private void OnTriggered(Entity<STAnomalyEffectEnterDimensionComponent> effect, ref STAnomalyTriggerEvent args)
    {
        foreach (var group in args.Groups)
        {
            if (!effect.Comp.Options.TryGetValue(group, out var options))
                continue;

            var entities =
                _entityLookup.GetEntitiesInRange<TransformComponent>(Transform(effect).Coordinates, options.Range);

            foreach (var entity in entities)
            {
                if (entity.Comp.Anchored)
                    continue;

                if (!STUtilsMap.InWorld((entity, entity), EntityManager))
                    continue;

                if (_whitelistSystem.IsWhitelistFail(options.Whitelist, entity))
                    continue;

                _dimension.EnterDimension(entity, options.Dimension);
            }
        }
    }
}
