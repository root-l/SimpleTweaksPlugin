﻿using System;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment; 

public unsafe class MarketEnhancements : UiAdjustments.SubTweak {
        
    public class MarketEnhancementsConfig : TweakConfig {
        [TweakConfigOption("##ResellProfit", 1)]
        public bool HighlightNpcSellProfit;

        [TweakConfigOption("Highlight items that can be sold to NPC for more.", "SimpleColor", 2, SameLine = true)]
        public Vector4 NpcSellProfitColour = new Vector4(0, 1, 0, 1);

        [TweakConfigOption("##LazyTax", 3)]
        public bool HighlightLazyTax;

        [TweakConfigOption("Highlight items that can be purchased from an NPC for cheaper.", "SimpleColor", 4, SameLine = true)]
        public Vector4 LazyTaxColour = new Vector4(1, 0, 0, 1);
    }
        
    public MarketEnhancementsConfig Config { get; private set; }
        
    public override string Name => "Market Enhancements";
    public override string Description => "Highlight items that could be bought from, or sold to, an NPC for a better price.";
    public override bool UseAutoConfig => true;
    
    private delegate void UpdateResultDelegate(AtkUnitBase* addonItemSearchResult, uint a2, ulong* a3, void* a4);
    private UpdateResultDelegate updateResult;

    private void* updateResultPointer;
        
    private delegate void* AddonSetupDelegate(void* a1, AtkUnitBase* a2, void* a3);
    private HookWrapper<AddonSetupDelegate> addonSetupHook;

    private UpdateResultDelegate replacementUpdateResultDelegate;

    public override void Setup() {
        AddChangelog(Changelog.UnreleasedVersion, "Return of the Lazy Tax/Profitable highlighting");
        base.Setup();
    }

    protected override void Enable() {
        Config = LoadConfig<MarketEnhancementsConfig>() ?? new MarketEnhancementsConfig();
        replacementUpdateResultDelegate = SetItemDetour;
        updateResultPointer = (void*) Service.SigScanner.ScanText("40 57 48 83 EC 30 8B FA");
        updateResult = Marshal.GetDelegateForFunctionPointer<UpdateResultDelegate>(new IntPtr(updateResultPointer));
        addonSetupHook ??= Common.Hook("E8 ?? ?? ?? ?? 41 B1 1E", new AddonSetupDelegate(SetupDetour));
        addonSetupHook?.Enable();
        UpdateItemList(Common.GetUnitBase("ItemSearchResult"));
        base.Enable();
    }

    private void* SetupDetour(void* a1, AtkUnitBase* a2, void* a3) {
        if (a3 == updateResultPointer) {
            var ptr = Marshal.GetFunctionPointerForDelegate(replacementUpdateResultDelegate);
            a3 = (void*) ptr;
        }
        return addonSetupHook.Original(a1, a2, a3);
    }

    private void SetItemDetour(AtkUnitBase* addonItemSearchResult, uint resultIndex, ulong* a3, void* a4) {
        updateResult(addonItemSearchResult, resultIndex, a3, a4);
        if (Enabled) UpdateItemList(addonItemSearchResult);
    }
        
    private uint npcPriceId;
    private uint npcBuyPrice;
    private uint npcSellPrice;
        
    private void UpdateItemList(AtkUnitBase* itemSearchResult) {
        try {
            if (itemSearchResult == null) itemSearchResult = Common.GetUnitBase("ItemSearchResult");
            if (itemSearchResult == null) return;
            if (Encoding.UTF8.GetString(itemSearchResult->Name, 16) != "ItemSearchResult") return;

            var listNode = (AtkComponentNode*)itemSearchResult->UldManager.SearchNodeById(26);
            if (listNode == null) return;
            var component = (AtkComponentList*) listNode->Component;

            var agent = AgentItemSearch.Instance();
            if (agent == null) return;
            
            if (npcPriceId != agent->ResultItemID) {
                var item = Service.Data.Excel.GetSheet<Item>()?.GetRow(agent->ResultItemID);
                if (item == null) return;
                npcPriceId = agent->ResultItemID;
                npcBuyPrice = 0;
                npcSellPrice = item.PriceLow;
                var gilShopItem = Service.Data.Excel.GetSheet<GilShopItem>()?.Where(a => a.Item.Row == agent->ResultItemID).ToList();
                if (gilShopItem is { Count: > 0 }) npcBuyPrice = item.PriceMid;
            }
                
            for (var i = 0; i < 12 && i < component->ListLength; i++) {
                var listItem = component->ItemRendererList[i].AtkComponentListItemRenderer;

                var uldManager = listItem->AtkComponentButton.AtkComponentBase.UldManager;

                var singlePriceNode = (AtkTextNode*)uldManager.SearchNodeById(5);
                var qtyTextNode = (AtkTextNode*)uldManager.SearchNodeById(6);
                var totalTextNode = (AtkTextNode*) uldManager.SearchNodeById(8);
                var hqImageNode = (AtkImageNode*)uldManager.SearchNodeById(3);
                if (hqImageNode == null || totalTextNode == null || qtyTextNode == null || singlePriceNode == null) {
                    continue;
                }
                    
                if (singlePriceNode->NodeText.StringPtr[0] == 0x20 || totalTextNode->NodeText.StringPtr[0] == 0x20) continue;
                    
                var priceString = Common.ReadSeString(singlePriceNode->NodeText).TextValue
                    .Replace($"{(char) SeIconChar.Gil}", "")
                    .Replace($",", "")
                    .Replace(" ", "")
                    .Replace($".", "");

                if (!ulong.TryParse(priceString, out var priceValue)) continue;
                if (!ushort.TryParse(Common.ReadSeString(qtyTextNode->NodeText).TextValue.Replace(",", "").Replace(".", ""), out var qtyValue)) continue;
                if (priceValue <= 0 || qtyValue <= 0) continue;

                var total = priceValue * qtyValue;

                var totalWithTax = total * 105 / 100;
                var realCostPerItem = totalWithTax / (float)qtyValue; 
                

                var sellValue = Math.Ceiling(npcSellPrice * (hqImageNode->AtkResNode.IsVisible ? 1.1 : 1.0));
                if (Config.HighlightLazyTax && npcBuyPrice > 0 && realCostPerItem > npcBuyPrice && !hqImageNode->AtkResNode.IsVisible) {
                    singlePriceNode->EdgeColor.R = (byte)(Config.LazyTaxColour.X * 255f);
                    singlePriceNode->EdgeColor.G = (byte)(Config.LazyTaxColour.Y * 255f);
                    singlePriceNode->EdgeColor.B = (byte)(Config.LazyTaxColour.Z * 255f);
                    singlePriceNode->EdgeColor.A = (byte)(Config.LazyTaxColour.W * 255f);
                    singlePriceNode->TextFlags |= (byte) TextFlags.Edge;
                } else if (Config.HighlightNpcSellProfit && npcSellPrice > 0 && realCostPerItem < sellValue) {
                    singlePriceNode->EdgeColor.R = (byte)(Config.NpcSellProfitColour.X * 255f);
                    singlePriceNode->EdgeColor.G = (byte)(Config.NpcSellProfitColour.Y * 255f);
                    singlePriceNode->EdgeColor.B = (byte)(Config.NpcSellProfitColour.Z * 255f);
                    singlePriceNode->EdgeColor.A = (byte)(Config.NpcSellProfitColour.W * 255f);
                    singlePriceNode->TextFlags |= (byte) TextFlags.Edge;
                } else {
                    singlePriceNode->EdgeColor.R = 0;
                    singlePriceNode->EdgeColor.G = 0;
                    singlePriceNode->EdgeColor.B = 0;
                    singlePriceNode->EdgeColor.A = 0;
                }
            }

        } catch (Exception ex) {
            Plugin.Error(this, ex, false, "Error in List Update");
        }
    }
        
    private void CloseMarketResults() {
        var isr = Common.GetUnitBase("ItemSearchResult");
        if (isr != null) UiHelper.Close(isr, true);
    }

    protected override void Disable() {
        SaveConfig(Config);
        CloseMarketResults();
        addonSetupHook?.Disable();
        base.Disable();
    }
        
    public override void Dispose() {
        addonSetupHook?.Dispose();
        base.Dispose();
    }
}