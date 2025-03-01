namespace Content.Server._Stalker.AdvancedSpawner
{
    [DataDefinition]
    public sealed partial class SpawnEntry
    {
        [DataField("id")]
        public string PrototypeId { get; set; } = string.Empty;

        [DataField("weight")]
        public int Weight { get; set; } = 1;

        [DataField("count", required: false)]
        public int Count { get; set; } = 1;
    }
}
