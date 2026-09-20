using System;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using UnityEngine;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// High-performance A* search engine for Goal-Oriented Action Planning.
    /// Operates with zero heap allocations during planning by utilizing pre-allocated
    /// node pools, an array-backed binary min-heap, and open-addressing visited state tables.
    /// </summary>
    public class GoapPlanner : IGoapPlanner
    {
        private struct GoapPlanNode
        {
            public WorldState State;
            public GoapAction Action;
            public float ActionCost;
            public int ParentIndex;
            public float GCost;
            public float HCost;
            public float FCost => GCost + HCost;
        }

        private struct ClosedEntry
        {
            public WorldState State;
            public float GCost;
            public int NodeIndex;
            public bool IsOccupied;
        }

        private readonly object _searchLock = new object();

        // Node pool buffer
        private readonly GoapPlanNode[] _nodePool;
        private int _nodeCount;
        private readonly int _maxNodes;

        // Binary min-heap open list (stores node indices)
        private readonly int[] _openHeap;
        private int _heapCount;

        // Closed set: flat hash table with linear probing
        private readonly ClosedEntry[] _closedTable;
        private readonly int[] _occupiedSlots;
        private int _occupiedCount;
        private readonly int _closedMask;

        // Scratch buffers for zero-allocation expansion
        private readonly List<GoapAction> _applicableActionsBuffer;
        private readonly int[] _pathScratch;

        public GoapPlanner(int maxNodes = 256)
        {
            _maxNodes = maxNodes;
            _nodePool = new GoapPlanNode[maxNodes];
            _openHeap = new int[maxNodes];

            // Closed table size must be power of two, sized larger than maxNodes to keep load factor low (< 0.5)
            int tableSize = 512;
            while (tableSize < maxNodes * 2)
            {
                tableSize <<= 1;
            }
            _closedTable = new ClosedEntry[tableSize];
            _occupiedSlots = new int[tableSize];
            _closedMask = tableSize - 1;

            _applicableActionsBuffer = new List<GoapAction>(32);
            _pathScratch = new int[maxNodes];
        }

        /// <inheritdoc />
        public bool Plan(
            in WorldState currentState,
            GoapGoal goal,
            ActionGraph actionGraph,
            AgentContext context,
            GoapPlan outPlan)
        {
            if (goal == null) throw new ArgumentNullException(nameof(goal));
            if (actionGraph == null) throw new ArgumentNullException(nameof(actionGraph));
            if (outPlan == null) throw new ArgumentNullException(nameof(outPlan));

            lock (_searchLock)
            {
                outPlan.Clear();

                // 1. Check if the goal is already satisfied
                if (goal.IsSatisfied(currentState))
                {
                    return true;
                }

                // 2. Reset search buffers
                ResetSearch();

                // 3. Create root node
                int rootIndex = AllocateNode(
                    currentState,
                    null,
                    0f,
                    -1,
                    0f,
                    CalculateHeuristic(currentState, goal.DesiredState)
                );

                HeapPush(rootIndex);
                InsertClosed(currentState, 0f, rootIndex);

                int bestGoalNodeIndex = -1;

                // 4. A* Search Loop
                while (_heapCount > 0)
                {
                    int currentIndex = HeapPop();
                    var currentNode = _nodePool[currentIndex];

                    // Check if current node satisfies goal
                    if (goal.IsSatisfied(currentNode.State))
                    {
                        bestGoalNodeIndex = currentIndex;
                        break;
                    }

                    // Query applicable actions from this state
                    _applicableActionsBuffer.Clear();
                    actionGraph.GetApplicableActions(currentNode.State, _applicableActionsBuffer);

                    int actionCount = _applicableActionsBuffer.Count;
                    for (int i = 0; i < actionCount; i++)
                    {
                        var action = _applicableActionsBuffer[i];

                        // Procedural validation check (e.g. line-of-sight, memory existence)
                        if (!action.IsValid(context))
                        {
                            continue;
                        }

                        // Calculate dynamic cost (must be positive)
                        float stepCost = action.GetCost(context);
                        if (stepCost <= 0f) stepCost = action.BaseCost > 0f ? action.BaseCost : 0.01f;

                        // Apply action effects to produce successor state
                        WorldState successorState = currentNode.State.ApplyEffects(action.Effects);

                        // Prevent no-op loops (where applying effects results in identical state)
                        if (successorState == currentNode.State)
                        {
                            continue;
                        }

                        float tentativeG = currentNode.GCost + stepCost;

                        // Check visited table
                        if (TryGetClosed(successorState, out float existingG, out int _))
                        {
                            if (tentativeG >= existingG)
                            {
                                // A path to this state already exists with equal or lower cost
                                continue;
                            }
                        }

                        // Check node pool capacity
                        if (_nodeCount >= _maxNodes)
                        {
                            // Node pool exhausted; terminate search to maintain frame budget
                            break;
                        }

                        float h = CalculateHeuristic(successorState, goal.DesiredState);
                        int successorIndex = AllocateNode(
                            successorState,
                            action,
                            stepCost,
                            currentIndex,
                            tentativeG,
                            h
                        );

                        InsertClosed(successorState, tentativeG, successorIndex);
                        HeapPush(successorIndex);
                    }
                }

                // 5. Reconstruct plan if goal was reached
                if (bestGoalNodeIndex != -1)
                {
                    ReconstructPlan(bestGoalNodeIndex, outPlan);
                    return true;
                }

                return false;
            }
        }

        private void ResetSearch()
        {
            _nodeCount = 0;
            _heapCount = 0;

            // Fast clear of occupied hash slots
            for (int i = 0; i < _occupiedCount; i++)
            {
                _closedTable[_occupiedSlots[i]] = default;
            }
            _occupiedCount = 0;
        }

        private int AllocateNode(WorldState state, GoapAction action, float actionCost, int parentIndex, float gCost, float hCost)
        {
            int index = _nodeCount++;
            _nodePool[index] = new GoapPlanNode
            {
                State = state,
                Action = action,
                ActionCost = actionCost,
                ParentIndex = parentIndex,
                GCost = gCost,
                HCost = hCost
            };
            return index;
        }

        private float CalculateHeuristic(in WorldState state, in WorldState desiredState)
        {
            // Admissible heuristic: count of unsatisfied facts * minimum standard cost (1.0f)
            return state.CountUnsatisfiedFacts(desiredState);
        }

        private void ReconstructPlan(int goalNodeIndex, GoapPlan outPlan)
        {
            int pathLength = 0;
            int curr = goalNodeIndex;

            // Trace backward to root
            while (curr != -1 && _nodePool[curr].Action != null)
            {
                _pathScratch[pathLength++] = curr;
                curr = _nodePool[curr].ParentIndex;
            }

            // Populate outPlan in forward chronological order
            for (int i = pathLength - 1; i >= 0; i--)
            {
                int nodeIdx = _pathScratch[i];
                outPlan.AddAction(_nodePool[nodeIdx].Action, _nodePool[nodeIdx].ActionCost);
            }
        }

        #region Binary Min-Heap Open List

        private void HeapPush(int nodeIndex)
        {
            int i = _heapCount++;
            _openHeap[i] = nodeIndex;

            // Bubble up
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_nodePool[_openHeap[i]].FCost < _nodePool[_openHeap[parent]].FCost)
                {
                    int temp = _openHeap[i];
                    _openHeap[i] = _openHeap[parent];
                    _openHeap[parent] = temp;
                    i = parent;
                }
                else
                {
                    break;
                }
            }
        }

        private int HeapPop()
        {
            int result = _openHeap[0];
            _heapCount--;

            if (_heapCount > 0)
            {
                _openHeap[0] = _openHeap[_heapCount];
                int i = 0;

                // Sift down
                while (true)
                {
                    int left = (i << 1) + 1;
                    if (left >= _heapCount) break;

                    int right = left + 1;
                    int smallest = left;

                    if (right < _heapCount && _nodePool[_openHeap[right]].FCost < _nodePool[_openHeap[left]].FCost)
                    {
                        smallest = right;
                    }

                    if (_nodePool[_openHeap[smallest]].FCost < _nodePool[_openHeap[i]].FCost)
                    {
                        int temp = _openHeap[i];
                        _openHeap[i] = _openHeap[smallest];
                        _openHeap[smallest] = temp;
                        i = smallest;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return result;
        }

        #endregion

        #region Closed Set Hash Table

        private bool TryGetClosed(in WorldState state, out float gCost, out int nodeIndex)
        {
            int hash = state.GetHashCode();
            int slot = hash & _closedMask;

            for (int i = 0; i < _closedTable.Length; i++)
            {
                ref var entry = ref _closedTable[slot];
                if (!entry.IsOccupied)
                {
                    gCost = 0f;
                    nodeIndex = -1;
                    return false;
                }

                if (entry.State == state)
                {
                    gCost = entry.GCost;
                    nodeIndex = entry.NodeIndex;
                    return true;
                }

                slot = (slot + 1) & _closedMask;
            }

            gCost = 0f;
            nodeIndex = -1;
            return false;
        }

        private void InsertClosed(in WorldState state, float gCost, int nodeIndex)
        {
            int hash = state.GetHashCode();
            int slot = hash & _closedMask;

            for (int i = 0; i < _closedTable.Length; i++)
            {
                ref var entry = ref _closedTable[slot];
                if (!entry.IsOccupied)
                {
                    entry.State = state;
                    entry.GCost = gCost;
                    entry.NodeIndex = nodeIndex;
                    entry.IsOccupied = true;
                    _occupiedSlots[_occupiedCount++] = slot;
                    return;
                }

                if (entry.State == state)
                {
                    entry.GCost = gCost;
                    entry.NodeIndex = nodeIndex;
                    return;
                }

                slot = (slot + 1) & _closedMask;
            }
        }

        #endregion
    }
}
