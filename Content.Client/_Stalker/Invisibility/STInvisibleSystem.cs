using Content.Shared._Stalker.Invisibility;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Stalker.Invisibility;

public sealed class STInvisibleSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STInvisibleComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<STInvisibleComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<STInvisibleComponent> entity, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return;

        sprite.PostShader = _prototypes.Index<ShaderPrototype>("STInvisible").InstanceUnique();
    }

    private void OnShutdown(Entity<STInvisibleComponent> entity, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(entity))
            return;

        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return;

        sprite.PostShader = null;
    }

    public override void Update(float frameTime)
    {
        var invisible = EntityQueryEnumerator<STInvisibleComponent, SpriteComponent>();
        while (invisible.MoveNext(out _, out var comp, out var sprite))
        {
            sprite.PostShader?.SetParameter("visibility", comp.Opacity);
        }
    }
}
