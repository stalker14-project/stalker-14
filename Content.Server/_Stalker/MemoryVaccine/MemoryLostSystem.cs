using Content.Server.Popups;
using Content.Shared.Popups;

namespace Content.Server._Stalker.MemoryLost;
public sealed class MemoryLostSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<MemoryLostComponent>();
        while (query.MoveNext(out var uid, out var resist))
        {
            resist.CoolDownTime -= frameTime;
            if (resist.CoolDownTime < 0)
            {
                if (resist.PopupCount > 0)
                {
                    _popupSystem.PopupEntity("Пам'ять за останні 10 хвилин втрачається", uid, PopupType.LargeCaution);
                    resist.PopupCount -= 1;
                    resist.CoolDownTime = 2f;
                }
                else
                    RemComp<MemoryLostComponent>(uid);
            }
        }
    }
}
