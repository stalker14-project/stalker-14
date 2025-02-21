using Robust.Shared.Prototypes;

namespace Content.Server.AdvancedSpawners.Components;

/// <summary>
/// Улучшенный спавнер, который активируется при определённых игровых условиях.
/// </summary>
[RegisterComponent]
public sealed partial class AdvancedConditionalSpawnerComponent : Component
{
    /// <summary>
    /// Список возможных прототипов, которые могут заспавниться.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("prototypes")]
    public List<string> Prototypes { get; set; } = new();

    /// <summary>
    /// Список игровых правил, при которых сработает спавн.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("gameRules")]
    public List<string> GameRules { get; set; } = new();

    /// <summary>
    /// Шанс спавна (значение от 0.0 до 1.0).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("chance")]
    public float Chance { get; set; } = 1.0f;
}

