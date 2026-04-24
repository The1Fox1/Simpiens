using System;
using System.Collections.Generic;
using Simpiens.Entities;

namespace Simpiens.Simulation
{
    /// <summary>
    /// Tracks all active entities in the simulation so the main loop can sync them to the spatial grid.
    /// </summary>
    public interface IWorldRegistry
    {
        IReadOnlyList<NodeController> ActiveNodes { get; }
        
        void RegisterNode(NodeController node);
        void UnregisterNode(NodeController node);
        NodeController GetNode(UnityEngine.GUID id);
    }
}
