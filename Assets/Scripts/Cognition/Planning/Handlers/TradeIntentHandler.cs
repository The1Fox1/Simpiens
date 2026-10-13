using UnityEngine;
using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using Simpiens.Entities;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using ZLogger;

namespace Simpiens.Cognition.Planning.Handlers
{
    public class TradeIntentHandler : IntentHandlerBase<TradeIntent>
    {
        protected override IntentResult Execute(NodeController node, TradeIntent trade, ActiveIntentState state, IIntentExecutionContext context)
        {
            if (!context.TryGetStall(trade.StallEntityId, out var stall))
            {
                return Reject(node, trade, "stall not found", IntentResult.TargetMissing);
            }

            float distToStall = Vector2.Distance(node.Position, stall.Position);
            if (distToStall > stall.InteractionRadius)
            {
                return Reject(node, trade, "out of stall interaction range");
            }

            var agent = node.Agent;
            if (agent == null || agent.Inventory == null)
            {
                return Reject(node, trade, "agent has no inventory");
            }

            uint currentTradeTick = context.CurrentTick;

            if (trade.ActionType == TradeActionType.BuyItem)
            {
                if (trade.ItemType != ItemType.Ration)
                {
                    return Reject(node, trade, "stall only sells rations");
                }

                if (stall.RationStock < trade.Quantity)
                {
                    return Reject(node, trade, "stall out of rations");
                }

                uint unitPrice = stall.RationSellPrice;
                if (unitPrice > trade.MaxExpectedPricePerUnit)
                {
                    return Reject(node, trade, "stall price above agent's expected maximum");
                }

                uint totalCost = unitPrice * trade.Quantity;
                if (agent.Inventory.Gold < totalCost)
                {
                    return Reject(node, trade, "agent cannot afford the purchase");
                }

                var itemToAdd = new ItemInstance(1, ItemType.Ration, trade.Quantity, (ushort)unitPrice);
                if (!agent.Inventory.TryAddItem(itemToAdd, out _))
                {
                    return Reject(node, trade, "inventory full");
                }

                // Atomic transfer: gold moves through the authoritative ledger (direct fallback for headless validators)
                if (context.EconomyLedger != null)
                {
                    if (!context.EconomyLedger.TryExecuteStallPurchase(stall.StallEntityId, ref stall, agent.Inventory, totalCost))
                    {
                        agent.Inventory.TryRemoveItem(ItemType.Ration, trade.Quantity); // roll back the added rations
                        return Reject(node, trade, "ledger refused the purchase");
                    }
                }
                else
                {
                    agent.Inventory.Gold -= (int)totalCost;
                    stall = stall.WithTreasury(stall.TreasuryGold + totalCost);
                }
                stall = stall.WithRationStock((ushort)(stall.RationStock - trade.Quantity));
                context.UpdateStall(stall);

                // Trade-to-Affinity valuation
                const ushort BaseRationCost = 5;
                sbyte deltaTrust, deltaWarmth, deltaRespect;
                if (unitPrice > (BaseRationCost * 1.5f))
                {
                    // Price gouging penalty
                    deltaTrust = -15;
                    deltaWarmth = -10;
                    deltaRespect = 0;
                }
                else if (unitPrice <= BaseRationCost)
                {
                    // Fair / Generous reward
                    deltaTrust = 10;
                    deltaWarmth = 5;
                    deltaRespect = 5;
                }
                else
                {
                    // Moderate trade
                    deltaTrust = 3;
                    deltaWarmth = 2;
                    deltaRespect = 2;
                }

                agent.Memory?.RecordInteraction(context.PlayerEntityId, deltaWarmth, deltaTrust, 0, deltaRespect, currentTradeTick);
                if (agent.State != null) agent.State.Activity.RecordRationPurchase();
                SimLog.Economy.ZLogInformation($"{SimLog.Agent(node.Id)} bought {trade.Quantity}x Ration for {totalCost}g at {SimLog.Agent(stall.StallEntityId)} (stock {stall.RationStock}, treasury {stall.TreasuryGold}g, player trust delta {deltaTrust})");

                context.ChronicleService?.RecordEvent(new ChronicleEvent(
                    ChronicleEventType.TradeTransaction,
                    node.Id,
                    context.PlayerEntityId,
                    context.GetSectorForPosition(node.Position),
                    currentTradeTick,
                    ChronicleIntensity.Notable));

                agent.VisualState = AgentVisualState.Idle;
                return IntentResult.Success;
            }
            else if (trade.ActionType == TradeActionType.SellItem)
            {
                ushort unitBuyPrice = 0;
                if (trade.ItemType == ItemType.IronOre) unitBuyPrice = stall.OreBuyPrice;
                else if (trade.ItemType == ItemType.MedicinalHerb) unitBuyPrice = stall.HerbBuyPrice;
                else return Reject(node, trade, "stall does not buy this item");

                if (!agent.Inventory.HasItem(trade.ItemType, trade.Quantity))
                {
                    return Reject(node, trade, "agent does not hold the items to sell");
                }

                uint totalPayout = (uint)(unitBuyPrice * trade.Quantity);
                if (stall.TreasuryGold < totalPayout)
                {
                    return Reject(node, trade, "stall treasury cannot pay");
                }

                if (!agent.Inventory.TryRemoveItem(trade.ItemType, trade.Quantity))
                {
                    return Reject(node, trade, "inventory removal failed");
                }

                // Atomic transfer: gold moves through the authoritative ledger (direct fallback for headless validators)
                if (context.EconomyLedger != null)
                {
                    if (!context.EconomyLedger.TryExecuteStallSale(stall.StallEntityId, ref stall, agent.Inventory, totalPayout))
                    {
                        agent.Inventory.TryAddItem(new ItemInstance((ushort)trade.ItemType, trade.ItemType, trade.Quantity, trade.ItemType.BaseValue()), out _); // roll back
                        return Reject(node, trade, "ledger refused the sale");
                    }
                }
                else
                {
                    agent.Inventory.Gold += (int)totalPayout;
                    stall = stall.WithTreasury(stall.TreasuryGold - totalPayout);
                }
                ushort newOre = trade.ItemType == ItemType.IronOre ? (ushort)(stall.OreStock + trade.Quantity) : stall.OreStock;
                ushort newHerb = trade.ItemType == ItemType.MedicinalHerb ? (ushort)(stall.HerbStock + trade.Quantity) : stall.HerbStock;
                stall = stall.WithStock(stall.RationStock, newOre, newHerb);
                context.UpdateStall(stall);

                // Trade-to-Affinity valuation
                agent.Memory?.RecordInteraction(context.PlayerEntityId, deltaWarmth: 5, deltaTrust: 10, deltaFear: 0, deltaRespect: 5, currentTradeTick);
                if (agent.State != null) agent.State.Activity.RecordSalvageSale(currentTradeTick);
                SimLog.Economy.ZLogInformation($"{SimLog.Agent(node.Id)} sold {trade.Quantity}x {trade.ItemType} for {totalPayout}g at {SimLog.Agent(stall.StallEntityId)} (ore {stall.OreStock}, herb {stall.HerbStock}, treasury {stall.TreasuryGold}g)");

                context.ChronicleService?.RecordEvent(new ChronicleEvent(
                    ChronicleEventType.TradeTransaction,
                    node.Id,
                    context.PlayerEntityId,
                    context.GetSectorForPosition(node.Position),
                    currentTradeTick,
                    ChronicleIntensity.Notable));

                agent.VisualState = AgentVisualState.Idle;
                return IntentResult.Success;
            }

            return Reject(node, trade, "unknown trade action");
        }

        private static IntentResult Reject(NodeController node, TradeIntent trade, string reason, IntentResult result = IntentResult.Failed)
        {
            SimLog.Economy.ZLogDebug($"{SimLog.Agent(node.Id)} {trade.ActionType} {trade.Quantity}x {trade.ItemType} rejected: {reason}");
            return result;
        }
    }
}
