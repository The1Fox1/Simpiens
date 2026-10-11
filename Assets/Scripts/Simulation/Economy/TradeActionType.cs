namespace Simpiens.Simulation.Economy
{
    /// <summary>
    /// Type of transaction executed at an outpost trade stall.
    /// </summary>
    public enum TradeActionType : byte
    {
        None = 0,
        BuyItem = 1,
        SellItem = 2
    }
}
