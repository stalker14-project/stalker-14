namespace Content.Shared._Stalker.Stun;

public sealed class STSizeStunSystem : EntitySystem
{
    public bool IsHumanoidSized(Entity<STSizeComponent> ent)
    {
        return ent.Comp.Size <= STSizes.Humanoid;
    }

    public bool IsXenoSized(Entity<STSizeComponent> ent)
    {
        return ent.Comp.Size >= STSizes.VerySmallXeno;
    }

    public bool TryGetSize(EntityUid ent, out STSizes size)
    {
        size = default;
        if (!TryComp(ent, out STSizeComponent? sizeComp))
            return false;

        size = sizeComp.Size;
        return true;
    }
}
