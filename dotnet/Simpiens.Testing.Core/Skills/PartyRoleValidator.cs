using System;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Skills;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// Party roles the guild's orders read (Epic 11, S5.4, Q11, D22): the order holds the front only at a clear's fight; a
    /// member holding the front draws a creature's eye 3 units nearer and steps in for a mate of the same fight within 3 units;
    /// the Back are armed to shoot first; a Carrier is sent to the fallen first; nothing changes outside a clear's fight. The
    /// front's hold on the blows is seen live (S5.9). Runs as a test (ValidatorTests).
    /// </summary>
    public class PartyRoleValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[PartyRoleValidator] Beginning party role validation...");
            ValidateTheFront();
            ValidateTheBack();
            ValidateTheCarrier();
            TestLog.Log("<color=green>[PartyRoleValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static ExpeditionOrder Order(ExpeditionStage stage, ExpeditionKind kind, PartyRole role, byte lair = 1) =>
            new ExpeditionOrder(stage, 3, default, Vec2.Zero, kind, lair, role: role);

        private static void ValidateTheFront()
        {
            var front = Order(ExpeditionStage.Fight, ExpeditionKind.Clear, PartyRole.Front);
            var mate = Order(ExpeditionStage.Fight, ExpeditionKind.Clear, PartyRole.None);
            var marching = Order(ExpeditionStage.March, ExpeditionKind.Clear, PartyRole.Front);
            var gathering = Order(ExpeditionStage.Work, ExpeditionKind.Gather, PartyRole.Front);
            Assert(front.HoldsTheFront && !mate.HoldsTheFront && !marching.HoldsTheFront && !gathering.HoldsTheFront, "The front is held at a clear's fight, by the Front alone.");
            Assert(CreatureService.DrawnDistance(front, 5f) == 2f && CreatureService.DrawnDistance(mate, 5f) == 5f && CreatureService.DrawnDistance(marching, 5f) == 5f,
                "A creature sees the front 3 units nearer, at the fight only.");
            Assert(CreatureService.StepsIn(mate, front, 9f) && !CreatureService.StepsIn(mate, front, 9.5f), "The front steps in for a mate within 3 units of the creature, not beyond.");
            Assert(!CreatureService.StepsIn(mate, Order(ExpeditionStage.Fight, ExpeditionKind.Clear, PartyRole.Front, lair: 2), 1f), "Not for another fight's.");
            Assert(!CreatureService.StepsIn(front, front, 1f), "Not for itself at the front.");
            Assert(!CreatureService.StepsIn(Order(ExpeditionStage.Work, ExpeditionKind.Gather, PartyRole.None), front, 1f) && !CreatureService.StepsIn(default, front, 1f),
                "Not for a crawler outside the fight.");
            TestLog.Log("[PASS] Test 1: the front: held at a clear's fight; it draws the eye 3 units nearer and steps in for a mate of the same fight within 3 units.");
        }

        private static void ValidateTheBack()
        {
            bool[] Shoots(int shooters, params PartyRole[] roles)
            {
                var shoots = new bool[roles.Length];
                GuildRules.Shooters(roles, shooters, shoots);
                return shoots;
            }
            var n = PartyRole.None;
            Assert(Same(Shoots(1, n, n, n, n), false, false, false, true), "With no Back, the last shoots, as before.");
            Assert(Same(Shoots(1, n, PartyRole.Back, n, n), false, true, false, false), "The Back shoots first.");
            Assert(Same(Shoots(2, n, PartyRole.Back, n, n), false, true, false, true), "Then the back of the list.");
            Assert(Same(Shoots(1, PartyRole.Back, PartyRole.Back, n, n), true, false, false, false), "As many as asked: the first Back.");
            Assert(Same(Shoots(0, PartyRole.Back, n), false, false) && Same(Shoots(5, n, n), true, true), "None asked, none shoot; more asked than there are, all do.");
            TestLog.Log("[PASS] Test 2: the Back are armed to shoot first, then the back of the party, as many as asked.");
        }

        private static void ValidateTheCarrier()
        {
            var surgeon = Crawler(1, 60f, Calling.FieldSurgeon);
            var fighter = Crawler(2, 90f, Calling.Warden);
            var other = Crawler(3, 90f, Calling.None);
            Assert(GuildRules.CarriesBefore(surgeon, 9f, fighter, 1f) && !GuildRules.CarriesBefore(fighter, 1f, surgeon, 9f), "A Carrier first, though hurt and farther.");
            Assert(GuildRules.CarriesBefore(fighter, 4f, other, 9f) && !GuildRules.CarriesBefore(other, 9f, fighter, 4f), "Between the rest, as before: the same health, the nearer.");
            Assert(GuildRules.CarriesBefore(other, 1f, null, float.MaxValue), "Anyone before nobody.");
            TestLog.Log("[PASS] Test 3: a Carrier by calling is sent to the fallen first; the rest by health, then distance, as before.");
        }

        private static AgentState Crawler(uint n, float health, Calling calling)
        {
            var crawler = new AgentState(new EntityId(n, 9, 9, 9)) { Health = health };
            var traits = default(CrawlerTraits);
            traits.SetCalling(calling);
            crawler.Traits = traits;
            return crawler;
        }

        private static bool Same(bool[] got, params bool[] want)
        {
            if (got.Length != want.Length) return false;
            for (int i = 0; i < got.Length; i++) if (got[i] != want[i]) return false;
            return true;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[PartyRoleValidator] FAILED: " + message);
        }
    }
}
