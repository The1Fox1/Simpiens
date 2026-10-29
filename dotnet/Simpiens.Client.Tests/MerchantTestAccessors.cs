using Gum.Forms.Controls;
using Gum.GueDeriving;
using Simpiens.Client.Ui;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation.Economy;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The merchant terminal by today's goods (item system I6): the window and its model list rows per good the stall
    /// trades; the tests keep the per-item names they were written with.
    /// </summary>
    public static class MerchantTestAccessors
    {
        extension(MerchantWindow window)
        {
            public Slider RationSlider => window.PriceSlider(ItemType.Ration);
            public Slider OreSlider => window.PriceSlider(ItemType.IronOre);
            public Slider HerbSlider => window.PriceSlider(ItemType.MedicinalHerb);
            public Slider GlowstoneSlider => window.PriceSlider(ItemType.Glowstone);
            public Slider GearSlider => window.PriceSlider(ItemType.HazardGear);
            public TextRuntime RationValue => window.PriceValue(ItemType.Ration);
            public TextRuntime OreValue => window.PriceValue(ItemType.IronOre);
            public TextRuntime HerbValue => window.PriceValue(ItemType.MedicinalHerb);
            public TextRuntime GlowstoneValue => window.PriceValue(ItemType.Glowstone);
            public TextRuntime GearValue => window.PriceValue(ItemType.HazardGear);
        }

        extension(MerchantModel model)
        {
            public int RationPrice { get => model.Row(ItemType.Ration).Price; set => model.Row(ItemType.Ration).Price = value; }
            public int OrePrice { get => model.Row(ItemType.IronOre).Price; set => model.Row(ItemType.IronOre).Price = value; }
            public int HerbPrice { get => model.Row(ItemType.MedicinalHerb).Price; set => model.Row(ItemType.MedicinalHerb).Price = value; }
            public int GlowstonePrice { get => model.Row(ItemType.Glowstone).Price; set => model.Row(ItemType.Glowstone).Price = value; }
            public int GearPrice { get => model.Row(ItemType.HazardGear).Price; set => model.Row(ItemType.HazardGear).Price = value; }
        }
    }
}
