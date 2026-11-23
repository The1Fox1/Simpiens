using System.Collections.Generic;
using Simpiens.Cognition.Inventory;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>One slot's piece as the panels show it (Epic 10 Part 3, K1.5): a row of <see cref="WornPieces"/>.</summary>
    public sealed class WornPiece
    {
        private ItemType _item;
        private byte _grade;
        private int _left = -1;

        internal WornPiece(SlotDefinition slot) => Slot = slot;

        /// <summary>The slot, from the table: its name, kind and place.</summary>
        public SlotDefinition Slot { get; }

        public bool IsWorn { get; private set; }

        /// <summary>"Hard Hat, 64% left" (<see cref="DossierModel.GearText"/>); empty with none.</summary>
        public string Text { get; private set; } = string.Empty;

        /// <summary>"Hard Hat 64%": what a line of pieces shows; empty with none.</summary>
        public string Short { get; private set; } = string.Empty;

        /// <summary>"64%": what's left of its life; empty with none.</summary>
        public string Left { get; private set; } = string.Empty;

        /// <summary>Its icon key (graphics V4); null with none.</summary>
        public string Icon { get; private set; }

        /// <summary>Its rarity as its icon's rim shows it, raised by its grade (Epic 10 Part 3, K4.5b): what colours its name; common with none.</summary>
        public ItemRarity Rarity { get; private set; }

        /// <summary>The masterpiece it is (Epic 11, S8.5, D45), or 0.</summary>
        public ushort Masterpiece { get; private set; }

        /// <summary>A masterpiece's story, for its tip (<see cref="MasterpieceTexts.StoryOf"/>); empty for any other piece.</summary>
        public string Story { get; private set; } = string.Empty;

        // Words are rebuilt only when the piece, its grade or what's left of it changes; a masterpiece's story each time, as
        // its owners and kills move
        internal void Show(in ItemInstance piece, DashboardContext context = null)
        {
            IsWorn = !piece.IsEmpty;
            if (!IsWorn)
            {
                _item = ItemType.None;
                _left = -1;
                Text = Short = Left = Story = string.Empty;
                Icon = null;
                Rarity = ItemRarity.Common;
                Masterpiece = 0;
                return;
            }
            Story = piece.IsMasterpiece ? MasterpieceTexts.StoryOf(context, piece.Masterpiece) : string.Empty;
            int left = DossierModel.LifeLeft(piece);
            if (piece.Type == _item && piece.Quality == _grade && left == _left && piece.Masterpiece == Masterpiece) return;
            _item = piece.Type;
            _grade = piece.Quality;
            _left = left;
            Masterpiece = piece.Masterpiece;
            string masterpiece = piece.IsMasterpiece ? MasterpieceTexts.NameOf(context, piece.Masterpiece) : string.Empty;
            string name = masterpiece.Length > 0 ? masterpiece : DossierModel.PieceName(piece);
            Text = Invariant($"{name}, {left}% left");
            Short = Invariant($"{name} {left}%");
            Left = Invariant($"{left}%");
            Icon = ItemIcons.Of(piece.Type, piece.Quality);
            Rarity = Grades.RimOf(ItemCatalog.Get(piece.Type)?.Rarity ?? ItemRarity.Common, piece.Quality);
        }
    }

    /// <summary>
    /// What a crawler wears, a row for each slot in the table's display order (Epic 10 Part 3, K1.5; <see cref="EquipmentSlots.InDisplayOrder"/>):
    /// the dossier's and the guild's equipment panels are laid out from it, so a new slot shows with no code of its own.
    /// </summary>
    public sealed class WornPieces
    {
        private readonly List<WornPiece> _slots = new List<WornPiece>(EquipmentSlots.Capacity);

        /// <summary>A row per slot, in display order; rows for empty slots say so.</summary>
        public IReadOnlyList<WornPiece> Slots => _slots;

        /// <summary>How many slots hold a piece.</summary>
        public int Count { get; private set; }

        public bool NoneWorn => Count == 0;

        /// <summary>The stories of the masterpieces worn, a blank line between (Epic 11, S8.5): the gear line's tip; empty for none.</summary>
        public string Stories { get; private set; } = string.Empty;

        /// <summary>The pieces worn, in display order, in short: "Respirator 100%, Hazard Gear 80%"; <see cref="DossierModel.NoGear"/> with none. Allocates.</summary>
        public string Summary
        {
            get
            {
                if (Count == 0) return DossierModel.NoGear;
                var text = new System.Text.StringBuilder();
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (!_slots[i].IsWorn) continue;
                    text.Append(text.Length == 0 ? string.Empty : ", ").Append(_slots[i].Short);
                }
                return text.ToString();
            }
        }

        /// <summary>
        /// Reads what <paramref name="inventory"/>'s wearer wears; nothing for none. The rows follow the table, a test's extended
        /// one included. With <paramref name="context"/>, a masterpiece shows by its name, with its story (Epic 11, S8.5).
        /// </summary>
        public void Refresh(IAgentInventory inventory, DashboardContext context = null)
        {
            var shown = EquipmentSlots.InDisplayOrder;
            if (_slots.Count != shown.Count || (_slots.Count > 0 && _slots[0].Slot != shown[0]))
            {
                _slots.Clear();
                for (int i = 0; i < shown.Count; i++) _slots.Add(new WornPiece(shown[i]));
            }
            Count = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                var piece = inventory != null ? inventory.GetEquipped(_slots[i].Slot.Id) : ItemInstance.Empty;
                _slots[i].Show(piece, context);
                if (_slots[i].IsWorn) Count++;
            }
            string stories = string.Empty;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Story.Length == 0) continue;
                stories = stories.Length == 0 ? _slots[i].Story : stories + "\n\n" + _slots[i].Story;
            }
            if (!string.Equals(stories, Stories, System.StringComparison.Ordinal)) Stories = stories;
        }

        /// <summary>The <see cref="Summary"/> of what <paramref name="inventory"/>'s wearer wears, at once (allocates).</summary>
        public static string Describe(IAgentInventory inventory)
        {
            var pieces = new WornPieces();
            pieces.Refresh(inventory);
            return pieces.Summary;
        }
    }
}
