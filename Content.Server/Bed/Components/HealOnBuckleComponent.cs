using Content.Shared.Damage;

namespace Content.Server.Bed.Components
{
    [RegisterComponent]
    public sealed partial class HealOnBuckleComponent : Component
    {
        /// <summary>
        /// Damage to apply to entities that are strapped to this entity.
        /// </summary>
        [DataField(required: true)]
        public DamageSpecifier Damage = default!;

        /// <summary>
        /// How frequently the damage should be applied, in seconds.
        /// </summary>
        [DataField(required: false)]
        public float HealTime = 1f;

        /// <summary>
        /// Damage multiplier that gets applied if the entity is sleeping.
        /// </summary>
        [DataField]
        public float SleepMultiplier = 3f;

        public TimeSpan NextHealTime = TimeSpan.Zero; //Next heal

        [DataField] public EntityUid? SleepAction;

        /// <summary>
        /// How much blood to replenish per HealTime.
        /// </summary>
        [DataField]
        public float ReplenishBloodAmount = 1f;

        /// <summary>
        /// How much bleeding to remove per HealTime.
        /// </summary>
        [DataField]
        public float BleedingAmount = -0.1f;
    }
}
