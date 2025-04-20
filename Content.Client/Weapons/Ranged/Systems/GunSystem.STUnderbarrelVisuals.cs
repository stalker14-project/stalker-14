using Content.Client.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;

namespace Content.Client.Weapons.Ranged.Systems
{
    public partial class GunSystem
    {
        private void InitializeSTUnderbarrelVisuals()
        {
            SubscribeLocalEvent<STUnderbarrelVisualsComponent, ComponentInit>(OnUnderbarrelVisualsInit);
            SubscribeLocalEvent<STUnderbarrelVisualsComponent, AppearanceChangeEvent>(OnUnderbarrelVisualsChange);
        }


        private void OnUnderbarrelVisualsInit(EntityUid uid, STUnderbarrelVisualsComponent component, ComponentInit args)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite)) return;

            if (sprite.LayerMapTryGet(GunVisualLayers.Underbarrel, out _))
            {
                sprite.LayerSetState(GunVisualLayers.Underbarrel, $"{component.UnderbarrelState}");
                sprite.LayerSetVisible(GunVisualLayers.Underbarrel, false);
            }

            if (sprite.LayerMapTryGet(GunVisualLayers.UnderbarrelUnshaded, out _))
            {
                sprite.LayerSetState(GunVisualLayers.UnderbarrelUnshaded, $"{component.UnderbarrelState}-unshaded");
                sprite.LayerSetVisible(GunVisualLayers.UnderbarrelUnshaded, false);
            }
        }

        private void OnUnderbarrelVisualsChange(EntityUid uid, STUnderbarrelVisualsComponent component, ref AppearanceChangeEvent args)
        {
            var sprite = args.Sprite;

            if (sprite == null) return;

            if (args.AppearanceData.TryGetValue(AmmoVisuals.UnderbarrelEquiped, out var underbarrelEquiped) &&
                underbarrelEquiped is true)
            {
                if (sprite.LayerMapTryGet(GunVisualLayers.Underbarrel, out _))
                {
                    sprite.LayerSetVisible(GunVisualLayers.Underbarrel, true);
                    sprite.LayerSetState(GunVisualLayers.Underbarrel, $"{component.UnderbarrelState}");
                }

                if (sprite.LayerMapTryGet(GunVisualLayers.UnderbarrelUnshaded, out _))
                {
                    sprite.LayerSetVisible(GunVisualLayers.UnderbarrelUnshaded, true);
                    sprite.LayerSetState(GunVisualLayers.UnderbarrelUnshaded, $"{component.UnderbarrelState}-unshaded");
                }
            }
            else
            {
                if (sprite.LayerMapTryGet(GunVisualLayers.Underbarrel, out _))
                {
                    sprite.LayerSetVisible(GunVisualLayers.Underbarrel, false);
                }

                if (sprite.LayerMapTryGet(GunVisualLayers.UnderbarrelUnshaded, out _))
                {
                    sprite.LayerSetVisible(GunVisualLayers.UnderbarrelUnshaded, false);
                }
            }
        }
   }
}
