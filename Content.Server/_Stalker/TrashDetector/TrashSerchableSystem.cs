namespace Content.Server.TrashSearchable;

public sealed class TrashSearchableSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var comp in EntityQuery<TrashSearchableComponent>())
        {
            comp.TimeBeforeNextSearch -= frameTime;
        }
    }
}
