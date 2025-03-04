using Content.Server._Stalker.AdvancedSpawner; // Connecting SpawnEntry
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using System;
using System.Collections.Generic;

namespace Content.Server.TrashDetector.Components;

[RegisterComponent]
public sealed partial class TrashDetectorComponent : Component
{
    /// <summary>
    /// Search time (how many seconds DoAfter lasts).
    /// </summary>
    [DataField]
    public float SearchTime { get; set; } = 5f;

    /// <summary>
    /// The base spawner that will be used.
    /// </summary>
    public const string LootSpawner = "RandomTrashDetectorSpawner";

    /// <summary>
    /// Category weight modifiers (probability of category drop).
    /// </summary>
    [DataField]
    public int CommonWeightMod { get; set; } = 5;

    [DataField]
    public int RareWeightMod { get; set; } = 3;

    [DataField]
    public int LegendaryWeightMod { get; set; } = 1;

    [DataField]
    public int NegativeWeightMod { get; set; } = -2;

    /// <summary>
    /// Additional items added to categories with their weights and quantity.
    /// </summary>
    [DataField]
    public List<SpawnEntry> ExtraCommonPrototypes { get; set; } = new();

    [DataField]
    public List<SpawnEntry> ExtraRarePrototypes { get; set; } = new();

    [DataField]
    public List<SpawnEntry> ExtraLegendaryPrototypes { get; set; } = new();

    [DataField]
    public List<SpawnEntry> ExtraNegativePrototypes { get; set; } = new();
}
