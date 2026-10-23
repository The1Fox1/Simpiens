using System;
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
    /// The merchant terminal (Unity's merchant window, 600 px): the stall's status, treasury and stock, three price
    /// sliders (1 to 50 gold), and Apply, which hands the prices to the model to queue.
    /// </summary>
    public sealed class MerchantWindow
    {
        public const float Width = 660f; // 600 until Epic 10 G2's glowstone lengthened the stock line

        private readonly MerchantModel _model;
        private readonly TextRuntime _status, _treasury, _stock, _feedback;
        private bool _binding;

        public MerchantWindow(MerchantModel model)
        {
            _model = model;
            Window = new Window("merchant-terminal", "OUTPOST MERCHANT TERMINAL", "Stall inventory management & automated commodity pricing.", Width, UiStyle.Accent);
            var body = Window.Body;

            var statusBar = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 34f };
            UiParts.AddFill(statusBar, UiStyle.Inset, UiStyle.Border);
            // Unity spread the three across the bar; at Inter's widths the treasury follows the status instead
            var left = UiParts.Row(14f);
            left.X = 12f;
            _status = UiParts.Text(string.Empty, UiStyle.Text, UiText.StatusBold);
            UiParts.CentreVertically(_status);
            _treasury = UiParts.Text(string.Empty, UiStyle.GoldText, UiText.StatusBold);
            UiParts.CentreVertically(_treasury);
            left.Children.Add(_status);
            left.Children.Add(_treasury);
            _stock = UiParts.Text(string.Empty, UiStyle.TextSecondary, UiText.Status);
            _stock.XUnits = GeneralUnitType.PixelsFromLarge;
            _stock.XOrigin = HorizontalAlignment.Right;
            _stock.X = -12f;
            UiParts.CentreVertically(_stock);
            statusBar.Children.Add(left);
            statusBar.Children.Add(_stock);
            body.Children.Add(statusBar);
            body.Children.Add(Window.Gap(14f));

            body.Children.Add(Window.SubHeader("COMMODITY EXCHANGE RATES"));
            body.Children.Add(Window.Gap(6f));
            RationSlider = AddPriceRow(body, "Provision Sell Price:", "(Outpost sells rations to hungry agents)", v => _model.RationPrice = v, out RationValue);
            OreSlider = AddPriceRow(body, "Iron Ore Buy Price:", "(Outpost purchases ore from salvagers)", v => _model.OrePrice = v, out OreValue);
            HerbSlider = AddPriceRow(body, "Herb Buy Price:", "(Outpost purchases medicinal flora)", v => _model.HerbPrice = v, out HerbValue);
            GlowstoneSlider = AddPriceRow(body, "Glowstone Buy Price:", "(Rare salvage from the hazard sectors)", v => _model.GlowstonePrice = v, out GlowstoneValue);

            body.Children.Add(Window.Gap(6f));
            body.Children.Add(Window.Rule());
            var footer = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 48f };
            _feedback = UiParts.Text(string.Empty, UiStyle.Success, UiText.Small);
            UiParts.CentreVertically(_feedback);
            footer.Children.Add(_feedback);
            ApplyButton = UiParts.Button("APPLY COMMODITY PRICES", 210f, 32f, UiText.BodyBold);
            ApplyButton.Visual.XUnits = GeneralUnitType.PixelsFromLarge;
            ApplyButton.Visual.XOrigin = HorizontalAlignment.Right;
            UiParts.CentreVertically(ApplyButton.Visual);
            footer.Children.Add(ApplyButton.Visual);
            body.Children.Add(footer);

            ApplyButton.Click += (_, _) => { _model.Apply(); Bind(); };
        }

        public Window Window { get; }
        public Slider RationSlider { get; }
        public Slider OreSlider { get; }
        public Slider HerbSlider { get; }
        public Slider GlowstoneSlider { get; }
        public readonly TextRuntime RationValue, OreValue, HerbValue, GlowstoneValue;
        public Button ApplyButton { get; }

        public string StatusText => _status.Text;
        public string TreasuryText => _treasury.Text;
        public string FeedbackText => _feedback.Text;

        private Slider AddPriceRow(ContainerRuntime body, string title, string description, Action<int> set, out TextRuntime value)
        {
            var row = new ContainerRuntime { WidthUnits = DimensionUnitType.RelativeToParent, Width = 0f, Height = 48f };
            UiParts.AddFill(row, UiStyle.Card);
            var labels = UiParts.Column(2f);
            labels.X = 12f;
            UiParts.CentreVertically(labels);
            labels.Children.Add(UiParts.Text(title, UiStyle.TextBright, UiText.BodyBold));
            labels.Children.Add(UiParts.Text(description, UiStyle.TextMuted, UiText.Small));
            row.Children.Add(labels);

            var slider = UiParts.Slider(MerchantModel.MinPrice, MerchantModel.MaxPrice, 1);
            slider.Visual.WidthUnits = DimensionUnitType.Absolute;
            slider.Width = 220f;
            slider.Visual.XUnits = GeneralUnitType.PixelsFromLarge;
            slider.Visual.XOrigin = HorizontalAlignment.Right;
            slider.X = -86f;
            UiParts.CentreVertically(slider.Visual);
            row.Children.Add(slider.Visual);

            var shown = UiParts.Text(MerchantModel.PriceText(MerchantModel.MinPrice), UiStyle.Accent, UiText.BodyBold);
            shown.XUnits = GeneralUnitType.PixelsFromLarge;
            shown.XOrigin = HorizontalAlignment.Right;
            shown.X = -12f;
            UiParts.CentreVertically(shown);
            row.Children.Add(shown);
            value = shown;

            slider.ValueChanged += (_, _) =>
            {
                int gold = (int)Math.Round(slider.Value);
                shown.Text = MerchantModel.PriceText(gold);
                if (!_binding) set(gold);
            };
            body.Children.Add(row);
            body.Children.Add(Window.Gap(8f));
            return slider;
        }

        /// <summary>The model's prices into the sliders (when the window opens), then its status and feedback.</summary>
        public void LoadPrices()
        {
            _binding = true;
            RationSlider.Value = _model.RationPrice;
            OreSlider.Value = _model.OrePrice;
            HerbSlider.Value = _model.HerbPrice;
            GlowstoneSlider.Value = _model.GlowstonePrice;
            RationValue.Text = MerchantModel.PriceText(_model.RationPrice);
            OreValue.Text = MerchantModel.PriceText(_model.OrePrice);
            HerbValue.Text = MerchantModel.PriceText(_model.HerbPrice);
            GlowstoneValue.Text = MerchantModel.PriceText(_model.GlowstonePrice);
            _binding = false;
        }

        public void Bind()
        {
            Window.IsShown = _model.IsOpen;
            if (!_model.IsOpen) return;
            UiParts.SetText(_status, _model.StallStatus);
            UiParts.SetText(_treasury, _model.Treasury);
            UiParts.SetText(_stock, _model.Stock);
            UiParts.SetText(_feedback, _model.Feedback.Text);
            _feedback.Color = UiStyle.ToneColor(_model.Feedback.Tone);
        }
    }
}
