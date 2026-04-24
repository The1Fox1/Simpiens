using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Evaluators
{
    public interface ICognitiveEvaluator
    {
        UniTask<AgentIntent> EvaluateAsync(AgentContext context, CancellationToken ct);
    }
}
