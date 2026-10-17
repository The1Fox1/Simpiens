using System;
using Simpiens.Entities;

namespace Simpiens.Testing.UI
{
    /// <summary>
    /// The yielding look has its two sprite frames (Epic Beta). A view test, so it stays in the Unity harness; it used
    /// to be part of ResourceContentionValidator, which became engine-free (Epic CORE C7).
    /// </summary>
    public class AgentSpriteValidator
    {
        public void RunAllValidationTests()
        {
            var sprite0 = AgentSpriteLibrary.GetAgentSprite(AgentVisualState.Yielding, 0);
            var sprite1 = AgentSpriteLibrary.GetAgentSprite(AgentVisualState.Yielding, 1);

            Assert(sprite0 != null, "AgentSpriteLibrary returned non-null sprite for AgentVisualState.Yielding frame 0.");
            Assert(sprite1 != null, "AgentSpriteLibrary returned non-null sprite for AgentVisualState.Yielding frame 1.");
            Assert(sprite0.name == "Agent_Yield_0", $"Sprite 0 name is Agent_Yield_0 (actual: {sprite0.name})");
            Assert(sprite1.name == "Agent_Yield_1", $"Sprite 1 name is Agent_Yield_1 (actual: {sprite1.name})");
            TestLog.Log("[PASS] AgentSpriteValidator: the yielding look has its two frames.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[AgentSpriteValidator ASSERTION FAILED] {message}");
        }
    }
}
