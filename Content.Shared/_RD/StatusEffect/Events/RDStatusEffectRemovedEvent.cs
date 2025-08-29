/*
 * Project: raincidation
 * File: RDStatusEffectRemovedEvent.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

namespace Content.Shared._RD.StatusEffect.Events;

/// <summary>
/// Calls on both effect entity and target entity, when a status effect is removed.
/// </summary>
[ByRefEvent]
public readonly record struct RDStatusEffectRemovedEvent(EntityUid Target, Entity<Components.RDStatusEffectComponent> Effect);
