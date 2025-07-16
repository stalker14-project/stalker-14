using System.Numerics;
using Content.Shared._Stalker.Anomaly;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Physics;

namespace Content.Client._Stalker.Anomaly;

public sealed class STAnomalyTipsOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly EntityLookupSystem _lookup;

    public STAnomalyTipsOverlay()
    {
        IoCManager.InjectDependencies(this);

        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();
        _lookup = _entity.System<EntityLookupSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var eye = args.Viewport.Eye;
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var viewport = args.WorldAABB;

        var handle = args.WorldHandle;

        const float scale = 1f;

        var scaleMatrix = Matrix3x2.CreateScale(new Vector2(scale, scale));
        var rotationMatrix = Matrix3x2.CreateRotation(-(float)rotation.Theta);

        var entities = _entity.EntityQueryEnumerator<STAnomalyTipsComponent, TransformComponent, FixturesComponent>();
        while (entities.MoveNext(out _, out var tips, out var xform, out var fixtures))
        {
            if (xform.MapID != eye?.Position.MapId)
                continue;

            var worldPosition = _transform.GetWorldPosition(xform);
            var worldMatrix = Matrix3x2.CreateTranslation(worldPosition);

            var scaledWorld = Matrix3x2.Multiply(scaleMatrix, worldMatrix);
            var matty = Matrix3x2.Multiply(rotationMatrix, scaledWorld);

            handle.SetTransform(matty);

            var size = fixtures.Fixtures.TryGetValue("fix1", out var fixture)
                ? (int) MathF.Round(MathF.Max(0, fixture.Shape.Radius - 0.5f))
                : 0;

            var texture = _sprite.GetFrame(tips.Icon, TimeSpan.Zero);
            var color = Color.White.WithAlpha(tips.Visibility);

            if (size == 0)
            {
                handle.DrawTexture(texture, tips.Offset, color);
                continue;
            }

            for (var x = -size; x <= size; x++)
            {
                for (var y = -size; y <= size; y++)
                {
                    if (!args.WorldAABB.Contains(worldPosition + new Vector2(x, y) + tips.Offset))
                        continue;

                    handle.DrawTexture(texture, new Vector2(x, y) + tips.Offset, color);
                }
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
