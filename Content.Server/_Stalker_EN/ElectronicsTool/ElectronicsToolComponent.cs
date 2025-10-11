namespace Content.Server.ElectronicsTool.Components
{
    [RegisterComponent]
    public sealed partial class ElectronicsToolComponent : Component
    {
        [DataField]
        public float SearchTime = 5;

        [DataField]
        public float Probability = 0.5f;

        [DataField]
        public string Loot = "RandomElectronicsToolSpawner";
    }
}
