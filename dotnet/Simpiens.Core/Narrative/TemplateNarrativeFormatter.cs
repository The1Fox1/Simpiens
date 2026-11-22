using System;
using Simpiens.Simulation.World;
namespace Simpiens.Narrative
{
    /// <summary>
    /// Stage 1 deterministic natural language narrative synthesizer.
    /// Turns structured chronicle events into humanized prose using contextual sentence templates.
    /// Null-safe and test-isolated.
    /// </summary>
    public class TemplateNarrativeFormatter : INarrativeFormatter
    {
        private readonly IAgentIdentityService _identityService;
        private readonly ISectorRegistry _sectors;

        public TemplateNarrativeFormatter() : this(null, null)
        {
        }

        public TemplateNarrativeFormatter(
            IAgentIdentityService identityService = null,
            ISectorRegistry sectors = null)
        {
            _identityService = identityService ?? new AgentIdentityService();
            _sectors = sectors;
        }

        public string FormatEvent(in ChronicleEvent evt)
        {
            string actorA = ResolveActorName(evt.ActorAId, "Someone");
            string actorB = ResolveActorName(evt.ActorBId, "a peer");
            string sector = ResolveSectorName(evt.SectorId);

            switch (evt.EventType)
            {
                case ChronicleEventType.CampfireGossip:
                    return $"{actorA} shared campfire rumors with {actorB} in {sector}.";

                case ChronicleEventType.CampfireCompanionship:
                    return $"{actorA} enjoyed quiet companionship with {actorB} near the hearth in {sector}.";

                case ChronicleEventType.AltruisticFoodShare:
                    float amount = evt.ExtraData > 0f ? evt.ExtraData : 40f;
                    return $"{actorA} shared {amount:F0} rations with starving peer {actorB} in {sector}.";

                case ChronicleEventType.ResourceYielded:
                    return $"{actorA} stepped aside and conceded the resource to {actorB} in {sector}.";

                case ChronicleEventType.ResourceClaimed:
                    return $"{actorA} claimed the foraging node after {actorB} yielded in {sector}.";

                case ChronicleEventType.DirectiveIssued:
                    return $"{actorA} issued a collective directive to all agents in {sector}.";

                case ChronicleEventType.DirectiveComplied:
                    return $"{actorA} completed their assigned directive in {sector}.";

                case ChronicleEventType.TantrumStarted:
                    return $"{actorA} flew into an angry tantrum from high frustration in {sector}!";

                case ChronicleEventType.ExhaustionCollapsed:
                    return $"{actorA} collapsed onto the floor from pure exhaustion in {sector}.";

                case ChronicleEventType.StarvationPanic:
                    return $"{actorA} panicked frantically searching for food in {sector}!";

                case ChronicleEventType.GroupFormed:
                    return $"{actorA} and {actorB} banded together to establish a new collective in {sector}.";

                case ChronicleEventType.LeadershipAssumed:
                    return $"{actorA} assumed leadership of the group in {sector}.";

                case ChronicleEventType.LeadershipChallenged:
                    return $"{actorA} openly challenged {actorB}'s authority in {sector}!";

                case ChronicleEventType.GroupSchism:
                    return $"A bitter factional grievance fractured the group in {sector}!";

                case ChronicleEventType.TradeTransaction:
                    return $"{actorA} completed a trade at the outpost stall in {sector}.";

                case ChronicleEventType.BountyPosted:
                    return $"A public bounty was posted at the outpost board in {sector}.";

                case ChronicleEventType.BountyClaimed:
                    return $"{actorA} claimed a public bounty contract in {sector}.";

                case ChronicleEventType.BountyFulfilled:
                    return $"{actorA} delivered their bounty contract and collected their gold reward in {sector}.";

                case ChronicleEventType.BountyExpired:
                    return $"An unfulfilled bounty contract expired in {sector}.";

                case ChronicleEventType.UpkeepUnpaid:
                    return "The outpost couldn't pay its upkeep; the stall stops buying salvage until it can.";

                case ChronicleEventType.SupplyCaravanArrived:
                    return "A supply caravan restocked the outpost stall with rations.";

                case ChronicleEventType.ResourceRegrown:
                    return $"A depleted salvage or forage node regrew in {sector}.";

                case ChronicleEventType.DrifterArrived:
                    return $"{actorA}, a drifter, came in by the Road Gate.";

                case ChronicleEventType.DrifterLeft:
                    return $"{actorA} moved on by the Road Gate.";

                case ChronicleEventType.CrawlerDowned:
                    return $"{actorA} was overcome in {sector} and limps home.";

                case ChronicleEventType.CrawlerRecovered:
                    return $"{actorA} is back on its feet.";

                case ChronicleEventType.GearWornOut:
                    return $"{actorA}'s hazard gear wore out in {sector}.";

                case ChronicleEventType.GuildJoined:
                    return $"{actorA} signed on with the guild, at {(int)evt.ExtraData}g a visit.";

                case ChronicleEventType.GuildUnpaid:
                    return $"{actorA} went unpaid this visit.";

                case ChronicleEventType.GuildQuit:
                    return $"{actorA} quit the guild.";

                case ChronicleEventType.GuildDismissed:
                    return $"{actorA} was let go from the guild.";

                case ChronicleEventType.ExpeditionSent:
                    return (int)evt.ExtraData > 1 ? $"{actorA} and {(int)evt.ExtraData - 1} more set out for {sector}." : $"{actorA} set out alone for {sector}.";

                case ChronicleEventType.ExpeditionTurnedBack:
                    return $"{actorA}'s party turned back from {sector}.";

                case ChronicleEventType.ExpeditionReturned:
                    return $"{actorA}'s party came back from {sector} with a haul of {(int)evt.ExtraData}.";

                case ChronicleEventType.ExpeditionDowned:
                    return $"{actorA} was downed in {sector} on an expedition.";

                case ChronicleEventType.StationBuilt:
                    return $"The {Station(evt)} was built in {sector}.";

                case ChronicleEventType.ArtisanHired:
                    return $"{actorA} took up work in {sector}, at {(int)evt.ExtraData}g a visit.";

                case ChronicleEventType.ArtisanUnpaid:
                    return $"{actorA} went unpaid at the {Station(evt)} this visit.";

                case ChronicleEventType.ArtisanQuit:
                    return $"{actorA} quit their work at the {Station(evt)}.";

                case ChronicleEventType.ArtisanDismissed:
                    return $"{actorA} was let go from the {Station(evt)}.";

                case ChronicleEventType.ArtisanBrokeDown:
                    return $"{actorA} broke down at the {Station(evt)}, worked past the end of their strength.";

                case ChronicleEventType.ToolWornOut:
                    return $"{actorA}'s {(Simpiens.Simulation.Items.ItemCatalog.Get((Simpiens.Simulation.Economy.ItemType)(byte)evt.ExtraData)?.DisplayName ?? "tool").ToLowerInvariant()} broke in {sector}.";

                case ChronicleEventType.CrawlerDied:
                {
                    int cause = (int)evt.ExtraData / 256, detail = (int)evt.ExtraData % 256;
                    if (cause == (int)Simpiens.Simulation.DeathCause.Creature)
                        return $"{actorA} was downed by a {(Simpiens.Simulation.Creatures.CreatureCatalog.Get((Simpiens.Simulation.Creatures.CreatureKind)detail)?.DisplayName ?? "creature").ToLowerInvariant()} in {sector} and bled out before anyone came.";
                    return $"{actorA} fell in the danger of {sector} and bled out before anyone came.";
                }

                case ChronicleEventType.CrawlerRescued:
                    return (int)evt.ExtraData == 2 ? $"{actorA} gave {actorB} a draught where they lay in {sector}." : $"{actorA} carried {actorB} out of danger in {sector}.";

                case ChronicleEventType.NewcomerArrived:
                    return $"{actorA} came in by the Road Gate to settle at the outpost.";

                case ChronicleEventType.SkillBandReached:
                {
                    var skill = Simpiens.Simulation.Skills.SkillTable.Get((Simpiens.Simulation.Skills.Skill)((int)evt.ExtraData / 256));
                    string band = Simpiens.Simulation.Skills.SkillRules.NameOf((Simpiens.Simulation.Skills.SkillBand)((int)evt.ExtraData % 256)).ToLowerInvariant();
                    string practitioner = $"{(band.StartsWith("a") ? "an" : "a")} {band} {skill?.Practitioner ?? "hand"}";
                    return skill?.Voice != null ? $"{Voiced(skill.Voice.Rising, actorA)}: {practitioner} now." : $"{actorA} is now {practitioner}."; // the skill's voice (S6.4)
                }

                case ChronicleEventType.FirstLesson:
                    return $"{actorA} learned {(Simpiens.Simulation.Skills.SkillTable.Get((Simpiens.Simulation.Skills.Skill)(byte)evt.ExtraData)?.DisplayName ?? "a craft").ToLowerInvariant()} at {actorB}'s side.";

                case ChronicleEventType.RaiseAsked:
                    return $"{actorA} asks for a raise, to {(int)evt.ExtraData}g a visit.";

                case ChronicleEventType.TechniqueChosen:
                {
                    var technique = Simpiens.Simulation.Skills.TechniqueTable.Get((Simpiens.Simulation.Skills.Technique)(byte)evt.ExtraData);
                    var other = Simpiens.Simulation.Skills.TechniqueTable.Get(Simpiens.Simulation.Skills.TechniqueTable.OtherThan(technique?.Id ?? default));
                    if (technique == null) return $"{actorA} found a way of their own.";
                    return other == null ? $"{actorA} took up {technique.DisplayName}." : $"{actorA} took up {technique.DisplayName} over {other.DisplayName}.";
                }

                case ChronicleEventType.CallingEarned:
                {
                    string word = Simpiens.Simulation.Skills.CallingTable.Get((Simpiens.Simulation.Skills.Calling)(byte)evt.ExtraData)?.DisplayName ?? "someone new";
                    return $"{actorA} is now {("AEIOU".IndexOf(word[0]) >= 0 ? "an" : "a")} {word}.";
                }

                case ChronicleEventType.EpithetEarned:
                {
                    int extra = (int)evt.ExtraData;
                    var epithet = (Simpiens.Simulation.Skills.Epithet)(extra / 256 % 256);
                    string words = Simpiens.Simulation.Skills.EpithetRules.WordsOf(epithet, (byte)(extra % 256), actorB);
                    var voice = Simpiens.Simulation.Skills.SkillTable.Get((Simpiens.Simulation.Skills.Skill)(extra / 65536))?.Voice; // its best skill's (S6.4)
                    var row = Simpiens.Simulation.Skills.EpithetTable.Get(epithet);
                    var deed = row != null ? Simpiens.Simulation.Skills.DeedTable.Get(row.Deed) : null;
                    if (words.Length == 0) return $"{actorA} has earned a name.";
                    string named = Simpiens.Simulation.Skills.EpithetRules.NameWith(actorA, words);
                    string why = deed == null ? string.Empty : $", for {deed.CountOf(row.Count)}";
                    return voice != null ? $"{Voiced(voice.Named, actorA)}: {named}{why}." : $"{actorA} is {named} now{why}.";
                }

                case ChronicleEventType.ScarTaken:
                {
                    string scar = Simpiens.Simulation.Skills.ScarTable.Describe((Simpiens.Simulation.Skills.Scar)((int)evt.ExtraData / 256), (int)evt.ExtraData % 256);
                    return scar.Length == 0 ? $"{actorA} lived, and bears the mark." : $"{actorA} lived, and bears {scar}.";
                }

                case ChronicleEventType.TraitEarned:
                {
                    var trait = Simpiens.Simulation.Skills.EarnedTraitTable.Get((Simpiens.Simulation.Skills.EarnedTrait)(byte)evt.ExtraData);
                    if (trait == null) return $"{actorA} has changed.";
                    var deed = Simpiens.Simulation.Skills.DeedTable.Get(trait.Deed);
                    return deed == null ? $"{actorA} is {trait.Words} now." : $"{actorA} is {trait.Words} now, after {deed.CountOf(trait.Count)}.";
                }

                case ChronicleEventType.TechniqueSponsored:
                    return $"{actorA} took up {Simpiens.Simulation.Skills.TechniqueTable.Get((Simpiens.Simulation.Skills.Technique)(byte)evt.ExtraData)?.DisplayName ?? "a new way"}, for the patron's gold.";

                case ChronicleEventType.GateBreached:
                    return $"{actorA} broke the Gatekeeper in {sector}: the descent gate stands open, its stairs going down into the dark.";

                case ChronicleEventType.FirstBlood:
                {
                    int extra = (int)evt.ExtraData;
                    string kind = (Simpiens.Simulation.Creatures.CreatureCatalog.Get((Simpiens.Simulation.Creatures.CreatureKind)(extra % 256))?.DisplayName ?? "creature").ToLowerInvariant();
                    var voice = Simpiens.Simulation.Skills.SkillTable.Get((Simpiens.Simulation.Skills.Skill)(extra / 256))?.Voice;
                    return voice != null ? $"{Voiced(voice.Blooded, actorA)}: a {kind}, in {sector}." : $"{actorA} drew first blood: a {kind}, in {sector}.";
                }

                // A creature's name (Epic 11, S7.1, D31): from the crawler it downed or killed, and its rank rising with each it downs
                case ChronicleEventType.CreatureNamed:
                case ChronicleEventType.CreatureRankRose:
                {
                    int renown = (int)evt.ExtraData;
                    string kind = (Simpiens.Simulation.Creatures.CreatureCatalog.Get((Simpiens.Simulation.Creatures.CreatureKind)(renown % 256))?.DisplayName ?? "creature").ToLowerInvariant();
                    string word = Simpiens.Simulation.Creatures.CreatureTitleTable.WordOf(renown / 256 % 4);
                    if (evt.EventType == ChronicleEventType.CreatureNamed)
                        return $"The {kind} that {((renown & 1024) != 0 ? "killed" : "downed")} {actorB} in {sector} has a name now: {Simpiens.Simulation.Creatures.CreatureTitleTable.NameOf(actorB, renown / 256 % 4)}.";
                    return $"{actorB} fell to {actorA} in {sector}, the {Ordinal(renown / 2048)} it has downed: its name grows to {word}.";
                }

                case ChronicleEventType.CreatureSlain:
                    return $"{actorA} slew a {(Simpiens.Simulation.Creatures.CreatureCatalog.Get((Simpiens.Simulation.Creatures.CreatureKind)(byte)evt.ExtraData)?.DisplayName ?? "creature").ToLowerInvariant()} in {sector}.";

                default:
                    return $"{actorA} interacted with {actorB} in {sector}.";
            }
        }

        /// <summary>"second", "third", ... "tenth", then "11th" (Epic 11, S7.1: the crawlers a named creature has downed).</summary>
        private static string Ordinal(int n) => n switch
        {
            1 => "first", 2 => "second", 3 => "third", 4 => "fourth", 5 => "fifth", 6 => "sixth", 7 => "seventh", 8 => "eighth", 9 => "ninth", 10 => "tenth",
            _ => n % 100 is 11 or 12 or 13 ? $"{n}th" : (n % 10) switch { 1 => $"{n}st", 2 => $"{n}nd", 3 => $"{n}rd", _ => $"{n}th" },
        };

        /// <summary>The station an event's extra names (Epic 10 Part 2): "forge".</summary>
        private static string Station(in ChronicleEvent evt) => Simpiens.Simulation.Workshops.WorkshopRules.StationName((Simpiens.Simulation.Items.StationKind)(byte)evt.ExtraData);

        private string ResolveActorName(EntityId actorId, string defaultName)
        {
            if (actorId == default) return defaultName;
            return _identityService != null ? _identityService.GetFriendlyName(actorId) : defaultName;
        }

        private string ResolveSectorName(int sectorId)
        {
            // The sector's name, or the fallback sector's for an unknown id (as the dashboard's zones resolve them)
            if (_sectors != null)
            {
                string name = _sectors.TryGetSector(sectorId, out var sector) ? sector.Name : _sectors.Fallback.Name;
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }

            if (sectorId == 0) return "Transit Corridor";
            return $"Sector {sectorId}";
        }

        // A voice's words with the crawler's name (Epic 11, S6.4, D30)
        private static string Voiced(string words, string name) => string.Format(System.Globalization.CultureInfo.InvariantCulture, words, name);
    }
}
