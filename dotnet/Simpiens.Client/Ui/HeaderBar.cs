using System.Collections.Generic;
using Gum.Converters;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.GueDeriving;
using Gum.Managers;
using RenderingLibrary.Graphics;
using Simpiens.Presentation.Art;
using Simpiens.Presentation.Dashboard;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The header across the top of the window, over the map and the panels alike: a lintel of the outpost's stone in
    /// five clusters (the UI polish plan, U4), left to right: who and where; the outpost (gold, stock, bounties) in one
    /// plaque; the windows (stall, bounties, guild), lit while open; then, from the right, the menu (save, load, key help),
    /// the Map | Ledger switch, and time (the clock, pause, the speed pips) in a plaque. It takes clicks, so the map
    /// ignores the pointer over it. The menu also picks the window's size, or full screen.
    /// </summary>
    public sealed class HeaderBar
    {
        public const float Height = 52f;

        /// <summary>How tall the clusters' plaques and buttons are.</summary>
        private const float Control = 30f;

        /// <summary>The menu's size pips, three in a row, and so the width of its controls.</summary>
        private const float SizePip = 78f, PipGap = 4f, MenuWidth = 3 * SizePip + 2 * PipGap;

        private static readonly Microsoft.Xna.Framework.Color Lamplight = UiStyle.Hex(0x5a3c1a); // a thread of light on the lintel's lower edge

        private readonly TextRuntime _place, _gold, _bounties, _saveStatus, _tick, _confirmText, _mapLabel, _ledgerLabel;
        private readonly SpriteRuntime _merchantGlyph, _bountiesGlyph, _guildGlyph, _pauseGlyph, _clockGlyph, _coin;
        private static readonly Microsoft.Xna.Framework.Color CoinPulse = UiStyle.Hex(0xffd28a);
        private long _goldTarget = -1;
        private float _goldFrom, _goldRoll = -1f;
        private readonly RectangleRuntime _viewLamp;
        private readonly IconAtlas _icons;
        private readonly ContainerRuntime _stock, _confirm;
        private readonly List<(SpriteRuntime Icon, TextRuntime Count)> _stockEntries = new List<(SpriteRuntime, TextRuntime)>(4);
        private readonly List<ContainerRuntime> _stockBoxes = new List<ContainerRuntime>(4);
        private int _windowWidth, _windowHeight;
        private bool _fullScreen;

        public HeaderBar(IconAtlas icons)
        {
            _icons = icons;
            Root = new ContainerRuntime
            {
                Name = "header-bar",
                WidthUnits = DimensionUnitType.RelativeToParent,
                Width = 0f,
                Height = Height,
                HasEvents = true, // the bar itself takes clicks: none reach the map
            };
            // The lintel: stone, lit along its top, shadowed and lamplit along its foot
            UiParts.AddFill(Root, UiStyle.Header);
            Root.Children.Add(Line(UiStyle.Hex(0x4c4138), 0f, fromBottom: false));
            Root.Children.Add(Line(UiStyle.Hex(0x0d0b09), -1f, fromBottom: true));
            Root.Children.Add(Line(Lamplight, 0f, fromBottom: true));

            var left = UiParts.Row(12f);
            left.X = 16f;
            Root.Children.Add(left);

            // Who and where
            var identity = UiParts.Row(8f);
            identity.Children.Add(UiParts.Glyph(UiKit.Lantern, UiStyle.Accent));
            var names = UiParts.Column(1f);
            UiParts.CentreVertically(names);
            names.Children.Add(UiParts.Text(HeaderModel.Title, UiStyle.Accent, UiText.Heading));
            _place = UiParts.Text(HeaderModel.OutpostPlace, UiStyle.TextSecondary, UiText.Small);
            names.Children.Add(_place);
            identity.Children.Add(names);
            left.Children.Add(identity);

            // The outpost: gold, the stall's stock, the bounties, in one plaque; each figure takes hover for its tip
            left.Children.Add(UiParts.Plaque(out var outpost, Control, padding: 10f, spacing: 10f));
            GoldCounter = Counter(UiKit.Coin, UiStyle.Accent, UiStyle.GoldText, out _gold, out _coin);
            outpost.Children.Add(GoldCounter);
            outpost.Children.Add(Divider());
            _stock = UiParts.Row(10f);
            outpost.Children.Add(_stock);
            outpost.Children.Add(Divider());
            BountiesCounter = Counter(UiKit.Notice, UiStyle.Bounty, UiStyle.Text, out _bounties);
            outpost.Children.Add(BountiesCounter);

            // The windows
            var windows = UiParts.Row(6f);
            MerchantButton = UiParts.GlyphButton("Stall", UiKit.Stall, 80f, out _merchantGlyph, Control);
            BountiesButton = UiParts.GlyphButton("Bounties", UiKit.Notice, 100f, out _bountiesGlyph, Control);
            GuildButton = UiParts.GlyphButton("Guild 0/4", UiKit.Shield, 104f, out _guildGlyph, Control); // Epic 10 G4, D6
            windows.Children.Add(MerchantButton.Visual);
            windows.Children.Add(BountiesButton.Visual);
            windows.Children.Add(GuildButton.Visual);
            left.Children.Add(windows);

            var right = UiParts.Row(12f);
            right.XUnits = GeneralUnitType.PixelsFromLarge;
            right.XOrigin = HorizontalAlignment.Right;
            right.X = -16f;
            Root.Children.Add(right);

            // Time: the clock, pause, and a pip per speed
            right.Children.Add(UiParts.Plaque(out var time, Control, padding: 8f, spacing: 6f));
            _clockGlyph = UiParts.Glyph(UiKit.Sun, UiStyle.Accent);
            UiParts.CentreVertically(_clockGlyph);
            time.Children.Add(_clockGlyph);
            _tick = UiParts.Text("Day 1 · 06:00", UiStyle.Text, UiText.BodyBold);
            UiParts.CentreVertically(_tick);
            time.Children.Add(_tick);
            time.Children.Add(new ContainerRuntime { Width = 2f, Height = 1f });
            PauseButton = UiParts.GlyphButton(string.Empty, UiKit.Pause, 26f, out _pauseGlyph, 22f);
            _pauseGlyph.Color = UiStyle.Text;
            time.Children.Add(PauseButton.Visual);
            var pips = UiParts.Row(2f);
            var speedButtons = new Button[HeaderModel.Speeds.Length];
            for (int i = 0; i < speedButtons.Length; i++)
            {
                speedButtons[i] = UiParts.Button(HeaderModel.SpeedText(i), i == speedButtons.Length - 1 ? 30f : 24f, 22f, UiText.Badge);
                pips.Children.Add(speedButtons[i].Visual);
            }
            SpeedButtons = speedButtons;
            time.Children.Add(pips);

            // The view switch: Map | Ledger, the one shown lit
            TabButton = UiParts.Button(string.Empty, 132f, Control);
            _mapLabel = Half("Map", 33f);
            _ledgerLabel = Half("Ledger", 99f);
            _viewLamp = new RectangleRuntime { IsFilled = true, StrokeWidth = 0, FillColor = UiStyle.Accent, Width = 44f, Height = 2f, XOrigin = HorizontalAlignment.Center, YUnits = GeneralUnitType.PixelsFromLarge, YOrigin = VerticalAlignment.Bottom, Y = -5f, X = 33f };
            TabButton.Visual.Children.Add(_mapLabel);
            TabButton.Visual.Children.Add(_ledgerLabel);
            TabButton.Visual.Children.Add(_viewLamp);
            right.Children.Add(TabButton.Visual);

            MenuButton = UiParts.GlyphButton(string.Empty, UiKit.Menu, 36f, out var menuGlyph, Control);
            menuGlyph.Color = UiStyle.Text;
            right.Children.Add(MenuButton.Visual);

            // The menu: under its button, over everything else; Load asks first
            Menu = new ContainerRuntime
            {
                Name = "header-menu",
                Width = MenuWidth + 28f,
                HeightUnits = DimensionUnitType.RelativeToChildren,
                Height = 14f,
                XUnits = GeneralUnitType.PixelsFromLarge,
                XOrigin = HorizontalAlignment.Right,
                X = -16f,
                Y = Height + 2f,
                HasEvents = true,
                Visible = false,
            };
            UiParts.AddKitFill(Menu, UiKit.FrameIron, UiKit.FrameCorner, UiStyle.Panel);
            var items = UiParts.Column(6f);
            items.X = 14f;
            items.Y = 14f;
            Menu.Children.Add(items);
            SaveButton = UiParts.Button("Save the outpost", MenuWidth, Control, UiText.StatusBold);
            LoadButton = UiParts.Button("Load the quick-save", MenuWidth, Control, UiText.StatusBold);
            KeyHelpButton = UiParts.Button("Key help (F1)", MenuWidth, Control, UiText.StatusBold);
            items.Children.Add(SaveButton.Visual);
            items.Children.Add(LoadButton.Visual);
            items.Children.Add(KeyHelpButton.Visual);
            _confirm = UiParts.Column(6f);
            _confirm.Visible = false;
            _confirmText = UiParts.Text("Load the quick-save? Unsaved progress is lost.", UiStyle.TextLight, UiText.Small);
            _confirmText.WidthUnits = DimensionUnitType.Absolute;
            _confirmText.Width = MenuWidth;
            _confirm.Children.Add(_confirmText);
            var answers = MenuRow(6f);
            ConfirmLoadButton = UiParts.PrimaryButton("Load", (MenuWidth - 6f) / 2f, Control, UiText.StatusBold);
            CancelLoadButton = UiParts.Button("Cancel", (MenuWidth - 6f) / 2f, Control, UiText.StatusBold);
            answers.Children.Add(ConfirmLoadButton.Visual);
            answers.Children.Add(CancelLoadButton.Visual);
            _confirm.Children.Add(answers);
            items.Children.Add(_confirm);

            // The window's size: a pip per size, the window's own lit, and full screen under them
            items.Children.Add(new RectangleRuntime { IsFilled = true, StrokeWidth = 0, FillColor = UiStyle.Hex(0x4c4138), Width = MenuWidth, Height = 1f });
            items.Children.Add(Window.SubHeader("WINDOW SIZE"));
            var sizes = MenuRow(PipGap);
            var sizeButtons = new Button[WindowSizes.All.Length];
            for (int i = 0; i < sizeButtons.Length; i++)
            {
                sizeButtons[i] = UiParts.Button(WindowSizes.Text(WindowSizes.All[i]), SizePip, 26f, UiText.SmallBold);
                sizes.Children.Add(sizeButtons[i].Visual);
            }
            WindowSizeButtons = sizeButtons;
            items.Children.Add(sizes);
            FullScreenButton = UiParts.Button("Full screen", MenuWidth, 26f, UiText.SmallBold);
            items.Children.Add(FullScreenButton.Visual);

            // The save's message, under everything at the bottom left (Unity had it in the header; at 1280 px it doesn't fit there)
            Status = UiParts.Pill(out _saveStatus, string.Empty, UiStyle.Header, UiStyle.Border, UiStyle.TextSecondary, UiText.Small);
            Status.X = 16f;
            Status.YUnits = GeneralUnitType.PixelsFromLarge;
            Status.YOrigin = VerticalAlignment.Bottom;
            Status.Y = -20f;

            ShowSaveButtons(false);
        }

        public ContainerRuntime Root { get; }

        /// <summary>The save status's own root (added to the UI after the panels).</summary>
        public ContainerRuntime Status { get; }

        /// <summary>The menu under its button (added to the UI after the header): save, load, key help.</summary>
        public ContainerRuntime Menu { get; }

        /// <summary>The gold's figure with its coin: it takes hover, for its tip.</summary>
        public ContainerRuntime GoldCounter { get; }

        /// <summary>The bounties' figure with its notice: it takes hover, for its tip.</summary>
        public ContainerRuntime BountiesCounter { get; }

        /// <summary>The stock's entries, each an icon and its count: they take hover, for their tips.</summary>
        public IReadOnlyList<ContainerRuntime> StockCounters => _stockBoxes;

        /// <summary>Opens the stall's prices (the merchant terminal).</summary>
        public Button MerchantButton { get; }
        public Button BountiesButton { get; }

        /// <summary>Opens the guild roster; it reads "Guild 2/4".</summary>
        public Button GuildButton { get; }

        /// <summary>Opens the menu.</summary>
        public Button MenuButton { get; }
        public Button SaveButton { get; }

        /// <summary>Asks before loading (<see cref="ConfirmLoadButton"/> loads).</summary>
        public Button LoadButton { get; }
        public Button ConfirmLoadButton { get; }
        public Button CancelLoadButton { get; }
        public Button KeyHelpButton { get; }

        /// <summary>A pip per size in <see cref="WindowSizes.All"/>, in the menu; the window's own size is lit.</summary>
        public IReadOnlyList<Button> WindowSizeButtons { get; }

        /// <summary>Fills the screen, or goes back to the window; lit while the game fills the screen.</summary>
        public Button FullScreenButton { get; }

        /// <summary>Pauses or resumes the world (P): its glyph is what it will do.</summary>
        public Button PauseButton { get; }

        /// <summary>A pip per speed in <see cref="HeaderModel.Speeds"/> (keys 1 to 4 pick one too); the current one is lit.</summary>
        public IReadOnlyList<Button> SpeedButtons { get; }

        /// <summary>The Map | Ledger switch (Tab).</summary>
        public Button TabButton { get; }

        /// <summary>The view the switch shows lit: "Map" or "Ledger".</summary>
        public string CurrentView { get; private set; } = "Map";

        /// <summary>What the pause control will do, as its tip says: "Play" or "Pause".</summary>
        public string PauseOffers { get; private set; } = "Pause";

        public string GoldText => _gold.Text;
        public string BountiesText => _bounties.Text;
        public string PlaceText => _place.Text;

        /// <summary>True while the menu is open; <see cref="LoadPending"/> while it asks before a load.</summary>
        public bool MenuOpen => Menu.Visible;
        public bool LoadPending => _confirm.Visible;

        /// <summary>The stock's icons and counts, left to right (tests read them).</summary>
        public IReadOnlyList<(SpriteRuntime Icon, TextRuntime Count)> StockEntries => _stockEntries;

        public string TickText => _tick.Text;
        public string SaveStatusText => _saveStatus.Text;

        /// <summary>Opens or closes the menu (closing forgets a pending load).</summary>
        public void ToggleMenu()
        {
            Menu.Visible = !Menu.Visible;
            _confirm.Visible = false;
        }

        /// <summary>Closes the menu; false when it wasn't open.</summary>
        public bool CloseMenu()
        {
            if (!Menu.Visible) return false;
            Menu.Visible = false;
            _confirm.Visible = false;
            return true;
        }

        /// <summary>Load asks first: the menu shows the question and its two answers.</summary>
        public void AskToLoad() => _confirm.Visible = true;

        /// <summary>No load after all.</summary>
        public void CancelLoad() => _confirm.Visible = false;

        /// <summary>Save and Load show only when there is a save slot (Unity hid them without one).</summary>
        public void ShowSaveButtons(bool show)
        {
            SaveButton.IsVisible = show;
            LoadButton.IsVisible = show;
            if (!show) _confirm.Visible = false;
            Status.Visible = show && !string.IsNullOrEmpty(_saveStatus.Text);
        }

        public void SetSaveStatus(string message) => _saveStatus.Text = message ?? string.Empty;

        /// <summary>
        /// Lights the menu's pip for a window <paramref name="width"/> by <paramref name="height"/> (none for a dragged size),
        /// or Full screen while the game fills the screen.
        /// </summary>
        public void BindWindowSize(int width, int height, bool fullScreen)
        {
            if (width == _windowWidth && height == _windowHeight && fullScreen == _fullScreen) return;
            _windowWidth = width;
            _windowHeight = height;
            _fullScreen = fullScreen;
            for (int i = 0; i < WindowSizeButtons.Count; i++)
                StoneSkin.SetActive(WindowSizeButtons[i], !fullScreen && WindowSizes.All[i].X == width && WindowSizes.All[i].Y == height);
            StoneSkin.SetActive(FullScreenButton, fullScreen);
        }

        public void Bind(DashboardModel model)
        {
            var header = model.Header;
            UiParts.SetText(_place, header.Place);
            BindGold(header);
            BindStock(header.Stock);
            UiParts.SetText(_bounties, header.BountyCount);
            UiParts.SetText(_tick, header.Clock);
            UiParts.SetGlyph(_clockGlyph, header.Night ? UiKit.Moon : UiKit.Sun);
            if (GuildButton.Text != header.Guild) GuildButton.Text = header.Guild;

            Lit(MerchantButton, _merchantGlyph, model.Merchant.IsOpen);
            Lit(BountiesButton, _bountiesGlyph, model.Bounties.IsOpen);
            Lit(GuildButton, _guildGlyph, model.Guild.IsOpen);

            PauseOffers = header.PauseLabel;
            UiParts.SetGlyph(_pauseGlyph, header.Paused ? UiKit.Play : UiKit.Pause);
            PauseButton.IsVisible = header.CanControlSpeed;
            for (int i = 0; i < SpeedButtons.Count; i++)
            {
                SpeedButtons[i].IsVisible = header.CanControlSpeed;
                StoneSkin.SetActive(SpeedButtons[i], i == header.SpeedStep);
            }

            CurrentView = model.PanelsVisible ? "Ledger" : "Map";
            bool ledger = model.PanelsVisible;
            _mapLabel.Color = ledger ? UiStyle.TextMuted : UiStyle.TextBright;
            _ledgerLabel.Color = ledger ? UiStyle.TextBright : UiStyle.TextMuted;
            _viewLamp.X = ledger ? 99f : 33f;

            UiParts.SetText(_saveStatus, model.Saves.Status);
            ShowSaveButtons(model.Saves.Available);
        }

        /// <summary>The header moves (<see cref="ClientOptions.UiMotion"/>): the gold rolls to a new figure and its coin pulses.</summary>
        public bool Motion { get; set; }

        /// <summary>True while the gold's figure is rolling.</summary>
        public bool Rolling => _goldRoll >= 0f;

        /// <summary>Moves the gold's roll on by <paramref name="realSeconds"/>.</summary>
        public void Animate(float realSeconds)
        {
            if (_goldRoll < 0f) return;
            _goldRoll += realSeconds;
            float t = _goldRoll / Ui.Motion.NumberRoll;
            if (t >= 1f)
            {
                _goldRoll = -1f;
                UiParts.SetText(_gold, _goldTarget.ToString(System.Globalization.CultureInfo.InvariantCulture));
                _coin.Color = UiStyle.Accent;
                return;
            }
            float eased = Ui.Motion.EaseOut(t);
            long shown = (long)System.MathF.Round(_goldFrom + (_goldTarget - _goldFrom) * eased);
            UiParts.SetText(_gold, shown.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _coin.Color = Microsoft.Xna.Framework.Color.Lerp(CoinPulse, UiStyle.Accent, eased);
        }

        private void BindGold(HeaderModel header)
        {
            if (header.GoldAmount == _goldTarget) return;
            bool roll = Motion && _goldTarget >= 0;
            float from = _goldRoll >= 0f && long.TryParse(_gold.Text, out long shown) ? shown : _goldTarget;
            _goldTarget = header.GoldAmount;
            if (!roll)
            {
                UiParts.SetText(_gold, header.Gold);
                return;
            }
            _goldFrom = from;
            _goldRoll = 0f;
        }

        private static void Lit(Button button, SpriteRuntime glyph, bool open)
        {
            StoneSkin.SetActive(button, open);
            glyph.Color = open ? UiStyle.Accent : UiStyle.TextSecondary;
        }

        /// <summary>One entry per good the model lists: its icon at 1x and its count, built when the list's length changes.</summary>
        private void BindStock(IReadOnlyList<HeaderStock> stock)
        {
            if (_stockEntries.Count != stock.Count)
            {
                _stock.Children.Clear();
                _stockEntries.Clear();
                _stockBoxes.Clear();
                for (int i = 0; i < stock.Count; i++)
                {
                    var entry = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToChildren, Width = 0f, Height = Control - 8f, HasEvents = true };
                    UiParts.CentreVertically(entry);
                    var icon = UiParts.Icon(_icons, null, scale: 1);
                    UiParts.CentreVertically(icon);
                    entry.Children.Add(icon);
                    var count = UiParts.Text("0", UiStyle.Text, UiText.BodyBold);
                    count.X = IconAtlas.SmallSize + 4f;
                    UiParts.CentreVertically(count);
                    entry.Children.Add(count);
                    _stock.Children.Add(entry);
                    _stockEntries.Add((icon, count));
                    _stockBoxes.Add(entry);
                }
            }
            for (int i = 0; i < stock.Count; i++)
            {
                UiParts.SetIcon(_stockEntries[i].Icon, _icons, stock[i].Icon);
                UiParts.SetText(_stockEntries[i].Count, stock[i].Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        /// <summary>A glyph and a figure that take hover (for a tip) but no clicks of their own.</summary>
        private static ContainerRuntime Counter(string glyph, Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Color words, out TextRuntime figure)
            => Counter(glyph, tint, words, out figure, out _);

        private static ContainerRuntime Counter(string glyph, Microsoft.Xna.Framework.Color tint, Microsoft.Xna.Framework.Color words, out TextRuntime figure, out SpriteRuntime mark)
        {
            var counter = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToChildren, Width = 0f, Height = Control - 8f, HasEvents = true };
            UiParts.CentreVertically(counter);
            var icon = UiParts.Glyph(glyph, tint);
            UiParts.CentreVertically(icon);
            counter.Children.Add(icon);
            mark = icon;
            figure = UiParts.Text("0", words, UiText.BodyBold);
            figure.X = icon.Width + 5f;
            UiParts.CentreVertically(figure);
            counter.Children.Add(figure);
            return counter;
        }

        /// <summary>A row in the menu's column, stacked from its top (a centred row would ride up over the line above).</summary>
        private static ContainerRuntime MenuRow(float spacing)
        {
            var row = UiParts.Row(spacing);
            row.YUnits = GeneralUnitType.PixelsFromSmall;
            row.YOrigin = VerticalAlignment.Top;
            return row;
        }

        /// <summary>A 1 px upright line in a plaque between its groups.</summary>
        private static RectangleRuntime Divider()
        {
            var line = new RectangleRuntime { IsFilled = true, StrokeWidth = 0, FillColor = UiStyle.Hex(0x4c4138), Width = 1f, Height = Control - 12f };
            UiParts.CentreVertically(line);
            return line;
        }

        /// <summary>A line across the header, at its top or its foot.</summary>
        private static RectangleRuntime Line(Microsoft.Xna.Framework.Color color, float y, bool fromBottom)
        {
            var line = new RectangleRuntime { IsFilled = true, StrokeWidth = 0, FillColor = color, WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 1f, Y = y };
            if (fromBottom)
            {
                line.YUnits = GeneralUnitType.PixelsFromLarge;
                line.YOrigin = VerticalAlignment.Bottom;
            }
            return line;
        }

        /// <summary>One half of the view switch: its word centred at <paramref name="centre"/>.</summary>
        private static TextRuntime Half(string word, float centre)
        {
            var text = UiParts.Text(word, UiStyle.TextMuted, UiText.StatusBold);
            text.XOrigin = HorizontalAlignment.Center;
            text.X = centre;
            UiParts.CentreVertically(text);
            return text;
        }
    }
}
