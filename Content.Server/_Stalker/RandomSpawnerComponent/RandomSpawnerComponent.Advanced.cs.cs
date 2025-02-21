using Robust.Shared.Prototypes;

namespace Content.Server.AdvancedSpawners.Components;

/// <summary>
/// Улучшенный рандомный спавнер с гибкими настройками.
/// </summary>
[RegisterComponent]
public sealed partial class AdvancedRandomSpawnerComponent : Component
{
    /// <summary>
    /// Список возможных объектов для спавна.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("prototypes")]
    public List<string> Prototypes { get; set; } = new();

    /// <summary>
    /// Редкие объекты, которые могут появиться с шансом RareChance.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("rarePrototypes")]
    public List<string> RarePrototypes { get; set; } = new();

    /// <summary>
    /// Шанс появления редкого объекта.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("rareChance")]
    public float RareChance { get; set; } = 0.05f;

    /// <summary>
    /// Минимальное количество спавнимых сущностей.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("minCount")]
    public int MinSpawnCount { get; set; } = 1;

    /// <summary>
    /// Максимальное количество спавнимых сущностей.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("maxCount")]
    public int MaxSpawnCount { get; set; } = 1;

    /// <summary>
    /// Шанс, что спавн НЕ произойдет.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("extraChance")]
    public float ExtraChance { get; set; } = 0.0f;

    /// <summary>
    /// Разброс координат при спавне.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("offset")]
    public float Offset { get; set; } = 0.2f;

    /// <summary>
    /// Удалять ли спавнер после спавна.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("deleteAfterSpawn")]
    public bool DeleteAfterSpawn { get; set; } = true;
}
