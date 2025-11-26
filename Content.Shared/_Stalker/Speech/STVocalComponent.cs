using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Speech;

/// <summary>
/// Component required for entities to be able to do vocal emotions.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STVocalComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<Sex, ProtoId<EmoteSoundsPrototype>> Sounds = new()
    {
        { Sex.Male, "STMaleHuman" },
        { Sex.Female, "STFemaleHuman" },
        { Sex.Unsexed, "STMaleHuman" },
    };

    [ViewVariables, AutoNetworkedField]
    public EmoteSoundsPrototype? EmoteSounds;
}
