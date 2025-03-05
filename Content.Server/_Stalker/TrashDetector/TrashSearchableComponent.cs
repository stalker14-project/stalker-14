using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.TrashSearchable;

[RegisterComponent]
public sealed partial class TrashSearchableComponent : Component
{
    [DataField]
    public float CooldownAfterSearch = 600f;

    [DataField]
    public float TimeBeforeNextSearch = 0f;

    public void SetTimeBeforeNextSearch(float time)
    {
        TimeBeforeNextSearch = time;
        Dirty();
    }
}
