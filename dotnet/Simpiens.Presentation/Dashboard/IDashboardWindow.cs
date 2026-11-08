namespace Simpiens.Presentation.Dashboard
{
    /// <summary>A window over the map (the stall, the bounty board, the guild, the stations): one is open at a time.</summary>
    public interface IDashboardWindow
    {
        bool IsOpen { get; }
        void Close();
    }
}
