namespace OPS
{
    public readonly struct PoolSnapshot
    {
        public readonly string Name;
        public readonly int Active;
        public readonly int Inactive;
        public readonly int SpawnCount;
        public readonly int DespawnCount;
        public readonly int PeakActive;
        public readonly int InitialCapacity;
        public readonly int MaxInactive;

        public PoolSnapshot(string name, int active, int inactive, int spawnCount, int despawnCount, int peakActive,
            int initialCapacity, int maxInactive)
        {
            Name = name;
            Active = active;
            Inactive = inactive;
            SpawnCount = spawnCount;
            DespawnCount = despawnCount;
            PeakActive = peakActive;
            InitialCapacity = initialCapacity;
            MaxInactive = maxInactive;
        }

        public override string ToString()
            => $"{Name}: {Active} active, {Inactive} inactive (peak {PeakActive}, max inactive {MaxInactive})";
    }
}
