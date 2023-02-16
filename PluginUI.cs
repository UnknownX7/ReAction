using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;

namespace ReAction;

public static class PluginUI
{
    private static bool isVisible = false;
    private static int selectedStack = -1;
    private static int hotbar = 0;
    private static int hotbarSlot = 0;
    private static int commandType = 1;
    private static int commandID = 0;

    public static bool IsVisible
    {
        get => isVisible;
        set => isVisible = value;
    }

    private static Configuration.ActionStack CurrentStack => 0 <= selectedStack && selectedStack < ReAction.Config.ActionStacks.Count ? ReAction.Config.ActionStacks[selectedStack] : null;

    public static void Draw()
    {
        if (!isVisible) return;

        ImGui.SetNextWindowSizeConstraints(new Vector2(700, 660) * ImGuiHelpers.GlobalScale, new Vector2(9999));
        ImGui.Begin("ReAction Configuration", ref isVisible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGuiEx.AddDonationHeader(2);

        if (ImGui.BeginTabBar("ReActionTabs"))
        {
            if (ImGui.BeginTabItem("Stacks"))
            {
                DrawStackList();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Other Settings"))
            {
                DrawOtherSettings();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Help"))
            {
                DrawStackHelp();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private static void DrawStackList()
    {
        var currentStack = CurrentStack;
        var hasSelectedStack = currentStack != null;

        ImGui.PushFont(UiBuilder.IconFont);

        var buttonSize = ImGui.CalcTextSize(FontAwesomeIcon.SignOutAlt.ToIconString()) + ImGui.GetStyle().FramePadding * 2;

        if (ImGui.Button(FontAwesomeIcon.Plus.ToIconString(), buttonSize))
        {
            ReAction.Config.ActionStacks.Add(new() { Name = "New Stack" });
            ReAction.Config.Save();
        }

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.SignOutAlt.ToIconString(), buttonSize) && hasSelectedStack)
            ImGui.SetClipboardText(Configuration.ExportActionStack(CurrentStack));
        ImGui.PopFont();
        ImGuiEx.SetItemTooltip("Export stack to clipboard.");
        ImGui.PushFont(UiBuilder.IconFont);

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.SignInAlt.ToIconString(), buttonSize))
        {
            try
            {
                var stack = Configuration.ImportActionStack(ImGui.GetClipboardText());
                ReAction.Config.ActionStacks.Add(stack);
                ReAction.Config.Save();
            }
            catch (Exception e)
            {
                ReAction.PrintError($"Failed to import stack from clipboard!\n{e.Message}");
            }
        }
        ImGui.PopFont();
        ImGuiEx.SetItemTooltip("Import stack from clipboard.");
        ImGui.PushFont(UiBuilder.IconFont);

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.ArrowUp.ToIconString(), buttonSize) && hasSelectedStack)
        {
            var preset = CurrentStack;
            ReAction.Config.ActionStacks.RemoveAt(selectedStack);

            selectedStack = Math.Max(selectedStack - 1, 0);

            ReAction.Config.ActionStacks.Insert(selectedStack, preset);
            ReAction.Config.Save();
        }

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.ArrowDown.ToIconString(), buttonSize) && hasSelectedStack)
        {
            var preset = CurrentStack;
            ReAction.Config.ActionStacks.RemoveAt(selectedStack);

            selectedStack = Math.Min(selectedStack + 1, ReAction.Config.ActionStacks.Count);

            ReAction.Config.ActionStacks.Insert(selectedStack, preset);
            ReAction.Config.Save();
        }

        ImGui.SameLine();

        ImGui.Button(FontAwesomeIcon.Times.ToIconString(), buttonSize);
        if (hasSelectedStack && ImGui.BeginPopupContextItem(null, ImGuiPopupFlags.MouseButtonLeft))
        {
            if (ImGui.Selectable(FontAwesomeIcon.TrashAlt.ToIconString()))
            {
                ReAction.Config.ActionStacks.RemoveAt(selectedStack);
                selectedStack = Math.Min(selectedStack, ReAction.Config.ActionStacks.Count - 1);
                currentStack = CurrentStack;
                hasSelectedStack = currentStack != null;
                ReAction.Config.Save();
            }
            ImGui.EndPopup();
        }

        ImGui.PopFont();

        var firstColumnWidth = 250 * ImGuiHelpers.GlobalScale;
        ImGui.PushStyleColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.TabActive));
        ImGui.BeginChild("ReActionPresetList", new Vector2(firstColumnWidth, ImGui.GetContentRegionAvail().Y / 2), true);
        ImGui.PopStyleColor();

        for (int i = 0; i < ReAction.Config.ActionStacks.Count; i++)
        {
            ImGui.PushID(i);

            var preset = ReAction.Config.ActionStacks[i];

            if (ImGui.Selectable(preset.Name, selectedStack == i))
                selectedStack = i;

            ImGui.PopID();
        }

        ImGui.EndChild();

        if (!hasSelectedStack) return;

        var lastCursorPos = ImGui.GetCursorPos();
        ImGui.SameLine();
        var nextLineCursorPos = ImGui.GetCursorPos();
        ImGui.SetCursorPos(lastCursorPos);

        ImGui.BeginChild("ReActionStackEditorMain", new Vector2(firstColumnWidth, ImGui.GetContentRegionAvail().Y), true);
        DrawStackEditorMain(currentStack);
        ImGui.EndChild();

        ImGui.SetCursorPos(nextLineCursorPos);
        ImGui.BeginChild("ReActionStackEditorLists", ImGui.GetContentRegionAvail(), false);
        DrawStackEditorLists(currentStack);
        ImGui.EndChild();
    }

    private static void DrawStackEditorMain(Configuration.ActionStack stack)
    {
        var save = false;

        save |= ImGui.InputText("Name", ref stack.Name, 64);
        save |= ImGui.CheckboxFlags("##Shift", ref stack.ModifierKeys, 1);
        ImGuiEx.SetItemTooltip("Shift");
        ImGui.SameLine();
        save |= ImGui.CheckboxFlags("##Ctrl", ref stack.ModifierKeys, 2);
        ImGuiEx.SetItemTooltip("Control");
        ImGui.SameLine();
        save |= ImGui.CheckboxFlags("##Alt", ref stack.ModifierKeys, 4);
        ImGuiEx.SetItemTooltip("Alt");
        ImGui.SameLine();
        save |= ImGui.CheckboxFlags("##Exact", ref stack.ModifierKeys, 8);
        ImGuiEx.SetItemTooltip("Match exactly these modifiers. E.g. Shift + Control ticked will match Shift + Control held, but not Shift + Control + Alt held.");
        ImGui.SameLine();
        ImGui.TextUnformatted("Modifier Keys");
        save |= ImGui.Checkbox("Block Original on Stack Fail", ref stack.BlockOriginal);
        save |= ImGui.Checkbox("Fail if Out of Range", ref stack.CheckRange);
        save |= ImGui.Checkbox("Fail if On Cooldown", ref stack.CheckCooldown);
        ImGuiEx.SetItemTooltip("Will fail if the action would fail to queue due to cooldown. Which is either" +
            "\n> 0.5s left on the cooldown, or < 0.5s since the last use (Charges / GCD).");

        if (save)
            ReAction.Config.Save();
    }

    private static void DrawStackEditorLists(Configuration.ActionStack stack)
    {
        DrawActionEditor(stack);
        DrawItemEditor(stack);
    }

    private static string FormatActionRow(Lumina.Excel.GeneratedSheets.Action a) => a.RowId switch
    {
        0 => "All Actions",
        1 => "All Harmful Actions",
        2 => "All Beneficial Actions",
        _ => $"[#{a.RowId} {a.ClassJob.Value?.Abbreviation}{(a.IsPvP ? " PVP" : string.Empty)}] {a.Name}"
    };

    private static readonly ImGuiEx.ExcelSheetComboOptions<Lumina.Excel.GeneratedSheets.Action> actionComboOptions = new()
    {
        FormatRow = FormatActionRow,
        SearchPredicate = (row, s) => (row.RowId <= 2 || row.ClassJobCategory.Row > 0 && row.ActionCategory.Row <= 4 && row.RowId is not 7)
            && FormatActionRow(row).Contains(s, StringComparison.CurrentCultureIgnoreCase)
    };

    private static void DrawActionEditor(Configuration.ActionStack stack)
    {
        var contentRegion = ImGui.GetContentRegionAvail();
        ImGui.BeginChild("ReActionActionEditor", new Vector2(contentRegion.X, contentRegion.Y / 2), true);

        var buttonWidth = ImGui.GetContentRegionAvail().X / 2;
        for (int i = 0; i < stack.Actions.Count; i++)
        {
            ImGui.PushID(i);

            var action = stack.Actions[i];

            ImGui.SetNextItemWidth(buttonWidth);
            if (ImGuiEx.ExcelSheetCombo("##Action", ref action.ID, actionComboOptions))
                ReAction.Config.Save();

            ImGui.SameLine();

            if (ImGui.Checkbox("Adjust ID", ref action.UseAdjustedID))
                ReAction.Config.Save();
            var detectedAdjustment = false;
            unsafe
            {
                if (!action.UseAdjustedID && (detectedAdjustment = Common.ActionManager->CS.GetAdjustedActionId(action.ID) != action.ID))
                    ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), 0x2000FF30, ImGui.GetStyle().FrameRounding);
            }
            ImGuiEx.SetItemTooltip("Allows the action to match any other action that it transforms into." +
                "\nE.g. Aero will match Dia, Play will match all cards, Diagnosis will match Eukrasian Diagnosis, etc." +
                "\nEnable this for skills that upgrade. Disable this for compatibility with certain XIVCombos." +
                (detectedAdjustment ? "\n\nThis action is currently adjusted due to a trait, combo or plugin. This option is recommended." : string.Empty));

            ImGui.SameLine();

            ImGui.PushFont(UiBuilder.IconFont);

            ImGui.Button(FontAwesomeIcon.TimesCircle.ToIconString());
            if (ImGui.BeginPopupContextItem(null, ImGuiPopupFlags.MouseButtonLeft))
            {
                if (ImGui.Selectable(FontAwesomeIcon.TrashAlt.ToIconString()))
                {
                    stack.Actions.RemoveAt(i);
                    ReAction.Config.Save();
                }
                ImGui.EndPopup();
            }

            ImGui.PopFont();

            ImGui.PopID();
        }

        AddActionList(stack.Actions, buttonWidth);

        ImGui.EndChild();
    }

    private static string FormatOverrideActionRow(Lumina.Excel.GeneratedSheets.Action a) => a.RowId switch
    {
        0 => "Same Action",
        _ => $"[#{a.RowId} {a.ClassJob.Value?.Abbreviation}{(a.IsPvP ? " PVP" : string.Empty)}] {a.Name}"
    };

    private static readonly ImGuiEx.ExcelSheetComboOptions<Lumina.Excel.GeneratedSheets.Action> actionOverrideComboOptions = new()
    {
        FormatRow = FormatOverrideActionRow,
        SearchPredicate = (row, s) => (row.RowId == 0 || row.ClassJobCategory.Row > 0 && row.ActionCategory.Row <= 4 && row.RowId is not 7)
            && FormatOverrideActionRow(row).Contains(s, StringComparison.CurrentCultureIgnoreCase)
    };

    private static void DrawItemEditor(Configuration.ActionStack stack)
    {
        ImGui.BeginChild("ReActionItemEditor", ImGui.GetContentRegionAvail(), true);

        var buttonWidth = ImGui.GetContentRegionAvail().X / 3;
        var targets = Enum.GetNames(typeof(ActionStackManager.TargetType));
        for (int i = 0; i < stack.Items.Count; i++)
        {
            ImGui.PushID(i);

            var item = stack.Items[i];

            ImGui.SetNextItemWidth(buttonWidth);
            var _ = (int)item.Target;
            if (ImGui.Combo("##TargetType", ref _, targets, targets.Length))
            {
                item.Target = (ActionStackManager.TargetType)_;
                ReAction.Config.Save();
            }

            ImGui.SameLine();

            ImGui.SetNextItemWidth(buttonWidth);
            if (ImGuiEx.ExcelSheetCombo("##Action", ref item.ID, actionOverrideComboOptions))
                ReAction.Config.Save();

            ImGui.SameLine();

            ImGui.PushFont(UiBuilder.IconFont);

            ImGui.Button(FontAwesomeIcon.TimesCircle.ToIconString());
            if (ImGui.BeginPopupContextItem(null, ImGuiPopupFlags.MouseButtonLeft))
            {
                if (ImGui.Selectable(FontAwesomeIcon.TrashAlt.ToIconString()))
                {
                    stack.Items.RemoveAt(i);
                    ReAction.Config.Save();
                }
                ImGui.EndPopup();
            }

            ImGui.PopFont();

            ImGui.PopID();
        }

        ImGui.PushFont(UiBuilder.IconFont);

        if (ImGui.Button(FontAwesomeIcon.PlusCircle.ToIconString(), new Vector2(buttonWidth, 0)))
        {
            stack.Items.Add(new());
            ReAction.Config.Save();
        }

        ImGui.PopFont();

        ImGui.EndChild();
    }

    private static readonly ImGuiEx.ExcelSheetPopupOptions<Lumina.Excel.GeneratedSheets.Action> actionPopupOptions = new()
        {
            FormatRow = FormatActionRow,
            SearchPredicate = (row, s) => (row.RowId <= 2 || row.ClassJobCategory.Row > 0 && row.ActionCategory.Row <= 4 && row.RowId is not 7)
                && FormatActionRow(row).Contains(s, StringComparison.CurrentCultureIgnoreCase)
        };

    private static void AddActionList(ICollection<Configuration.Action> actions, float buttonWidth)
    {
        ImGui.PushFont(UiBuilder.IconFont);

        const string id = "ReActionAddSkillsPopup";
        if (ImGui.Button(FontAwesomeIcon.PlusCircle.ToIconString(), new Vector2(buttonWidth, 0)))
            ImGui.OpenPopup(id);

        ImGui.PopFont();

        if (!ImGuiEx.ExcelSheetPopup(id, out var row, actionPopupOptions)) return;
        actions.Add(new() { ID = row });
        ReAction.Config.Save();
    }

    private static void DrawStackHelp()
    {
        ImGui.Text("Creating a Stack");
        ImGui.Indent();
        ImGui.TextWrapped("To start, click the + button in the top left corner, this will create a new stack that you can begin adding actions and functionality to.");
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.Text("Editing a Stack");
        ImGui.Indent();
        ImGui.TextWrapped("Click on a stack from the top left list to display the editing panes for that it. The bottom left pane is where the " +
            "main settings reside, these will change the base functionality for the stack itself.");
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.Text("Editing a Stack's Actions");
        ImGui.Indent();
        ImGui.TextWrapped("The top right pane is where you can add actions, click the + to bring up a box that you can search for them through. " +
            "After adding every action that you would like to change the functionality of, you can additionally select which ones you would like to " +
            "\"adjust\". This means that the selected action will match any other one that replaces it on the hotbar. This can be due to a trait " +
            "(Holy <-> Holy III), a buff (Play -> The Balance) or another plugin (XIVCombo). An example case where you might want it off is when the " +
            "adjusted action has a separate use case, such as XIVCombo turning Play into Draw. You can change the functionality of the individual " +
            "cards while not affecting Draw by adding each of them to the list. Additionally, if the action is currently adjusted by the game, the " +
            "option will be highlighted in green as an indicator.");
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.Text("Editing a Stack's Functionality");
        ImGui.Indent();
        ImGui.TextWrapped("The bottom right pane is where you can change the functionality of the selected actions, by setting a list of targets to " +
            "extend or replace the game's. When the action is used, the plugin will attempt to determine, from top to bottom, which target is a valid choice. " +
            "This will execute before the game's own target priority system and only allow it to continue if not blocked by the stack. If any of the targets " +
            "are valid choices, the plugin will change the action's target to the new one and, additionally, replace the action with the override if set.");
        ImGui.Unindent();

        ImGui.Separator();

        ImGui.Text("Stack Priority");
        ImGui.Indent();
        ImGui.TextWrapped("The executed stack will depend on which one, from top to bottom, first contains the action being used and has its modifier " +
            "keys held. If you would like to use \"All Actions\" in a stack, you can utilize this to add overrides above it in the list. Note that a stack " +
            "does not need to contain any functionality in the event that you would like for a set of actions to never be changed by \"All Actions\" and " +
            "instead use the original.");
        ImGui.Unindent();
    }

    private static void DrawOtherSettings()
    {
        ImGui.Columns(2, null, false);

        var save = false;

        if (ImGui.Checkbox("Enable Ground Target Queuing", ref ReAction.Config.EnableGroundTargetQueuing))
        {
            Game.queueGroundTargetsPatch.Toggle();
            save = true;
        }
        ImGuiEx.SetItemTooltip("Ground targets will insert themselves into the action queue,\ncausing them to immediately be used as soon as possible, like other OGCDs.");

        ImGui.NextColumn();

        save |= ImGui.Checkbox("Enable Instant Ground Targets", ref ReAction.Config.EnableInstantGroundTarget);
        ImGuiEx.SetItemTooltip("Ground targets will immediately place themselves at your current cursor position when a stack does not override the target.");

        ImGui.NextColumn();

        if (ImGui.Checkbox("Enable Enhanced Auto Face Target", ref ReAction.Config.EnableEnhancedAutoFaceTarget))
        {
            Game.enhancedAutoFaceTargetPatch.Toggle();
            Game.removeAutoFaceGroundTargetPatch.Toggle();
            save = true;
        }
        ImGuiEx.SetItemTooltip("Actions that don't require facing a target will no longer automatically face the target, such as healing.");

        ImGui.NextColumn();

        save |= ImGui.Checkbox("Enable Auto Dismount", ref ReAction.Config.EnableAutoDismount);
        ImGuiEx.SetItemTooltip("Automatically dismounts when an action is used, prior to using the action.");

        ImGui.NextColumn();

        save |= ImGui.Checkbox("Enable Auto Cast Cancel", ref ReAction.Config.EnableAutoCastCancel);
        ImGuiEx.SetItemTooltip("Automatically cancels casting when the target dies.");

        ImGui.NextColumn();

        save |= ImGui.Checkbox("Enable Auto Target", ref ReAction.Config.EnableAutoTarget);
        ImGuiEx.SetItemTooltip("Automatically targets the closest enemy when no target is specified for a targeted attack.");

        if (ReAction.Config.EnableAutoTarget)
        {
            ImGui.NextColumn();
            save |= ImGui.Checkbox("Enable Auto Change Target", ref ReAction.Config.EnableAutoChangeTarget);
            ImGuiEx.SetItemTooltip("Additionally targets the closest enemy when your main target is incorrect for a targeted attack.");
        }

        ImGui.NextColumn();

        save |= ImGui.Checkbox("Enable Auto Refocus Target", ref ReAction.Config.EnableAutoRefocusTarget);
        ImGuiEx.SetItemTooltip("While in duties, attempts to focus target whatever was previously focus targeted if the focus target is lost.");

        ImGui.NextColumn();

        save |= ImGui.Checkbox("Enable Auto Attacks on Spells", ref ReAction.Config.EnableSpellAutoAttacks);
        ImGuiEx.SetItemTooltip("Causes spells (and some other actions) to start using auto attacks just like weaponskills.");

        ImGui.NextColumn();

        save |= ImGui.Checkbox("Enable Camera Relative Dashes", ref ReAction.Config.EnableCameraRelativeDashes);
        ImGuiEx.SetItemTooltip("Changes dashes, such as En Avant and Elusive Jump, to be relative\nto the direction your camera is facing, rather than your character.");

        if (ReAction.Config.EnableCameraRelativeDashes)
        {
            ImGui.NextColumn();
            save |= ImGui.Checkbox("Enable Normal Backward Dashes", ref ReAction.Config.EnableNormalBackwardDashes);
            ImGuiEx.SetItemTooltip("Ignores \"Enable Camera Relative Dashes\" for any backward dash, such as Elusive Jump.");
        }

        ImGui.NextColumn();

        save |= ImGui.Checkbox("Enable Queuing More", ref ReAction.Config.EnableQueuingMore);
        ImGuiEx.SetItemTooltip("Allows sprint, items and LBs to be queued.");

        ImGui.NextColumn();

        save |= ImGui.Checkbox("Always Queue Macros", ref ReAction.Config.EnableMacroQueue);
        ImGuiEx.SetItemTooltip("All macros will behave as if /macroqueue was used.");

        ImGui.NextColumn();

        if (ImGui.Checkbox("Enable Decimal Waits (Fractionality)", ref ReAction.Config.EnableFractionality))
        {
            if (!DalamudApi.PluginInterface.PluginNames.Contains("Fractionality") || !ReAction.Config.EnableFractionality)
            {
                Game.waitSyntaxDecimalPatch.Toggle();
                Game.waitCommandDecimalPatch.Toggle();
                save = true;
            }
            else
            {
                ReAction.Config.EnableFractionality = false;
                ReAction.PrintError("Please disable and delete Fractionality by using the trashcan icon on the plugin installer before enabling this!");
            }
        }
        ImGuiEx.SetItemTooltip("Allows decimals in wait commands and removes the 60 seconds cap (e.g. <wait.0.5> or /wait 0.5).");

        ImGui.NextColumn();

        save |= ImGui.Checkbox("Enable Frame Alignment", ref ReAction.Config.EnableFrameAlignment);
        ImGuiEx.SetItemTooltip("Aligns the game's frames with the GCD and animation lock.\nNote: This option will cause an almost unnoticeable stutter when either of these timers ends.");

        ImGui.Columns(1);
        ImGui.Separator();
        ImGui.Columns(2, null, false);

        ImGui.Indent();
        ImGui.TextUnformatted("Decombos");
        ImGui.Unindent();

        ImGui.NextColumn();
        ImGui.NextColumn();

        if (ImGui.Checkbox("Enable Uncombo'd Meditation", ref ReAction.Config.EnableDecomboMeditation))
        {
            Game.decomboMeditationPatch.Toggle();
            save = true;
        }
        ImGuiEx.SetItemTooltip("Removes the Meditation <-> Steel Peak / Forbidden Chakra combo. You will need to use\nthe hotbar feature below to place one of them on your hotbar in order to use them again.\nSteel Peak ID: 25761\nForbidden Chakra ID: 3547");

        ImGui.NextColumn();

        if (ImGui.Checkbox("Enable Uncombo'd Bunshin", ref ReAction.Config.EnableDecomboBunshin))
        {
            Game.decomboBunshinPatch.Toggle();
            save = true;
        }
        ImGuiEx.SetItemTooltip("Removes the Bunshin <-> Phantom Kamaitachi combo. You will need to use\nthe hotbar feature below to place it on your hotbar in order to use it again.\nPhantom Kamaitachi ID: 25774");

        ImGui.NextColumn();

        if (ImGui.Checkbox("Enable Uncombo'd Wanderer's Minuet", ref ReAction.Config.EnableDecomboWanderersMinuet))
        {
            Game.decomboWanderersMinuetPatch.Toggle();
            save = true;
        }
        ImGuiEx.SetItemTooltip("Removes the Wanderer's Minuet -> Pitch Perfect combo. You will need to use\nthe hotbar feature below to place it on your hotbar in order to use it again.\nPitch Perfect ID: 7404");

        ImGui.NextColumn();

        if (ImGui.Checkbox("Enable Uncombo'd Liturgy of the Bell", ref ReAction.Config.EnableDecomboLiturgy))
        {
            Game.decomboLiturgyPatch.Toggle();
            save = true;
        }
        ImGuiEx.SetItemTooltip("Removes the Liturgy of the Bell combo. You will need to use the hotbar\nfeature below to place it on your hotbar in order to use it again.\nLiturgy of the Bell (Detonate) ID: 28509");

        ImGui.NextColumn();

        if (ImGui.Checkbox("Enable Uncombo'd Earthly Star", ref ReAction.Config.EnableDecomboEarthlyStar))
        {
            Game.decomboEarthlyStarPatch.Toggle();
            save = true;
        }
        ImGuiEx.SetItemTooltip("Removes the Earthly Star combo. You will need to use the hotbar\nfeature below to place it on your hotbar in order to use it again.\nStellar Detonation ID: 8324");

        ImGui.Columns(1);

        ImGui.Spacing();
        ImGui.Separator();

        ImGui.Indent();
        ImGui.TextUnformatted("Place on Hotbar (HOVER ME FOR INFORMATION)");
        ImGuiEx.SetItemTooltip("This will allow you to place various things on the hotbar that you can't normally." +
            "\nIf you don't know what this can be used for, don't touch it. Whatever you place on it MUST BE MOVED OR ELSE IT WILL NOT SAVE." +
            "\nSome examples of things you can do:" +
            "\n\tPlace a certain action on the hotbar to be used with one of the \"Decombo\" features. The IDs are in each setting's tooltip." +
            "\n\tPlace a certain doze and sit emote on the hotbar (88 and 95)." +
            "\n\tPlace a currency (Item, 1-99) on the hotbar to see how much you have without opening the currency menu." +
            "\n\tRevive flying mount roulette (GeneralAction, 24).");
        ImGui.Unindent();
        ImGui.Columns(5, null, false);
        ImGui.Combo("Bar", ref hotbar, "1\02\03\04\05\06\07\08\09\010\0XHB 1\0XHB 2\0XHB 3\0XHB 4\0XHB 5\0XHB 6\0XHB 7\0XHB 8");
        ImGui.NextColumn();
        ImGui.Combo("Slot", ref hotbarSlot, "1\02\03\04\05\06\07\08\09\010\011\012\013\014\015\016");
        ImGui.NextColumn();
        var hotbarSlotType = Enum.GetName(typeof(HotbarSlotType), commandType) ?? commandType.ToString();
        if (ImGui.BeginCombo("Type", hotbarSlotType))
        {
            for (int i = 1; i <= 32; i++)
            {
                if (!ImGui.Selectable($"{Enum.GetName(typeof(HotbarSlotType), i) ?? i.ToString()}##{i}", commandType == i)) continue;
                commandType = i;
            }
            ImGui.EndCombo();
        }
        ImGui.NextColumn();
        ImGui.InputInt("ID", ref commandID);
        ImGui.NextColumn();
        if (ImGui.Button("Execute"))
        {
            Game.SetHotbarSlot(hotbar, hotbarSlot, (byte)commandType, (uint)commandID);
            ReAction.PrintEcho("MAKE SURE TO MOVE WHATEVER YOU JUST PLACED ON THE HOTBAR OR IT WILL NOT SAVE. YES, MOVING IT TO ANOTHER SLOT AND THEN MOVING IT BACK IS FINE.");
        }
        ImGuiEx.SetItemTooltip("You need to move whatever you place on the hotbar in order to have it save.");
        ImGui.Columns(1);

        if (save)
            ReAction.Config.Save();
    }
}