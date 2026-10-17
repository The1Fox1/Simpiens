using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Evaluators
{
    public interface ICognitiveEvaluator
    {
        ValueTask<AgentIntent> EvaluateAsync(AgentContext context, CancellationToken ct);
    }
}
