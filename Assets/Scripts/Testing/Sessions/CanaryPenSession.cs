using Simpiens.Core.Session;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using UnityEngine;

namespace Simpiens.Testing.Sessions
{
    /// <summary>
    /// The Dev Map session (test-only since Epic MAP Phase 2): the 10 scripted demonstration pens, each seeded with
    /// the relationships, hunger, memories and groups its demonstration needs. No stall, bounties or crawlers.
    /// Installed by <see cref="TestHarnessInstaller"/>, reachable through the session switch and asserted by
    /// PenCanaryScenario. Never part of a player build.
    /// </summary>
    public sealed class CanaryPenSession : ISessionDefinition
    {
        public const string SessionId = "devmap";

        public const int ScriptedPenAgentCount = 24;

        // Predetermined positions for the 10 demonstration scenarios within their respective play pens
        private static readonly Vector2[] PenAgentPositions =
        {
            new Vector2(-8f, 1.8f), // 0: Pen 1: Dominant
            new Vector2(-8f, -1.0f), // 1: Pen 1: Submissive
            new Vector2(-4f, 1.8f), // 2: Pen 2: Respected
            new Vector2(-4f, -1.0f), // 3: Pen 2: Deferent
            new Vector2(0f, 1.5f), // 4: Pen 3: Rival A
            new Vector2(0f, -1.5f), // 5: Pen 3: Rival B
            new Vector2(4f, 1.8f), // 6: Pen 4: Altruistic Donor
            new Vector2(4f, -1.8f), // 7: Pen 4: Starving Companion
            new Vector2(8f, 1.8f), // 8: Pen 5: Selfish Peer
            new Vector2(8f, -1.8f), // 9: Pen 5: Starving Stranger
            new Vector2(-8f, -7.5f), // 10: Pen 6: Group 1 Leader
            new Vector2(-8f, -10.5f), // 11: Pen 6: Compliant Subordinate
            new Vector2(-4f, -7.2f), // 12: Pen 7: Group 2 Leader
            new Vector2(-4f, -9.0f), // 13: Pen 7: Starving Defiant Subordinate
            new Vector2(0f, -8.0f), // 14: Pen 8: Respected Candidate
            new Vector2(-0.8f, -9.8f), // 15: Pen 8: Elector Peer A
            new Vector2(0.8f, -9.8f), // 16: Pen 8: Elector Peer B
            new Vector2(4f, -8.0f), // 17: Pen 9: Collapsed Leader
            new Vector2(4f, -9.5f), // 18: Pen 9: Dominance Challenger
            new Vector2(4.8f, -9.0f), // 19: Pen 9: Group 3 Member
            new Vector2(7.2f, -8.0f), // 20: Pen 10: Discredited Leader
            new Vector2(7.2f, -9.5f), // 21: Pen 10: Loyalist
            new Vector2(8.8f, -8.0f), // 22: Pen 10: Dissident Contender
            new Vector2(8.8f, -9.5f), // 23: Pen 10: Dissident Follower
        };

        public string Id => SessionId;
        public string DisplayName => "Dev Map";
        public MapDefinition Map => DevMapLayout.Definition;
        public Simpiens.Simulation.Economy.EconomyTuning Economy => Simpiens.Simulation.Economy.EconomyTuning.Default;

        public void Populate(SessionBuilder session)
        {
            var world = session.World;
            var bushFearId = world.GetEntityId(DevMapLayout.Pen1ContestedBush);
            var bushRespectId = world.GetEntityId(DevMapLayout.Pen2ContestedBush);
            var bushRivalryId = world.GetEntityId(DevMapLayout.Pen3ContestedBush);
            var campfirePen4Id = world.GetEntityId(DevMapLayout.Pen4Campfire);
            var campfirePen5Id = world.GetEntityId(DevMapLayout.Pen5Campfire);
            var bushPen7Id = world.GetEntityId(DevMapLayout.Pen7Bush);
            var campfirePen8Id = world.GetEntityId(DevMapLayout.Pen8Campfire);
            var bushPen9Id = world.GetEntityId(DevMapLayout.Pen9Bush);
            var bushPen10Id = world.GetEntityId(DevMapLayout.Pen10Bush);

            for (int i = 0; i < ScriptedPenAgentCount; i++)
            {
                // Scripted pen agents stay neutral so the Epic 7 / Beta / 8 demonstrations keep the behaviour they
                // were tuned with (producer decision 2026-10-09)
                session.SpawnAgent(PenAgentPositions[i], AgentLabel(i), seededPersonality: false);
            }
            var a = session.Agents;

            // Seed relational dyads for visual demonstration
            if (a.Count >= 6)
            {
                // Dyad 1 (Fear Asymmetry): Agent 1 fears Agent 0 (Fear = 75). Agent 1 will yield Bush 0 to Agent 0.
                a[0].Hunger = 60f;
                a[1].Hunger = 60f;
                a[1].Memory.RecordInteraction(a[0].AgentId, deltaWarmth: 0, deltaTrust: 0, deltaFear: 75, deltaRespect: 0, 0);
                a[0].Memory.SpatialMemoryMap[bushFearId] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushFearId, EntityType.Resource, new Vector2Int(-8, 0), 0);
                a[1].Memory.SpatialMemoryMap[bushFearId] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushFearId, EntityType.Resource, new Vector2Int(-8, 0), 0);

                // Dyad 2 (Respect Asymmetry): Agent 3 respects Agent 2 (Respect = 80). Agent 3 will defer Bush 1 to Agent 2.
                a[2].Hunger = 60f;
                a[3].Hunger = 60f;
                a[3].Memory.RecordInteraction(a[2].AgentId, deltaWarmth: 20, deltaTrust: 20, deltaFear: 0, deltaRespect: 80, 0);
                a[2].Memory.SpatialMemoryMap[bushRespectId] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushRespectId, EntityType.Resource, new Vector2Int(-4, 0), 0);
                a[3].Memory.SpatialMemoryMap[bushRespectId] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushRespectId, EntityType.Resource, new Vector2Int(-4, 0), 0);

                // Dyad 3 (Symmetrical Rivalry): Agent 4 and Agent 5 are mutual rivals (Warmth -40, Respect -20).
                a[4].Hunger = 60f;
                a[5].Hunger = 60f;
                a[4].Memory.RecordInteraction(a[5].AgentId, deltaWarmth: -40, deltaTrust: -30, deltaFear: 0, deltaRespect: -20, 0);
                a[5].Memory.RecordInteraction(a[4].AgentId, deltaWarmth: -40, deltaTrust: -30, deltaFear: 0, deltaRespect: -20, 0);
                a[4].Memory.SpatialMemoryMap[bushRivalryId] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushRivalryId, EntityType.Resource, new Vector2Int(0, 0), 0);
                a[5].Memory.SpatialMemoryMap[bushRivalryId] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushRivalryId, EntityType.Resource, new Vector2Int(0, 0), 0);
            }

            if (a.Count >= 10)
            {
                // Dyad 4 (Altruistic Companions & Hearth): Agent 6 loves Agent 7 (Warmth = 85, Trust = 80).
                // Agent 6 is sated (Hunger = 20), Agent 7 is starving (Hunger = 90).
                // Agent 6 will approach Agent 7, feed Agent 7, and once sated, gather around the Pen 4 campfire!
                a[6].Hunger = 20f;
                a[7].Hunger = 90f;
                a[6].Memory.RecordInteraction(a[7].AgentId, deltaWarmth: 85, deltaTrust: 80, deltaFear: 0, deltaRespect: 20, 0);
                a[7].Memory.RecordInteraction(a[6].AgentId, deltaWarmth: 85, deltaTrust: 80, deltaFear: 0, deltaRespect: 20, 0);
                a[6].Memory.SpatialMemoryMap[campfirePen4Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen4Id, EntityType.Anchor, new Vector2Int(4, 0), 0);
                a[7].Memory.SpatialMemoryMap[campfirePen4Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen4Id, EntityType.Anchor, new Vector2Int(4, 0), 0);

                // Dyad 5 (Selfish Indifference & Hearth Isolation): Agent 8 is estranged/hostile to Agent 9 (Warmth = -20, Trust = -10).
                // Agent 8 is sated (Hunger = 20), Agent 9 is starving (Hunger = 90).
                // Agent 8 ignores Agent 9 and relaxes alone by the Pen 5 campfire; starving Agent 9 receives no aid.
                a[8].Hunger = 20f;
                a[9].Hunger = 90f;
                a[8].Memory.RecordInteraction(a[9].AgentId, deltaWarmth: -20, deltaTrust: -10, deltaFear: 0, deltaRespect: -10, 0);
                a[9].Memory.RecordInteraction(a[8].AgentId, deltaWarmth: -20, deltaTrust: -10, deltaFear: 0, deltaRespect: -10, 0);
                a[8].Memory.SpatialMemoryMap[campfirePen5Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen5Id, EntityType.Anchor, new Vector2Int(8, 0), 0);
                a[9].Memory.SpatialMemoryMap[campfirePen5Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen5Id, EntityType.Anchor, new Vector2Int(8, 0), 0);
            }

            if (a.Count >= 24 && session.Groups != null)
            {
                var groupRegistry = session.Groups;

                // Pen 6 (Collective Directives & Compliance):
                // Agent 10 is Leader. Agent 11 is Follower with high Allegiance (85) and Respect (70).
                a[10].Hunger = 20f;
                a[11].Hunger = 20f;
                a[11].Memory.RecordInteraction(a[10].AgentId, deltaWarmth: 50, deltaTrust: 50, deltaFear: 0, deltaRespect: 70, 0);

                byte g6 = groupRegistry.CreateGroup(Simpiens.Cognition.Groups.GroupType.Settlement, a[10].AgentId, new Vector2(-8f, -9f), initialCohesion: 80);
                groupRegistry.AddMember(g6, a[11].AgentId, Simpiens.Cognition.Groups.RoleRank.Follower, initialAllegiance: 85);
                a[10].Membership = new Simpiens.Cognition.Groups.GroupMembership(g6, Simpiens.Cognition.Groups.RoleRank.Leader, 100, 0);
                a[11].Membership = new Simpiens.Cognition.Groups.GroupMembership(g6, Simpiens.Cognition.Groups.RoleRank.Follower, 85, 0);

                var dir6 = new Simpiens.Cognition.Groups.GroupDirective(
                    Simpiens.Cognition.Groups.DirectiveType.RallyAtLocation,
                    groupId: g6,
                    leaderId: a[10].AgentId,
                    targetLocation: new Vector2(-8f, -7.5f),
                    targetEntityId: default,
                    issuedTick: 0,
                    expirationTick: 5000,
                    targetRadius: 2.0f);
                groupRegistry.SetDirective(g6, in dir6);

                // Pen 7 (Directives & Passive Defiance):
                // Agent 12 is Leader. Agent 13 is Follower with low Allegiance (15), low Respect (-30), and starving (Hunger = 85).
                // Agent 13 knows of the bush at (-4, -11). Agent 13 defies rally directive to forage food.
                a[12].Hunger = 20f;
                a[13].Hunger = 85f;
                a[13].Memory.RecordInteraction(a[12].AgentId, deltaWarmth: -20, deltaTrust: -20, deltaFear: 0, deltaRespect: -30, 0);
                a[13].Memory.SpatialMemoryMap[bushPen7Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushPen7Id, EntityType.Resource, new Vector2Int(-4, -11), 0);

                byte g7 = groupRegistry.CreateGroup(Simpiens.Cognition.Groups.GroupType.TemporaryParty, a[12].AgentId, new Vector2(-4f, -9f), initialCohesion: 40);
                groupRegistry.AddMember(g7, a[13].AgentId, Simpiens.Cognition.Groups.RoleRank.Follower, initialAllegiance: 15);
                a[12].Membership = new Simpiens.Cognition.Groups.GroupMembership(g7, Simpiens.Cognition.Groups.RoleRank.Leader, 100, 0);
                a[13].Membership = new Simpiens.Cognition.Groups.GroupMembership(g7, Simpiens.Cognition.Groups.RoleRank.Follower, 15, 0);

                var dir7 = new Simpiens.Cognition.Groups.GroupDirective(
                    Simpiens.Cognition.Groups.DirectiveType.RallyAtLocation,
                    groupId: g7,
                    leaderId: a[12].AgentId,
                    targetLocation: new Vector2(-4f, -7.2f),
                    targetEntityId: default,
                    issuedTick: 0,
                    expirationTick: 5000,
                    targetRadius: 2.0f);
                groupRegistry.SetDirective(g7, in dir7);

                // Pen 8 (Organic Hearth Emergence):
                // Agents 14, 15, 16 are unaligned and gather at the campfire anchor.
                // High mutual warmth/trust; high respect for Agent 14. Campfire consensus elects Agent 14.
                a[14].Hunger = 20f;
                a[15].Hunger = 20f;
                a[16].Hunger = 20f;

                a[14].Membership = Simpiens.Cognition.Groups.GroupMembership.None;
                a[15].Membership = Simpiens.Cognition.Groups.GroupMembership.None;
                a[16].Membership = Simpiens.Cognition.Groups.GroupMembership.None;

                a[14].Memory.SpatialMemoryMap[campfirePen8Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen8Id, EntityType.Anchor, new Vector2Int(0, -9), 0);
                a[15].Memory.SpatialMemoryMap[campfirePen8Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen8Id, EntityType.Anchor, new Vector2Int(0, -9), 0);
                a[16].Memory.SpatialMemoryMap[campfirePen8Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(campfirePen8Id, EntityType.Anchor, new Vector2Int(0, -9), 0);

                a[14].Memory.RecordInteraction(a[15].AgentId, deltaWarmth: 60, deltaTrust: 60, deltaFear: 0, deltaRespect: 40, 0);
                a[15].Memory.RecordInteraction(a[14].AgentId, deltaWarmth: 60, deltaTrust: 60, deltaFear: 0, deltaRespect: 75, 0);

                a[14].Memory.RecordInteraction(a[16].AgentId, deltaWarmth: 65, deltaTrust: 65, deltaFear: 0, deltaRespect: 40, 0);
                a[16].Memory.RecordInteraction(a[14].AgentId, deltaWarmth: 65, deltaTrust: 65, deltaFear: 0, deltaRespect: 80, 0);

                a[15].Memory.RecordInteraction(a[16].AgentId, deltaWarmth: 50, deltaTrust: 50, deltaFear: 0, deltaRespect: 20, 0);
                a[16].Memory.RecordInteraction(a[15].AgentId, deltaWarmth: 50, deltaTrust: 50, deltaFear: 0, deltaRespect: 20, 0);

                // Pen 9 (Leadership Succession Contest - Dominance Challenge):
                // Group 3 with Cohesion 35. Incumbent leader Agent 17 collapsed from exhaustion.
                // Challenger Agent 18 challenges for dominance and wins.
                a[17].CollapseFromExhaustion();
                a[17].Hunger = 50f;
                a[18].Hunger = 20f;
                a[19].Hunger = 20f;

                a[18].Memory.SpatialMemoryMap[bushPen9Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushPen9Id, EntityType.Resource, new Vector2Int(4, -11), 0);
                a[19].Memory.SpatialMemoryMap[bushPen9Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushPen9Id, EntityType.Resource, new Vector2Int(4, -11), 0);

                byte g9 = groupRegistry.CreateGroup(Simpiens.Cognition.Groups.GroupType.TemporaryParty, a[17].AgentId, new Vector2(4f, -9f), initialCohesion: 35);
                groupRegistry.AddMember(g9, a[18].AgentId, Simpiens.Cognition.Groups.RoleRank.Follower, initialAllegiance: 60);
                groupRegistry.AddMember(g9, a[19].AgentId, Simpiens.Cognition.Groups.RoleRank.Follower, initialAllegiance: 65);
                a[17].Membership = new Simpiens.Cognition.Groups.GroupMembership(g9, Simpiens.Cognition.Groups.RoleRank.Leader, 100, 0);
                a[18].Membership = new Simpiens.Cognition.Groups.GroupMembership(g9, Simpiens.Cognition.Groups.RoleRank.Follower, 60, 0);
                a[19].Membership = new Simpiens.Cognition.Groups.GroupMembership(g9, Simpiens.Cognition.Groups.RoleRank.Follower, 65, 0);

                a[18].Memory.RecordInteraction(a[17].AgentId, deltaWarmth: -20, deltaTrust: -10, deltaFear: 0, deltaRespect: -20, 0);
                a[17].Memory.RecordInteraction(a[18].AgentId, deltaWarmth: -10, deltaTrust: -10, deltaFear: 65, deltaRespect: 0, 0);
                a[19].Memory.RecordInteraction(a[18].AgentId, deltaWarmth: 10, deltaTrust: 20, deltaFear: 30, deltaRespect: 60, 0);

                // Pen 10 (Factional Schism):
                // Group 4 with critical Cohesion 15. Leader Agent 20 is discredited (negative follower respect).
                // Group fractures into two splinter bands: 20+21 (Loyalists) and 22+23 (Dissidents).
                a[20].Hunger = 20f;
                a[21].Hunger = 20f;
                a[22].Hunger = 20f;
                a[23].Hunger = 20f;

                a[20].Memory.SpatialMemoryMap[bushPen10Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushPen10Id, EntityType.Resource, new Vector2Int(8, -11), 0);
                a[21].Memory.SpatialMemoryMap[bushPen10Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushPen10Id, EntityType.Resource, new Vector2Int(8, -11), 0);
                a[22].Memory.SpatialMemoryMap[bushPen10Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushPen10Id, EntityType.Resource, new Vector2Int(8, -11), 0);
                a[23].Memory.SpatialMemoryMap[bushPen10Id] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(bushPen10Id, EntityType.Resource, new Vector2Int(8, -11), 0);

                byte g10 = groupRegistry.CreateGroup(Simpiens.Cognition.Groups.GroupType.TemporaryParty, a[20].AgentId, new Vector2(8f, -9f), initialCohesion: 15);
                groupRegistry.AddMember(g10, a[21].AgentId, Simpiens.Cognition.Groups.RoleRank.Follower, initialAllegiance: 75);
                groupRegistry.AddMember(g10, a[22].AgentId, Simpiens.Cognition.Groups.RoleRank.Follower, initialAllegiance: 30);
                groupRegistry.AddMember(g10, a[23].AgentId, Simpiens.Cognition.Groups.RoleRank.Follower, initialAllegiance: 30);
                a[20].Membership = new Simpiens.Cognition.Groups.GroupMembership(g10, Simpiens.Cognition.Groups.RoleRank.Leader, 100, 0);
                a[21].Membership = new Simpiens.Cognition.Groups.GroupMembership(g10, Simpiens.Cognition.Groups.RoleRank.Follower, 75, 0);
                a[22].Membership = new Simpiens.Cognition.Groups.GroupMembership(g10, Simpiens.Cognition.Groups.RoleRank.Follower, 30, 0);
                a[23].Membership = new Simpiens.Cognition.Groups.GroupMembership(g10, Simpiens.Cognition.Groups.RoleRank.Follower, 30, 0);

                // Agent 21 is loyal to Agent 20
                a[21].Memory.RecordInteraction(a[20].AgentId, deltaWarmth: 80, deltaTrust: 80, deltaFear: 0, deltaRespect: 70, 0);

                // Agents 22 and 23 hate Agent 20 (causing negative average follower respect)
                a[22].Memory.RecordInteraction(a[20].AgentId, deltaWarmth: -60, deltaTrust: -60, deltaFear: 0, deltaRespect: -70, 0);
                a[23].Memory.RecordInteraction(a[20].AgentId, deltaWarmth: -60, deltaTrust: -60, deltaFear: 0, deltaRespect: -70, 0);

                // Agents 22 and 23 have high mutual loyalty
                a[22].Memory.RecordInteraction(a[23].AgentId, deltaWarmth: 80, deltaTrust: 80, deltaFear: 0, deltaRespect: 60, 0);
                a[23].Memory.RecordInteraction(a[22].AgentId, deltaWarmth: 80, deltaTrust: 80, deltaFear: 0, deltaRespect: 60, 0);
            }
        }

        /// <summary>
        /// Readable log label: "PenN.AgentI".
        /// </summary>
        public static string AgentLabel(int index)
        {
            int pen = index < 10 ? index / 2 + 1
                : index < 14 ? (index - 10) / 2 + 6
                : index < 17 ? 8
                : index < 20 ? 9
                : 10;
            return $"Pen{pen}.Agent{index}";
        }
    }
}
