namespace Simpiens.Intervention
{
    public interface IPlayerInterventionService
    {
        void QueueInfluence(InfluenceEvent influence);
        bool TryDequeueInfluence(out InfluenceEvent influence);
    }
}
