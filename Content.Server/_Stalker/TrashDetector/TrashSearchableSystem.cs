namespace Content.Server._Stalker.TrashDetector;

public sealed class TrashSearchableSystem : EntitySystem
{
    private float _updateTimer;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;
        if (_updateTimer < 1f)
            return;

        _updateTimer = 0f;

        foreach (var comp in EntityQuery<TrashSearchableComponent>())
        {
            if (comp.TimeBeforeNextSearch > 0)
                comp.TimeBeforeNextSearch -= 1f;
        }
    }
}
