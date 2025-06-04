/*
 * Project: raincidation
 * File: RDStatusEffectContainerComponent.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Content.Shared._RD.StatusEffect.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._RD.StatusEffect.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(RDStatusEffectSystem))]
public sealed partial class RDStatusEffectContainerComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> ActiveStatusEffects = new();
}
