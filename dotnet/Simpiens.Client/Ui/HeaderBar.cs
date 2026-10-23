using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Microsoft.Xna.Framework;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;
using Simpiens.Presentation.Dashboard;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The header across the top of the window (Unity's header bar, 52 px), over the map and the panels alike: the
    /// title and status, the outpost's numbers, the windows' buttons, quick save and load, the tick, pause and speed,
    /// and the Tab switch. It takes clicks, so the map ignores the pointer over it.
    /// </summary>
    public sealed class HeaderBar
    {
        public const float Height = 52f;

        private static readonly Color Gold = UiParts.Hex(0xf59e0b), GoldText = UiParts.Hex(0xfbbf24);
        private static readonly Color Stock = UiParts.Hex(0x3b82f6), StockText = UiParts.Hex(0x93c5fd);
        private static readonly Color Bounty = UiParts.Hex(0xa855f7), BountyText = UiParts.Hex(0xc084fc);
        private static readonly Color Metric = UiParts.Hex(0x94a3b8);
        private static readonly Color Operational = UiParts.Hex(0x34d399);

        private readonly TextRuntime _treasury, _rations, _salvage, _bounties, _saveStatus, _tick;

        public HeaderBar()
        {
            Root = new ContainerRuntime
            {
                Name = "header-bar",
                WidthUnits = DimensionUnitType.RelativeToParent,
                Width = 0f,
                Height = Height,
                HasEvents = true, // the bar itself takes clicks: none reach the map
            };
            UiParts.AddFill(Root, UiStyle.Header);
            var rule = new ColoredRectangleRuntime { Color = UiStyle.Border, WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 1f, YUnits = GeneralUnitType.PixelsFromLarge, YOrigin = VerticalAlignment.Bottom };
            Root.Children.Add(rule);

            var left = UiParts.Row(10f);
            left.X = 16f;
            left.Children.Add(UiParts.Text(HeaderModel.Title, UiStyle.Accent, UiText.Title));
            left.Children.Add(UiParts.Pill(out _, "OPERATIONAL", UiParts.Alpha(UiStyle.Success, 0.15f), UiStyle.Success, Operational));
            Root.Children.Add(left);

            // The outpost's numbers follow the status (Unity centred them between the groups; at 1280 px there is no room to)
            var centre = UiParts.Row(6f);
            left.Children.Add(new ContainerRuntime { Width = 10f, Height = 1f });
            left.Children.Add(centre);
            centre.Children.Add(UiParts.Pill(out _treasury, "0 GOLD", UiParts.Alpha(Gold, 0.15f), Gold, GoldText));
            centre.Children.Add(UiParts.Pill(out _rations, "0 RATIONS", UiParts.Alpha(Stock, 0.15f), Stock, StockText));
            centre.Children.Add(UiParts.Pill(out _salvage, "0 ORE | 0 HERB", UiParts.Alpha(Stock, 0.15f), Stock, StockText));
            centre.Children.Add(UiParts.Pill(out _bounties, "0 BOUNTIES", UiParts.Alpha(Bounty, 0.15f), Bounty, BountyText));

            var right = UiParts.Row(4f); // 4 px apart: pause and speed took the room the speed's text left (1280 px)
            right.XUnits = GeneralUnitType.PixelsFromLarge;
            right.XOrigin = HorizontalAlignment.Right;
            right.X = -16f;
            MerchantButton = UiParts.Button("MERCHANT", 78f);
            BountiesButton = UiParts.Button("BOUNTIES", 78f);
            SaveButton = UiParts.Button("SAVE", 48f);
            LoadButton = UiParts.Button("LOAD", 48f);
            right.Children.Add(MerchantButton.Visual);
            right.Children.Add(BountiesButton.Visual);
            right.Children.Add(SaveButton.Visual);
            right.Children.Add(LoadButton.Visual);
            _tick = UiParts.Text("TICK: 00000", Metric, UiText.SmallBold);
            UiParts.CentreVertically(_tick);
            right.Children.Add(_tick);
            PauseButton = UiParts.Button("PAUSE", 52f);
            SpeedButton = UiParts.Button("1x", 36f);
            right.Children.Add(PauseButton.Visual);
            right.Children.Add(SpeedButton.Visual);
            TabButton = UiParts.Button("[TAB] MAP VIEW", 120f);
            right.Children.Add(TabButton.Visual);
            Root.Children.Add(right);

            // The save's message, under everything at the bottom left (Unity had it in the header; at 1280 px it doesn't fit there)
            Status = UiParts.Pill(out _saveStatus, string.Empty, UiStyle.Header, UiStyle.Border, Metric, UiText.Small);
            Status.X = 16f;
            Status.YUnits = GeneralUnitType.PixelsFromLarge;
            Status.YOrigin = VerticalAlignment.Bottom;
            Status.Y = -20f;

            ShowSaveButtons(false);
        }

        public ContainerRuntime Root { get; }

        /// <summary>The save status's own root (added to the UI after the panels).</summary>
        public ContainerRuntime Status { get; }
        public Button MerchantButton { get; }
        public Button BountiesButton { get; }
        public Button SaveButton { get; }
        public Button LoadButton { get; }

        /// <summary>Pauses or resumes the world (P).</summary>
        public Button PauseButton { get; }

        /// <summary>The world's speed: a click moves to the next (1 to 4 pick one).</summary>
        public Button SpeedButton { get; }
        public Button TabButton { get; }

        public string TreasuryText => _treasury.Text;
        public string TickText => _tick.Text;
        public string SaveStatusText => _saveStatus.Text;

        /// <summary>Save and Load show only when there is a save slot (Unity hid them without one).</summary>
        public void ShowSaveButtons(bool show)
        {
            SaveButton.IsVisible = show;
            LoadButton.IsVisible = show;
            Status.Visible = show && !string.IsNullOrEmpty(_saveStatus.Text);
        }

        public void SetSaveStatus(string message) => _saveStatus.Text = message ?? string.Empty;

        public void Bind(DashboardModel model)
        {
            var header = model.Header;
            UiParts.SetText(_treasury, header.Treasury);
            UiParts.SetText(_rations, header.Rations);
            UiParts.SetText(_salvage, header.Salvage);
            UiParts.SetText(_bounties, header.Bounties);
            UiParts.SetText(_tick, header.Tick);
            if (PauseButton.Text != header.PauseLabel) PauseButton.Text = header.PauseLabel;
            if (SpeedButton.Text != header.Speed) SpeedButton.Text = header.Speed;
            PauseButton.IsVisible = header.CanControlSpeed;
            SpeedButton.IsVisible = header.CanControlSpeed;
            TabButton.Text = model.PanelsVisible ? "[TAB] MAP VIEW" : "[TAB] DASHBOARD";
            UiParts.SetText(_saveStatus, model.Saves.Status);
            ShowSaveButtons(model.Saves.Available);
        }
    }
}
