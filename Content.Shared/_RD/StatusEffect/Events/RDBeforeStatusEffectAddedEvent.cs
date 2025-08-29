/*
 * Project: raincidation
 * File: RDBeforeStatusEffectAddedEvent.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Robust.Shared.Prototypes;

namespace Content.Shared._RD.StatusEffect.Events;

/// <summary>
/// Raised on an entity before a status effect is added to determine if adding it should be cancelled.
/// </summary>
[ByRefEvent]
public record struct RDBeforeStatusEffectAddedEvent(EntProtoId Effect, bool Cancelled = false);
