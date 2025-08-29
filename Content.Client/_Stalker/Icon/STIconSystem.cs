using Content.Shared.Throwing;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map.Components;

namespace Content.Client._Stalker.Icon;

public sealed class STIconSystem : EntitySystem
{
    [Dependency] private readonly IResourceCache _resCache = default!;

    private EntityQuery<SpriteComponent> _spriteQuery;

    public override void Initialize()
    {
        base.Initialize();

        _spriteQuery = GetEntityQuery<SpriteComponent>();

        SubscribeLocalEvent<STIconComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<STIconComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<STIconComponent, ThrownEvent>(OnThrown);
    }

    private void OnComponentInit(Entity<STIconComponent> entity, ref ComponentInit args)
    {
#if DEBUG
        if (!HasComp<AppearanceComponent>(entity))
            Log.Error($"Requires an {nameof(AppearanceComponent)} for {entity}");
#endif

        if (_spriteQuery.TryComp(entity, out var spriteComponent))
        {
            foreach (var key in entity.Comp.Layers)
            {
                if (!spriteComponent.LayerMapTryGet(key, out var index))
                    continue;

                if (!spriteComponent.TryGetLayer(index, out var layer))
                    continue;

                entity.Comp.CachedTexture[key] = layer.RSI;
            }
        }

        Update(entity);
    }

    private void OnParentChanged(Entity<STIconComponent> entity, ref EntParentChangedMessage args)
    {
        Update(entity);
    }

    private void OnThrown(Entity<STIconComponent> entity, ref ThrownEvent args)
    {
        // Idk, but throw don't call reparent
        Update(entity, args.User);
    }

    private void Update(Entity<STIconComponent> entity, EntityUid? parent = null)
    {
        parent ??= Transform(entity).ParentUid;
        var inWorld = HasComp<MapComponent>(parent) || HasComp<MapGridComponent>(parent);

        if (!_spriteQuery.TryComp(entity, out var spriteComponent))
            return;

        if (inWorld)
        {
            foreach (var key in entity.Comp.Layers)
            {
                if (!spriteComponent.LayerMapTryGet(key, out var index))
                    continue;

                if (!spriteComponent.TryGetLayer(index, out var layer))
                    continue;

                if (!_resCache.TryGetResource<RSIResource>($"/Textures/{entity.Comp.Sprite}", out var rsiResource))
                {
                    Log.Warning($"Failed to load RSI at path '{entity.Comp.Sprite}' for layer '{key}'");
                    continue;
                }

                layer.SetRsi(rsiResource.RSI);
            }
            return;
        }

        foreach (var key in entity.Comp.Layers)
        {
            if (!spriteComponent.LayerMapTryGet(key, out var index))
                continue;

            if (!spriteComponent.TryGetLayer(index, out var layer))
                continue;

            if (!entity.Comp.CachedTexture.TryGetValue(key, out var rsi))
            {
                Log.Warning($"No RSI path defined for layer '{key}' in CachedTexture for {ToPrettyString(entity)}");
                continue;
            }

            layer.SetRsi(rsi);
        }
    }
}
