using System;
using System.Collections.Generic;
using UnityEngine;

namespace Simpiens.Narrative
{
    /// <summary>
    /// Deterministic agent persona mapper.
    /// Maps GUID hashes to friendly character names and titles with zero allocations after initial cache warmup.
    /// </summary>
    public class AgentIdentityService : IAgentIdentityService
    {
        private static readonly string[] DefaultNames = new[]
        {
            "Burt", "Bob", "Alice", "Charlie", "Danielle", "Ezekiel", "Fiona", "Grover",
            "Helen", "Ivan", "Juno", "Kaelen", "Liam", "Maya", "Nora", "Orson",
            "Penny", "Quinn", "Rowan", "Soren", "Tessa", "Uri", "Vera", "Wren",
            "Xander", "Yara", "Zane", "Arthur", "Beatrice", "Cyrus", "Dorothy", "Edgar",
            "Freya", "Gideon", "Hazel", "Iris", "Jasper", "Kira", "Leon", "Maeve",
            "Niles", "Opal", "Peter", "Rosa", "Silas", "Tara", "Victor", "Wendy"
        };

        private static readonly string[] DefaultTitles = new[]
        {
            "Settler", "Forager", "Scout", "Elder", "Herbalist", "Crafter",
            "Guardian", "Speaker", "Pioneer", "Laborer", "Watchman", "Pathfinder"
        };

        private readonly Dictionary<GUID, string> _nameCache = new Dictionary<GUID, string>(64);
        private readonly Dictionary<GUID, string> _titleCache = new Dictionary<GUID, string>(64);
        private readonly Dictionary<GUID, string> _customNames = new Dictionary<GUID, string>(16);
        private readonly Dictionary<GUID, string> _customTitles = new Dictionary<GUID, string>(16);

        public string GetFriendlyName(GUID agentId)
        {
            if (agentId == default) return "Unknown";

            if (_customNames.TryGetValue(agentId, out var customName))
            {
                return customName;
            }

            if (_nameCache.TryGetValue(agentId, out var cachedName))
            {
                return cachedName;
            }

            uint hash = ComputeGuidHash(agentId);
            int index = (int)(hash % (uint)DefaultNames.Length);
            string name = DefaultNames[index];
            _nameCache[agentId] = name;
            return name;
        }

        public string GetFriendlyTitle(GUID agentId)
        {
            if (agentId == default) return "Transient";

            if (_customTitles.TryGetValue(agentId, out var customTitle))
            {
                return customTitle;
            }

            if (_titleCache.TryGetValue(agentId, out var cachedTitle))
            {
                return cachedTitle;
            }

            uint hash = ComputeGuidHash(agentId);
            int index = (int)((hash / (uint)DefaultNames.Length) % (uint)DefaultTitles.Length);
            string title = DefaultTitles[index];
            _titleCache[agentId] = title;
            return title;
        }

        public string GetFormattedDisplayName(GUID agentId, string fallbackName = null)
        {
            string friendly = GetFriendlyName(agentId);
            if (string.IsNullOrEmpty(fallbackName))
            {
                return friendly;
            }
            return $"{friendly} [{fallbackName}]";
        }

        public void RegisterCustomName(GUID agentId, string name)
        {
            if (agentId == default || string.IsNullOrWhiteSpace(name)) return;
            _customNames[agentId] = name;
            _nameCache[agentId] = name;
        }

        public void RegisterCustomTitle(GUID agentId, string title)
        {
            if (agentId == default || string.IsNullOrWhiteSpace(title)) return;
            _customTitles[agentId] = title;
            _titleCache[agentId] = title;
        }

        private static uint ComputeGuidHash(GUID agentId)
        {
            string str = agentId.ToString();
            // 32-bit FNV-1a hash
            uint hash = 2166136261;
            for (int i = 0; i < str.Length; i++)
            {
                hash = (hash ^ str[i]) * 16777619;
            }
            return hash;
        }
    }
}
