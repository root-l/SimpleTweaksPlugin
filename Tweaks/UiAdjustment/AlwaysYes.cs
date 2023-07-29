﻿using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using SimpleTweaksPlugin.TweakSystem;
using SimpleTweaksPlugin.Utility;

namespace SimpleTweaksPlugin.Tweaks.UiAdjustment;

public unsafe class AlwaysYes : UiAdjustments.SubTweak {
    public override string Name => "Always Yes";
    public override string Description => "Default cursor to yes when using confirm (num 0).";
    protected override string Author => "Aireil";

    public class Configs : TweakConfig {
        public bool SelectCheckBox = true;
        public bool YesNo = true;
        public bool DutyConfirmation = true;
        public bool CardsShop = true;
        public bool RetainerVentures = true;
        public bool RetainerEntrustDuplicates = true;
        public bool MateriaMelds = true;
        public bool MateriaExtractions = true;
        public bool MateriaRetrievals = true;
        public bool GlamourDispels = true;
        public bool Desynthesis = true;
        public bool Lobby = true;
        public bool ItemExchangeConfirmations = true;
        public List<string> ExceptionsYesNo = new();
    }

    public Configs Config { get; private set; }

    private string newException = string.Empty;

    protected override DrawConfigDelegate DrawConfigTree => (ref bool hasChanged) => {
        hasChanged |= ImGui.Checkbox("Default cursor to the checkbox when one exists", ref Config.SelectCheckBox);
        ImGui.Text("Enable for:");
        ImGui.Indent();

        hasChanged |= ImGui.Checkbox("Most yes/(hold)/no dialogs", ref Config.YesNo);
        ImGui.Indent();
        if (ImGui.CollapsingHeader("Exceptions##AlwaysYes")) {
            ImGui.Text("Do not change default if dialog text contains:");
            for (var  i = 0; i < Config.ExceptionsYesNo.Count; i++) {
                ImGui.PushID($"AlwaysYesBlacklist_{i.ToString()}");
                var exception = Config.ExceptionsYesNo[i];
                if (ImGui.InputText("##AlwaysYesTextBlacklist", ref exception, 500)) {
                    Config.ExceptionsYesNo[i] = exception;
                    hasChanged = true;
                }

                ImGui.SameLine();
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button(FontAwesomeIcon.Trash.ToIconString())) {
                    Config.ExceptionsYesNo.RemoveAt(i--);
                    hasChanged = true;
                }

                ImGui.PopFont();
                ImGui.PopID();
                if (i < 0) break;
            }

            ImGui.InputText("##AlwaysYesNewTextException", ref newException, 500);
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString())) {
                if (newException != string.Empty) {
                    Config.ExceptionsYesNo.Add(newException);
                    newException = string.Empty;
                    hasChanged = true;
                }
            }

            ImGui.PopFont();
        }

        ImGui.Unindent();

        hasChanged |= ImGui.Checkbox("Duty confirmations", ref Config.DutyConfirmation);
        hasChanged |= ImGui.Checkbox("TT cards sales", ref Config.CardsShop);
        hasChanged |= ImGui.Checkbox("Retainer ventures", ref Config.RetainerVentures);
        hasChanged |= ImGui.Checkbox("Retainer entrust duplicates", ref Config.RetainerEntrustDuplicates);
        hasChanged |= ImGui.Checkbox("Materia melds", ref Config.MateriaMelds);
        hasChanged |= ImGui.Checkbox("Materia extractions", ref Config.MateriaExtractions);
        hasChanged |= ImGui.Checkbox("Materia retrievals", ref Config.MateriaRetrievals);
        hasChanged |= ImGui.Checkbox("Glamour dispels", ref Config.GlamourDispels);
        hasChanged |= ImGui.Checkbox("Desynthesis", ref Config.Desynthesis);
        hasChanged |= ImGui.Checkbox("Character selection dialogs", ref Config.Lobby);
        hasChanged |= ImGui.Checkbox("Item exchange confirmations", ref Config.ItemExchangeConfirmations);

        ImGui.Unindent();

        if (hasChanged) {
            SaveConfig(Config);
        }
    };

    public override void Setup() {
        AddChangelog("1.8.5.0", "Added an option to default cursor to the checkbox when one exists.");
        base.Setup();
    }

    protected override void Enable() {
        Config = LoadConfig<Configs>() ?? new Configs();
        Common.AddonSetup += OnAddonSetup;
        base.Enable();
    }

    private void OnAddonSetup(SetupAddonArgs args) {
        switch (args.AddonName) {
            case "SelectYesno":
                if (Config.YesNo && !IsYesnoAnException(args.Addon)) SetFocusYes(args.Addon, 8, 9, 4);
                return;
            case "ContentsFinderConfirm":
                if (Config.DutyConfirmation) SetFocusYes(args.Addon, 63);
                return;
            case "ShopCardDialog":
                if (Config.CardsShop) SetFocusYes(args.Addon, 16);
                return;
            case "RetainerTaskAsk":
                if (Config.RetainerVentures) SetFocusYes(args.Addon, 40);
                return;
            case "RetainerItemTransferList":
                if (Config.RetainerVentures) SetFocusYes(args.Addon, 7);
                return;
            case "RetainerTaskResult":
                if (Config.RetainerVentures) SetFocusYes(args.Addon, 20);
                return;
            case "MateriaAttachDialog":
                if (Config.MateriaMelds) SetFocusYes(args.Addon, 35, null, 39);
                return;
            case "MaterializeDialog":
                if (Config.MateriaExtractions) SetFocusYes(args.Addon, 13);
                return;
            case "MateriaRetrieveDialog":
                if (Config.MateriaRetrievals) SetFocusYes(args.Addon, 17);
                return;
            case "MiragePrismRemove":
                if (Config.GlamourDispels) SetFocusYes(args.Addon, 15);
                return;
            case "SalvageDialog":
                if (Config.Desynthesis) SetFocusYes(args.Addon, 24, null, 23);
                return;
            case "LobbyWKTCheck":
                if (Config.Lobby) SetFocusYes(args.Addon, 4);
                return;
            case "LobbyDKTWorldList":
                if (Config.Lobby) SetFocusYes(args.Addon, 23);
                return;
            case "LobbyDKTCheckExec":
                if (Config.Lobby) SetFocusYes(args.Addon, 3);
                return;
            case "ShopExchangeItemDialog":
                if (Config.ItemExchangeConfirmations) SetFocusYes(args.Addon, 18);
                return;
        }
    }

    private void SetFocusYes(AtkUnitBase* unitBase, uint yesButtonId, uint? yesHoldButtonId = null, uint? checkBoxId = null) {
        if (unitBase == null) return;

        var yesButton = unitBase->UldManager.SearchNodeById(yesButtonId);
        if (yesButton == null) return;

        uint collisionId;
        AtkResNode* targetNode;
        var checkBox = checkBoxId != null ? unitBase->UldManager.SearchNodeById(checkBoxId.Value) : null;
        var textCheckBox = checkBox != null ? (AtkTextNode*)((AtkComponentNode *)checkBox)->Component->UldManager.SearchNodeById(2) : null;
        if (Config.SelectCheckBox && checkBox != null && checkBox->IsVisible && textCheckBox != null && !textCheckBox->NodeText.ToString().IsNullOrWhitespace()) {
            collisionId = 5;
            targetNode = checkBox;
        } else {
            var holdButton = yesHoldButtonId != null ? unitBase->UldManager.SearchNodeById(yesHoldButtonId.Value) : null;
            if (holdButton != null && !yesButton->IsVisible) {
                collisionId = 7;
                targetNode = holdButton;
            }
            else {
                collisionId = 4;
                targetNode = yesButton;
            }
        }

        var yesCollision = ((AtkComponentNode *)targetNode)->Component->UldManager.SearchNodeById(collisionId);
        if (yesCollision == null) return;

        unitBase->SetFocusNode(yesCollision);
        unitBase->CursorTarget = yesCollision;
    }

    private bool IsYesnoAnException(AtkUnitBase* unitBase) {
        if (Config.ExceptionsYesNo.Count == 0 || unitBase == null) return false;

        var textNode = (AtkTextNode *)unitBase->UldManager.SearchNodeById(2);
        if (textNode == null) return false;

        var text = Common.ReadSeString(textNode->NodeText).TextValue.ReplaceLineEndings(string.Empty);

        return text != string.Empty && Config.ExceptionsYesNo.Any(val => text.Contains(val));
    }

    protected override void Disable() {
        Common.AddonSetup -= OnAddonSetup;
        base.Disable();
    }
}
