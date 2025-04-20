using Content.Client.Weapons.Ranged.Components;
using Content.Shared.Rounding;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Client.Weapons.Ranged.Systems
{
    public partial class GunSystem
    {
        private void InitializeSTMuzzleVisuals()
        {
            SubscribeLocalEvent<STMuzzleVisualsComponent, ComponentInit>(OnMuzzleVisualsInit);
            SubscribeLocalEvent<STMuzzleVisualsComponent, AppearanceChangeEvent>(OnMuzzleVisualsChange);
        }


        private void OnMuzzleVisualsInit(EntityUid uid, STMuzzleVisualsComponent component, ComponentInit args)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite)) return;

            if (sprite.LayerMapTryGet(GunVisualLayers.Muzzle, out _))
            {
                sprite.LayerSetState(GunVisualLayers.Muzzle, $"{component.MuzzleState}");
                sprite.LayerSetVisible(GunVisualLayers.Muzzle, false);
            }

            if (sprite.LayerMapTryGet(GunVisualLayers.MuzzleUnshaded, out _))
            {
                sprite.LayerSetState(GunVisualLayers.MuzzleUnshaded, $"{component.MuzzleState}-unshaded");
                sprite.LayerSetVisible(GunVisualLayers.MuzzleUnshaded, false);
            }
        }

        private void OnMuzzleVisualsChange(EntityUid uid, STMuzzleVisualsComponent component, ref AppearanceChangeEvent args)
        {
            var sprite = args.Sprite;

            if (sprite == null) return;

            if (args.AppearanceData.TryGetValue(AmmoVisuals.MuzzleEquiped, out var muzzleEquipped) &&
                muzzleEquipped is true)
            {
                if (sprite.LayerMapTryGet(GunVisualLayers.Muzzle, out _))
                {
                    sprite.LayerSetVisible(GunVisualLayers.Muzzle, true);
                    sprite.LayerSetState(GunVisualLayers.Muzzle, $"{component.MuzzleState}");
                }

                if (sprite.LayerMapTryGet(GunVisualLayers.MuzzleUnshaded, out _))
                {
                    sprite.LayerSetVisible(GunVisualLayers.MuzzleUnshaded, true);
                    sprite.LayerSetState(GunVisualLayers.MuzzleUnshaded, $"{component.MuzzleState}-unshaded");
                }
            }
            else
            {
                if (sprite.LayerMapTryGet(GunVisualLayers.Muzzle, out _))
                {
                    sprite.LayerSetVisible(GunVisualLayers.Muzzle, false);
                }

                if (sprite.LayerMapTryGet(GunVisualLayers.MuzzleUnshaded, out _))
                {
                    sprite.LayerSetVisible(GunVisualLayers.MuzzleUnshaded, false);
                }
            }
        }
   }
}
