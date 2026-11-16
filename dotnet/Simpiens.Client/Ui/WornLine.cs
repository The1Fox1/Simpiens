using System.Collections.Generic;
using Gum.DataTypes;
using Gum.Wireframe;
using Gum.GueDeriving;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation.Items;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// What a crawler wears, on one line, laid out from the slot table (Epic 10 Part 3, K1.5): "Gear:", then for each slot
    /// in display order that holds a piece, its icon at 1x and its words; "Gear: None" with nothing worn. An entry per slot,
    /// made the first time the table shows it, so a new slot needs no code here. A hidden entry takes no room in the row.
    /// The dossier and the guild's cards use it.
    /// </summary>
    public sealed class WornLine
    {
        private readonly IconAtlas _icons;
        private readonly string _label;
        private readonly TextRuntime _labelText, _suffix;
        private readonly List<Entry> _entries = new List<Entry>(EquipmentSlots.Capacity);

        private sealed class Entry
        {
            public EquipmentSlot Slot;
            public ContainerRuntime Gap;
            public SpriteRuntime Icon;
            public TextRuntime Text;
        }

        public WornLine(IconAtlas icons, string label = "Gear:")
        {
            _icons = icons;
            _label = label;
            Visual = UiParts.Row(4f);
            _labelText = UiParts.Text(label, UiStyle.TextSecondary, UiText.Small);
            UiParts.CentreVertically(_labelText);
            Visual.Children.Add(_labelText);
            _suffix = UiParts.Text(string.Empty, UiStyle.TextSecondary, UiText.Small);
            _suffix.Visible = false;
            UiParts.CentreVertically(_suffix);
            Visual.Children.Add(_suffix);
        }

        /// <summary>The row, to place in a panel.</summary>
        public ContainerRuntime Visual { get; }

        /// <summary>
        /// Shows <paramref name="pieces"/>: each worn piece's short words ("Hard Hat 64%"), or only what's left of it
        /// (<paramref name="brief"/>: "64%", where the icon names it); then <paramref name="suffix"/> ("· Bunk 3"), after the row's spacing.
        /// </summary>
        public void Show(WornPieces pieces, bool brief = false, string suffix = "")
        {
            var slots = pieces.Slots;
            while (_entries.Count < slots.Count) Add();
            UiParts.SetText(_labelText, pieces.NoneWorn ? _label + " " + DossierModel.NoGear : _label);
            bool first = true;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                bool worn = i < slots.Count && slots[i].IsWorn;
                if (i < slots.Count) entry.Slot = slots[i].Slot.Id;
                if (entry.Text.Visible != worn) entry.Text.Visible = worn;
                bool gap = worn && !first;
                if (entry.Gap.Visible != gap) entry.Gap.Visible = gap;
                UiParts.SetIcon(entry.Icon, _icons, worn ? slots[i].Icon : null);
                if (worn)
                {
                    UiParts.SetText(entry.Text, brief ? slots[i].Left : slots[i].Short);
                    var colour = UiStyle.RarityText(slots[i].Rarity, UiStyle.TextSecondary); // its rarity's, as its icon's rim (K4.5b)
                    if (entry.Text.Color != colour) entry.Text.Color = colour;
                    first = false;
                }
            }
            bool hasSuffix = !string.IsNullOrEmpty(suffix);
            if (_suffix.Visible != hasSuffix) _suffix.Visible = hasSuffix;
            if (hasSuffix) UiParts.SetText(_suffix, suffix);
        }

        /// <summary>The words shown, entries joined by a space ("Gear: Hard Hat 64% Hazard Gear 80%"): what tests read.</summary>
        public string Shown
        {
            get
            {
                var text = new System.Text.StringBuilder(_labelText.Text);
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].Text.Visible) text.Append(' ').Append(_entries[i].Text.Text);
                }
                if (_suffix.Visible) text.Append(' ').Append(_suffix.Text);
                return text.ToString();
            }
        }

        /// <summary>The icon of <paramref name="slot"/>'s entry; null when the line has none for it yet.</summary>
        public SpriteRuntime IconFor(EquipmentSlot slot)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Slot == slot) return _entries[i].Icon;
            }
            return null;
        }

        private void Add()
        {
            var entry = new Entry
            {
                Gap = new ContainerRuntime { Width = 4f, Height = 1f, Visible = false },
                Icon = UiParts.Icon(_icons, null, scale: 1),
                Text = UiParts.Text(string.Empty, UiStyle.TextSecondary, UiText.Small),
            };
            entry.Icon.Visible = false;
            entry.Text.Visible = false;
            UiParts.CentreVertically(entry.Text);
            // Before the suffix, which stays last
            int at = Visual.Children.IndexOf(_suffix);
            Visual.Children.Insert(at, entry.Gap);
            Visual.Children.Insert(at + 1, entry.Icon);
            Visual.Children.Insert(at + 2, entry.Text);
            _entries.Add(entry);
        }
    }
}
