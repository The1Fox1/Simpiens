namespace Simpiens.Simulation.Combat
{
    /// <summary>
    /// A shot loosed in the creatures' pass (Epic 10 Part 3, K8.4a): who shot whom, from and to where, whether it landed, and
    /// whether a creature loosed it (a spinner's spit) or a crawler (a stone, a bolt). Presentation's feed, never saved.
    /// </summary>
    public readonly struct ShotRecord
    {
        public readonly EntityId Shooter;
        public readonly EntityId Target;
        public readonly Vec2 From;
        public readonly Vec2 To;
        public readonly bool Hit;
        public readonly bool ByCreature;

        public ShotRecord(EntityId shooter, EntityId target, Vec2 from, Vec2 to, bool hit, bool creature)
        {
            Shooter = shooter;
            Target = target;
            From = from;
            To = to;
            Hit = hit;
            ByCreature = creature;
        }

        public override string ToString() => $"{(ByCreature ? "spit" : "shot")} {Shooter} -> {Target} from {From} to {To}: {(Hit ? "hit" : "miss")}";
    }
}
