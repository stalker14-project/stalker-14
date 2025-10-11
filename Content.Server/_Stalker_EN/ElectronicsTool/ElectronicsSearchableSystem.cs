using Content.Server._Stalker_EN.ElectronicsSearchable;

namespace Content.Server._Stalker_EN.ElectronicsTool.ElectronicsSearchable;

public sealed class ElectronicsSearchableSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<ElectronicsSearchableComponent>();
        while (query.MoveNext(out var uid, out var resist))
        {
            resist.TimeBeforeNextSearch -= frameTime;
        }
    }
}
