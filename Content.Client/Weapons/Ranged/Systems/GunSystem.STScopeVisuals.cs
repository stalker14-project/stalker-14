using Content.Client.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;

namespace Content.Client.Weapons.Ranged.Systems
{
    public partial class GunSystem
    {
        private void InitializeSTScopeVisuals()
        {
            SubscribeLocalEvent<STScopeVisualsComponent, ComponentInit>(OnScopeVisualsInit);
            SubscribeLocalEvent<STScopeVisualsComponent, AppearanceChangeEvent>(OnScopeVisualsChange);
        }


        private void OnScopeVisualsInit(EntityUid uid, STScopeVisualsComponent component, ComponentInit args)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite)) return;

            if (sprite.LayerMapTryGet(GunVisualLayers.Scope, out _))
            {
                sprite.LayerSetState(GunVisualLayers.Scope, $"{component.ScopeState}");
                sprite.LayerSetVisible(GunVisualLayers.Scope, false);
            }

            if (sprite.LayerMapTryGet(GunVisualLayers.ScopeUnshaded, out _))
            {
                sprite.LayerSetState(GunVisualLayers.ScopeUnshaded, $"{component.ScopeState}-unshaded");
                sprite.LayerSetVisible(GunVisualLayers.ScopeUnshaded, false);
            }
        }

        private void OnScopeVisualsChange(EntityUid uid, STScopeVisualsComponent component, ref AppearanceChangeEvent args)
        {
            var sprite = args.Sprite;

            if (sprite == null) return;

            if (args.AppearanceData.TryGetValue(AmmoVisuals.ScopeEquiped, out var scopeEquiped) &&
                scopeEquiped is true)
            {
                if (sprite.LayerMapTryGet(GunVisualLayers.Scope, out _))
                {
                    sprite.LayerSetVisible(GunVisualLayers.Scope, true);
                    sprite.LayerSetState(GunVisualLayers.Scope, $"{component.ScopeState}");
                }

                if (sprite.LayerMapTryGet(GunVisualLayers.ScopeUnshaded, out _))
                {
                    sprite.LayerSetVisible(GunVisualLayers.ScopeUnshaded, true);
                    sprite.LayerSetState(GunVisualLayers.ScopeUnshaded, $"{component.ScopeState}-unshaded");
                }
            }
            else
            {
                if (sprite.LayerMapTryGet(GunVisualLayers.Scope, out _))
                {
                    sprite.LayerSetVisible(GunVisualLayers.Scope, false);
                }

                if (sprite.LayerMapTryGet(GunVisualLayers.ScopeUnshaded, out _))
                {
                    sprite.LayerSetVisible(GunVisualLayers.ScopeUnshaded, false);
                }
            }
        }
   }
}
