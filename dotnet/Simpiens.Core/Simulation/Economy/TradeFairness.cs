namespace Simpiens.Simulation.Economy
{
    /// <summary>How a crawler judged a completed trade at the player's stall.</summary>
    public enum TradeVerdict : byte
    {
        Generous = 0,
        Fair = 1,
        /// <summary>A purchase above the crawler's calm valuation: hunger made it pay more than it thinks fair.</summary>
        Gouged = 2,
        /// <summary>A sale at or just above the crawler's minimum: it needed the gold.</summary>
        Stingy = 3
    }

    /// <summary>
    /// The fairness rule (review §3.3 D3): a crawler judges a completed trade by its own valuation, not by a fixed
    /// multiple of base value. Pure and allocation-free; the trade handler supplies the crawler's valuation.
    /// Only completed trades are judged: a refused trade changes nothing.
    /// </summary>
    public static class TradeFairness
    {
        /// <summary>Judges a ration purchase at <paramref name="price"/> against the buyer's calm valuation.</summary>
        public static TradeVerdict ClassifyPurchase(uint price, float calmValue, in TradeTrustTuning tuning)
        {
            if (calmValue <= 0f) return TradeVerdict.Gouged;
            float ratio = price / calmValue;
            if (ratio <= tuning.GenerousBuyRatio) return TradeVerdict.Generous;
            if (ratio <= tuning.FairBuyRatio) return TradeVerdict.Fair;
            return TradeVerdict.Gouged;
        }

        /// <summary>Judges a salvage sale at <paramref name="price"/> against the seller's minimum sale price.</summary>
        public static TradeVerdict ClassifySale(uint price, float minimumPrice, in TradeTrustTuning tuning)
        {
            if (minimumPrice <= 0f) return TradeVerdict.Generous;
            float ratio = price / minimumPrice;
            if (ratio >= tuning.GenerousSaleRatio) return TradeVerdict.Generous;
            if (ratio >= tuning.FairSaleRatio) return TradeVerdict.Fair;
            return TradeVerdict.Stingy;
        }

        /// <summary>
        /// Judges a delivered bounty's <paramref name="reward"/> against the claimant's own minimum for the work (Epic 10 G4,
        /// D5), as a sale is judged: generous, fair or stingy.
        /// </summary>
        public static TradeVerdict ClassifyBounty(uint reward, float minimum, in TradeTrustTuning tuning)
        {
            if (minimum <= 0f) return TradeVerdict.Generous;
            float ratio = reward / minimum;
            if (ratio >= tuning.GenerousBountyRatio) return TradeVerdict.Generous;
            if (ratio >= tuning.FairBountyRatio) return TradeVerdict.Fair;
            return TradeVerdict.Stingy;
        }

        /// <summary>The affinity change the tuning assigns to a verdict on a bounty.</summary>
        public static TrustDelta BountyDelta(TradeVerdict verdict, in TradeTrustTuning tuning)
        {
            switch (verdict)
            {
                case TradeVerdict.Generous: return tuning.GenerousBounty;
                case TradeVerdict.Fair: return tuning.FairBounty;
                default: return tuning.StingyBounty;
            }
        }

        /// <summary>The affinity change the tuning assigns to a verdict on a purchase.</summary>
        public static TrustDelta PurchaseDelta(TradeVerdict verdict, in TradeTrustTuning tuning)
        {
            switch (verdict)
            {
                case TradeVerdict.Generous: return tuning.GenerousPurchase;
                case TradeVerdict.Fair: return tuning.FairPurchase;
                default: return tuning.GougedPurchase;
            }
        }

        /// <summary>The affinity change the tuning assigns to a verdict on a sale.</summary>
        public static TrustDelta SaleDelta(TradeVerdict verdict, in TradeTrustTuning tuning)
        {
            switch (verdict)
            {
                case TradeVerdict.Generous: return tuning.GenerousSale;
                case TradeVerdict.Fair: return tuning.FairSale;
                default: return tuning.StingySale;
            }
        }
    }
}
