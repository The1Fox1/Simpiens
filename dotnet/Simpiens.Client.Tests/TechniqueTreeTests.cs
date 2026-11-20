using System.Linq;
using Gum.Wireframe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simpiens.Client.Ui;
using Simpiens.Cognition;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The technique tree (Epic 11, S4.11, D25; the producer's look at S4): the dossier's Sponsor opens a window with a skill
    /// tree, each skill's bands and their two techniques; a click picks a technique, and Offer sponsors it when the crawler
    /// has reached its band and the rule allows; a refusal says why; anyone's tree can be read from the techniques line, but
    /// only the patron's people take its gold; every technique's words fit its node. Skills and techniques are set by hand on
    /// the live outpost.
    /// </summary>
    public class TechniqueTreeTests
    {
        private const int Width = 1280, Height = 720;

        [Fact]
        public void SponsorOpensTheTreeOnTheOffer()
        {
            var (ui, member, _) = Start();
            var dashboard = ui.Game.Play.Dashboard;
            var tree = ui.Game.Session.DashboardModel.Techniques;
            Assert.Equal("3 techniques open to sponsor, from 20g.", dashboard.Panels.RecruitShown); // Reckless Fury; Hard Driver and Rich Veins
            Assert.Equal("Sponsor", dashboard.Panels.SwayButtonShown);
            ui.Click(Centre(dashboard.Panels.RecruitButton));
            Assert.True(tree.IsOpen && dashboard.Techniques.Window.IsShown, "Sponsor opens the tree.");
            Assert.Equal(member.AgentId, tree.CrawlerId);

            // On the dossier's offer: the other technique at the top band of its best skill
            Assert.Equal(Skill.Mining, tree.SelectedSkill);
            Assert.Equal(Technique.RichVeins, tree.Selected);
            var window = dashboard.Techniques;
            Assert.Equal("Mining 7", window.RootShown);
            Assert.Equal(("Seam Student", TechniqueTreeModel.HeldTag), (window.NodeName(0, 0), window.NodeTag(0, 0)));
            Assert.Equal(("Hard Driver", "20g"), (window.NodeName(0, 1), window.NodeTag(0, 1)));
            Assert.Equal(("Patient Pick", TechniqueTreeModel.HeldTag), (window.NodeName(1, 0), window.NodeTag(1, 0)));
            Assert.Equal(("Rich Veins", "40g"), (window.NodeName(1, 1), window.NodeTag(1, 1)));
            Assert.Equal(("Old Hand's Lessons", "level 9"), (window.NodeName(2, 0), window.NodeTag(2, 0)));
            Assert.True(window.NodeLit(0, 0) && window.NodeLit(1, 0) && !window.NodeLit(1, 1) && !window.NodeLit(2, 0), "The held are lit stone.");
            Assert.Equal(new[] { "learns 25% faster", "10% slower" }, window.NodeEffects(0, 0).Select(e => e.Text).ToArray());
            var picked = window.NodeButton(1, 1).Visual;
            Assert.True(window.Ring.Visible && window.Ring.X < picked.X && window.Ring.X + window.Ring.Width > picked.X + picked.Width, "A ring round the technique picked.");
            // The trunk lit past Journeyman, short of Master
            float journeyman = window.NodeButton(1, 0).Visual.Y + TechniqueTreeWindow.NodeHeight / 2f, master = window.NodeButton(2, 0).Visual.Y + TechniqueTreeWindow.NodeHeight / 2f;
            float lit = window.TrunkLit.Y + window.TrunkLit.Height;
            Assert.True(lit > journeyman && lit < master, $"The trunk is lit to level 7, between Journeyman ({journeyman}) and Master ({master}): {lit}.");

            Assert.Equal("Rich Veins", window.DetailTitleShown);
            Assert.Equal("In place of Patient Pick.", window.SponsorLineShown);
            Assert.Equal("Offer 40g", window.OfferButton.Text);
            Assert.True(window.OfferButton.IsEnabled);
            Assert.False(string.IsNullOrEmpty(window.NameShown));
            Assert.Equal("Guild member · trust 80 (a sponsor needs 50)", window.StandingShown);
            Assert.True(ui.Game.Play.Dashboard.CloseWindow() && !tree.IsOpen, "Esc closes it.");
        }

        [Fact]
        public void PickingATechniqueAndOfferingSponsorsIt()
        {
            var (ui, member, sim) = Start();
            var dashboard = ui.Game.Play.Dashboard;
            var tree = ui.Game.Session.DashboardModel.Techniques;
            var window = dashboard.Techniques;
            ui.Click(Centre(dashboard.Panels.RecruitButton));

            // Another skill: its tree, on the other technique at its top band reached
            ui.Click(Centre(window.SkillButton(Skill.Melee)));
            Assert.Equal(Skill.Melee, tree.SelectedSkill);
            Assert.Equal("Melee 4", window.RootShown);
            Assert.Equal(Technique.RecklessFury, tree.Selected);
            Assert.Null(window.SkillButton(Skill.Leadership)); // a skill with no techniques has no tree

            // A band not reached: read, not sponsored
            ui.Click(Centre(window.NodeButton(1, 0)));
            Assert.Equal(Technique.QuickBlade, tree.Selected);
            Assert.Equal("Quick Blade", window.DetailTitleShown);
            Assert.EndsWith("isn't Journeyman yet: Melee 4 of 6.", window.SponsorLineShown);
            Assert.False(window.OfferButton.IsEnabled);

            // The band's other: offered, taken up on the tick
            ui.Click(Centre(window.NodeButton(0, 1)));
            Assert.Equal("In place of Shield Wall.", window.SponsorLineShown);
            Assert.True(window.OfferButton.IsEnabled);
            uint treasury = sim.ActiveStalls[0].TreasuryGold;
            ui.Click(Centre(window.OfferButton));
            Assert.StartsWith("Offered ", window.FeedbackShown);
            Assert.EndsWith(" 20g to take up Reckless Fury.", window.FeedbackShown);
            Assert.False(window.OfferButton.IsEnabled, "One offer at a time.");
            ui.Ticks(1);
            ui.Frame(30);
            Assert.Equal(Technique.RecklessFury, member.Traits.TechniqueIn(Skill.Melee, SkillBand.Apprentice));
            Assert.Equal(treasury - 20, sim.ActiveStalls[0].TreasuryGold);
            Assert.EndsWith(" took up Reckless Fury for 20g.", window.FeedbackShown);
            Assert.Equal(TechniqueTreeModel.HeldTag, window.NodeTag(0, 1));
            Assert.Equal("20g", window.NodeTag(0, 0));
            Assert.True(window.NodeLit(0, 1) && !window.NodeLit(0, 0));
            Assert.EndsWith(" holds it.", window.SponsorLineShown);
        }

        [Fact]
        public void ARefusalSaysWhy()
        {
            var (ui, member, _) = Start();
            var dashboard = ui.Game.Play.Dashboard;
            var window = dashboard.Techniques;
            // Its trust under the line: nothing open, and the dossier and the tree say why
            member.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(10, 40, 0, 0, 1, 0));
            ui.Frame(30);
            Assert.Equal("Sponsor Rich Veins? Not yet: trust 40 of 50.", dashboard.Panels.RecruitShown);
            Assert.True(dashboard.Panels.RecruitButton.IsEnabled, "The tree still opens, to read.");
            ui.Click(Centre(dashboard.Panels.RecruitButton));
            Assert.Equal(TechniqueNodeState.Barred, ui.Game.Session.DashboardModel.Techniques.TierRows[1].Nodes[1].State);
            Assert.Equal("40g", window.NodeTag(1, 1));
            Assert.Equal("Not yet: trust 40 of 50.", window.SponsorLineShown);
            Assert.False(window.OfferButton.IsEnabled);
        }

        [Fact]
        public void AResidentsTreeIsReadOnly()
        {
            var (ui, member, _) = Start(sign: false);
            var dashboard = ui.Game.Play.Dashboard;
            var window = dashboard.Techniques;
            Assert.Equal("Recruit", dashboard.Panels.SwayButtonShown);
            Assert.EndsWith("\n" + DossierModel.TreeHint, ui.Game.Session.DashboardModel.Dossier.TechniquesTip);
            ui.Click(Centre(dashboard.Panels.TechniquesLine));
            Assert.True(window.Window.IsShown, "The techniques line opens anyone's tree.");
            Assert.EndsWith(" · only your guild members and artisans take a patron's gold", window.StandingShown);
            Assert.EndsWith("isn't one of yours: only guild members and artisans take a patron's gold.", window.SponsorLineShown);
            Assert.False(window.OfferButton.IsEnabled);
            Assert.Equal(Technique.SeamStudent, member.Traits.TechniqueIn(Skill.Mining, SkillBand.Apprentice));
        }

        /// <summary>Every technique's words fit its node, and the detail's its column, for every skill; the window fits the smallest window.</summary>
        [Fact]
        public void EveryTechniqueFitsItsNode()
        {
            var (ui, _, _) = Start();
            var dashboard = ui.Game.Play.Dashboard;
            var window = dashboard.Techniques;
            ui.Click(Centre(dashboard.Panels.RecruitButton));
            var frame = window.Window.Frame;
            Assert.True(frame.AbsoluteLeft >= 0 && frame.AbsoluteTop >= HeaderBarHeight && frame.AbsoluteLeft + frame.AbsoluteWidth <= Width && frame.AbsoluteTop + frame.AbsoluteHeight <= Height,
                $"The window fits {Width}x{Height}: {frame.AbsoluteLeft},{frame.AbsoluteTop} {frame.AbsoluteWidth}x{frame.AbsoluteHeight}.");
            int nodes = 0;
            foreach (var skill in TechniqueTreeModel.TreeSkills)
            {
                ui.Click(Centre(window.SkillButton(skill)));
                for (int t = 0; t < TechniqueTreeModel.Tiers; t++)
                {
                    for (int n = 0; n < TechniqueTreeModel.NodesPerTier; n++)
                    {
                        var node = window.NodeButton(t, n).Visual;
                        float bottom = node.AbsoluteTop + node.AbsoluteHeight, right = node.AbsoluteLeft + node.AbsoluteWidth;
                        foreach (var effect in window.NodeEffects(t, n))
                            Assert.True(effect.AbsoluteTop + effect.AbsoluteHeight <= bottom - 2f && effect.AbsoluteLeft + effect.AbsoluteWidth <= right,
                                $"{skill} tier {t}: \"{effect.Text}\" fits {window.NodeName(t, n)}'s node.");
                        ui.Click(Centre(window.NodeButton(t, n)));
                        nodes++;
                        var column = window.DetailTexts.First().Parent as GraphicalUiElement;
                        var pane = column.Parent as GraphicalUiElement;
                        foreach (var text in window.DetailTexts.Where(x => x.Visible && x.Text.Length > 0))
                            Assert.True(text.AbsoluteTop + text.AbsoluteHeight <= pane.AbsoluteTop + pane.AbsoluteHeight, $"{window.DetailTitleShown}'s detail fits its column: \"{text.Text}\".");
                        var offer = window.OfferButton.Visual;
                        Assert.True(offer.AbsoluteTop + offer.AbsoluteHeight <= pane.AbsoluteTop + pane.AbsoluteHeight, $"{window.DetailTitleShown}'s Offer fits its column.");
                    }
                }
            }
            Assert.Equal(TechniqueTable.All.Count, nodes);
        }

        [Fact]
        public void TheTreeLooksAsRecorded()
        {
            var (ui, _, _) = Start();
            ui.Click(Centre(ui.Game.Play.Dashboard.Panels.RecruitButton));
            ui.MoveTo(new Vec2(4f, Height - 4f)); // the pointer off the window
            RenderTests.CompareWithReference("render-outpost-technique-tree", FrameCapture.Capture(ui.Game, Width, Height), Width, Height);
        }

        private const float HeaderBarHeight = 52f;

        // A resident with Melee 4 (Shield Wall at Apprentice) and Mining 7 (Seam Student, Patient Pick), shown in the dossier;
        // signed on to the guild unless told not
        private static (UiDriver Ui, AgentState Member, SimulationManager Sim) Start(bool sign = true)
        {
            var ui = UiDriver.Start(new ClientOptions
            {
                ConfigureServices = services =>
                {
                    services.RemoveAll<ISessionDefinition>();
                    services.AddSingleton<ISessionDefinition>(new OutpostSession(OutpostSession.DefaultCrawlerCount, EconomyTuning.Default.WithStallTreasury(1000)));
                },
            }, width: Width, height: Height);
            var game = ui.Game;
            var sim = (SimulationManager)game.Session.Simulation.Simulation;
            var member = game.Session.Simulation.Host.CrawlerStates.First(s => !s.IsDrifter);
            var skills = default(SkillSet);
            skills.Set(Skill.Melee, 4);
            skills.Set(Skill.Mining, 7);
            member.Skills = skills;
            var traits = default(CrawlerTraits);
            traits.SetTechnique(Skill.Melee, SkillBand.Apprentice, Technique.ShieldWall);
            traits.SetTechnique(Skill.Mining, SkillBand.Apprentice, Technique.SeamStudent);
            traits.SetTechnique(Skill.Mining, SkillBand.Journeyman, Technique.PatientPick);
            member.Traits = traits;
            member.Personality = new Simpiens.Cognition.Contracts.AgentPersonality(0f, 0.2f, 0f, 0f, 0f);
            member.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(60, 80, 0, 0, 1, 0));
            if (sign) Assert.Equal(GuildRules.Check.Ok, sim.Guild.Recruit(sim, member.AgentId, 30, 1));
            game.Session.DashboardModel.SelectCrawler(member.AgentId);
            ui.Frame(30);
            return (ui, member, sim);
        }

        private static Vec2 Centre(GraphicalUiElement control)
            => new Vec2(control.AbsoluteLeft + control.AbsoluteWidth * 0.5f, control.AbsoluteTop + control.AbsoluteHeight * 0.5f);

        private static Vec2 Centre(Gum.Forms.Controls.FrameworkElement control) => Centre(control.Visual);
    }
}
