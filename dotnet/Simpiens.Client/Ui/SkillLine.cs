using System.Collections.Generic;
using Gum.Wireframe;
using Gum.GueDeriving;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation.Skills;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// A crawler's skills on one line (Epic 11, S1.6): an optional label ("Skills:"), then each listed skill's words and,
    /// for a skill it loves, the passion's flame from the UI kit (one for keen, in lantern amber; two for burning, in ember).
    /// An entry per skill shown, made the first time a list needs it; a hidden entry takes no room in the row. The dossier and
    /// the guild's cards use it.
    /// </summary>
    public sealed class SkillLine
    {
        private readonly TextRuntime _labelText;
        private readonly string _label;
        private readonly List<Entry> _entries = new List<Entry>(SkillTable.Capacity);

        private sealed class Entry
        {
            public TextRuntime Text;
            public SpriteRuntime Flame;
            public Passion Passion;
        }

        public SkillLine(string label = "Skills:")
        {
            _label = label;
            Visual = UiParts.Row(4f);
            _labelText = UiParts.Text(label ?? string.Empty, UiStyle.TextSecondary, UiText.Small);
            _labelText.Visible = !string.IsNullOrEmpty(label);
            UiParts.CentreVertically(_labelText);
            Visual.Children.Add(_labelText);
        }

        /// <summary>The row, to place in a panel.</summary>
        public ContainerRuntime Visual { get; }

        /// <summary>Shows <paramref name="skills"/>; with none, the row shows nothing (the label hides too).</summary>
        public void Show(IReadOnlyList<SkillTexts.Shown> skills)
        {
            while (_entries.Count < skills.Count) Add();
            bool any = skills.Count > 0;
            bool label = any && !string.IsNullOrEmpty(_label);
            if (_labelText.Visible != label) _labelText.Visible = label;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                bool shown = i < skills.Count;
                if (entry.Text.Visible != shown) entry.Text.Visible = shown;
                var passion = shown ? skills[i].Passion : Passion.None;
                bool flame = shown && passion != Passion.None;
                if (shown) UiParts.SetText(entry.Text, i > 0 ? "· " + skills[i].Text : skills[i].Text);
                if (flame && entry.Passion != passion)
                {
                    UiParts.SetGlyph(entry.Flame, skills[i].Glyph, scale: 1);
                    entry.Flame.Color = passion == Passion.Burning ? UiStyle.Danger : UiStyle.Accent;
                }
                entry.Passion = flame ? passion : Passion.None;
                if (entry.Flame.Visible != flame) entry.Flame.Visible = flame;
            }
        }

        /// <summary>The words shown ("Skills: Smithing 5 (burning) · Melee 2"), a passion as "(keen)" or "(burning)": what tests read.</summary>
        public string Text
        {
            get
            {
                var text = new System.Text.StringBuilder(_labelText.Visible ? _labelText.Text : string.Empty);
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (!_entries[i].Text.Visible) continue;
                    if (text.Length > 0) text.Append(' ');
                    text.Append(_entries[i].Text.Text);
                    if (_entries[i].Flame.Visible) text.Append(" (").Append(PassionTable.Get(_entries[i].Passion).DisplayName).Append(')');
                }
                return text.ToString();
            }
        }

        private void Add()
        {
            var entry = new Entry
            {
                Text = UiParts.Text(string.Empty, UiStyle.TextSecondary, UiText.Small),
                Flame = UiParts.Glyph(Simpiens.Presentation.Art.UiKit.PassionKeen, UiStyle.Accent, scale: 1),
            };
            entry.Text.Visible = false;
            entry.Flame.Visible = false;
            UiParts.CentreVertically(entry.Text);
            UiParts.CentreVertically(entry.Flame);
            Visual.Children.Add(entry.Text);
            Visual.Children.Add(entry.Flame);
            _entries.Add(entry);
        }
    }
}
