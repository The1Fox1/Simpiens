namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// What a goal is for (Epic LIFE-1, L0.2, D1): the motive model (L0.5) weighs every goal of a motive alike, so mood, wants
    /// and the day's rhythm reach every goal through one rule. Values are never reused. Plan 2 adds fun and a want; LIFE-3
    /// an aspiration.
    /// </summary>
    public enum Motive : byte
    {
        /// <summary>Undeclared: only test goals; every production goal declares one (MotiveValidator).</summary>
        None = 0,
        Hunger = 1,
        Rest = 2,
        Company = 3,
        Income = 4,
        Gear = 5,
        /// <summary>Learning a craft: a manual, the drill yard (Epic 11's teachers).</summary>
        Growth = 6,
        /// <summary>Orders: a group's directive.</summary>
        Duty = 7,
        /// <summary>Helping another: aid, rescue.</summary>
        Aid = 8,
        /// <summary>A drifter leaving the outpost.</summary>
        Leave = 9,
        /// <summary>Fun: what boredom asks for (Epic LIFE-1 plan 2, L2.1, D11); Recreate's, from L2.6.</summary>
        Fun = 10,
    }

    /// <summary>
    /// Which part of the day a goal belongs to (Epic LIFE-1, L0.2, D1): the routine (L3) and the day's intention weigh goals by
    /// family and hour. An unweighted goal (hunger, orders, aid, leaving) is never scaled by them, so survival and orders keep
    /// their precedence (the epic's A8). Values are never reused.
    /// </summary>
    public enum ActivityFamily : byte
    {
        Unweighted = 0,
        Work = 1,
        /// <summary>Free hours: recreation (L2), training (Epic 11's teachers).</summary>
        Leisure = 2,
        Social = 3,
        Rest = 4,
    }
}
