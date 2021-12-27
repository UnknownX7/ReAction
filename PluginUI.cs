using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;

namespace ReAction
{
    public static class PluginUI
    {
        public static bool isVisible = false;
        private static int selectedStack = -1;
        private static string search = string.Empty;

        private static Configuration.ActionStack CurrentStack => 0 <= selectedStack && selectedStack < ReAction.Config.ActionStacks.Count ? ReAction.Config.ActionStacks[selectedStack] : null;

        public static void Draw()
        {
            if (!isVisible) return;

            ImGui.SetNextWindowSizeConstraints(new Vector2(700, 660) * ImGuiHelpers.GlobalScale, new Vector2(9999));
            ImGui.Begin("ReAction Configuration", ref isVisible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

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
            SetItemTooltip("Export stack to clipboard.");
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
            SetItemTooltip("Import stack from clipboard.");
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
            DrawStackEditorLists (currentStack);
            ImGui.EndChild();
        }

        private static void DrawStackEditorMain(Configuration.ActionStack stack)
        {
            var save = false;

            save |= ImGui.InputText("Name", ref stack.Name, 64);
            save |= ImGui.CheckboxFlags("##Shift", ref stack.ModifierKeys, 1);
            SetItemTooltip("Shift");
            ImGui.SameLine();
            save |= ImGui.CheckboxFlags("##Ctrl", ref stack.ModifierKeys, 2);
            SetItemTooltip("Control");
            ImGui.SameLine();
            save |= ImGui.CheckboxFlags("##Alt", ref stack.ModifierKeys, 4);
            SetItemTooltip("Alt");
            ImGui.SameLine();
            save |= ImGui.CheckboxFlags("##Exact", ref stack.ModifierKeys, 8);
            SetItemTooltip("Match exactly these modifiers. E.g. Shift + Control ticked will match Shift + Control held, but not Shift + Control + Alt held.");
            ImGui.SameLine();
            ImGui.TextUnformatted("Modifier Keys");
            save |= ImGui.Checkbox("Block Original on Stack Fail", ref stack.BlockOriginal);
            save |= ImGui.Checkbox("Fail if Out of Range", ref stack.CheckRange);

            if (save)
                ReAction.Config.Save();
        }

        private static void DrawStackEditorLists(Configuration.ActionStack stack)
        {
            DrawActionEditor(stack);
            DrawItemEditor(stack);
        }

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
                ActionComboBox(ref action.ID, "All Actions");

                ImGui.SameLine();

                if (ImGui.Checkbox("Adjust ID", ref action.UseAdjustedID))
                    ReAction.Config.Save();
                var detectedAdjustment = false;
                unsafe
                {
                    if (!action.UseAdjustedID && (detectedAdjustment = Game.actionManager->GetAdjustedActionId(action.ID) != action.ID))
                        ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), 0x2000FF30, ImGui.GetStyle().FrameRounding);
                }
                SetItemTooltip("Allows the action to match any other action that it transforms into." +
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
                ActionComboBox(ref item.ID, "Same Action");

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

        private static string FormatActionRowName(Lumina.Excel.GeneratedSheets.Action a) => $"[#{a.RowId} {a.ClassJob.Value?.Abbreviation}{(a.IsPvP ? " PVP" : string.Empty)}] {a.Name}";

        private static void ActionComboBox(ref uint option, string initialOption)
        {
            var selected = option == 0 ? initialOption : ReAction.actionSheet.TryGetValue(option, out var a) ? FormatActionRowName(a) : option.ToString();
            if (!ImGui.BeginCombo("##Action", selected))
                return;

            ImGui.InputText("##ActionSearch", ref search, 64);

            var doSearch = !string.IsNullOrEmpty(search);

            if ((!doSearch || initialOption.Contains(search, StringComparison.CurrentCultureIgnoreCase)) && ImGui.Selectable(initialOption, option == 0))
            {
                option = 0;
                ReAction.Config.Save();
            }

            foreach (var (id, row) in ReAction.actionSheet)
            {
                var name = FormatActionRowName(row);
                if (doSearch && !name.Contains(search, StringComparison.CurrentCultureIgnoreCase)) continue;

                ImGui.PushID((int)id);

                if (ImGui.Selectable(name, option == id))
                {
                    option = id;
                    ReAction.Config.Save();
                }

                ImGui.PopID();
            }

            ImGui.EndCombo();
        }

        private static void AddActionList(ICollection<Configuration.Action> actions, float buttonWidth)
        {
            ImGui.PushFont(UiBuilder.IconFont);

            if (ImGui.Button(FontAwesomeIcon.PlusCircle.ToIconString(), new Vector2(buttonWidth, 0)))
                ImGui.OpenPopup("ReActionAddSkillsPopup");

            ImGui.PopFont();

            ImGui.SetNextWindowSize(new Vector2(0, 200 * ImGuiHelpers.GlobalScale));

            if (!ImGui.BeginPopup("ReActionAddSkillsPopup")) return;

            ImGui.InputText("##ActionSearch", ref search, 64);

            var doSearch = !string.IsNullOrEmpty(search);

            const string initialOption = "All Actions";
            if ((!doSearch || initialOption.Contains(search, StringComparison.CurrentCultureIgnoreCase)) && ImGui.Selectable(initialOption))
            {
                actions.Add(new() { ID = 0 });
                ReAction.Config.Save();
            }

            foreach (var (id, row) in ReAction.actionSheet)
            {
                var name = FormatActionRowName(row);
                if (doSearch && !name.Contains(search, StringComparison.CurrentCultureIgnoreCase)) continue;

                ImGui.PushID((int)id);

                if (ImGui.Selectable(name, false, ImGuiSelectableFlags.DontClosePopups))
                {
                    actions.Add(new() { ID = id });
                    ReAction.Config.Save();
                }

                ImGui.PopID();
            }

            ImGui.EndPopup();
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
                Game.queueGroundTargetsReplacer.Toggle();
                save = true;
            }
            SetItemTooltip("Ground targets will insert themselves into the action queue,\ncausing them to immediately be used as soon as possible, like other OGCDs.");

            ImGui.NextColumn();

            save |= ImGui.Checkbox("Enable Instant Ground Targets", ref ReAction.Config.EnableInstantGroundTarget);
            SetItemTooltip("Ground targets will immediately place themselves at your current cursor position when not used in a stack.");

            ImGui.NextColumn();

            if (ImGui.Checkbox("Enable Enhanced Auto Face Target", ref ReAction.Config.EnableEnhancedAutoFaceTarget))
            {
                Game.enhancedAutoFaceTargetReplacer1.Toggle();
                Game.enhancedAutoFaceTargetReplacer2.Toggle();
                save = true;
            }
            SetItemTooltip("Actions that don't require facing a target will no longer automatically face the target, such as healing.");

            ImGui.NextColumn();

            save |= ImGui.Checkbox("Enable Auto Dismount", ref ReAction.Config.EnableAutoDismount);
            SetItemTooltip("Automatically dismounts when an action is used, prior to using the action.");

            ImGui.NextColumn();

            save |= ImGui.Checkbox("Enable Auto Cast Cancel", ref ReAction.Config.EnableAutoCastCancel);
            SetItemTooltip("Automatically cancels casting when the target dies.");

            ImGui.NextColumn();

            save |= ImGui.Checkbox("Enable Auto Target", ref ReAction.Config.EnableAutoTarget);
            SetItemTooltip("Automatically uses tab target when no target is specified for a targeted attack.");

            if (ReAction.Config.EnableAutoTarget)
            {
                ImGui.NextColumn();
                save |= ImGui.Checkbox("Enable Auto Change Target", ref ReAction.Config.EnableAutoChangeTarget);
                SetItemTooltip("Additionally uses tab target when your main target is incorrect for a targeted attack.");
            }

            ImGui.NextColumn();

            if (ImGui.Checkbox("Enable Auto Attacks on Spells", ref ReAction.Config.EnableSpellAutoAttacks))
            {
                Game.spellAutoAttackReplacer.Toggle();
                save = true;
            }
            SetItemTooltip("Causes spells to start using auto attacks just like weaponskills.");

            ImGui.NextColumn();

            save |= ImGui.Checkbox("Enable Camera Relative Dashes", ref ReAction.Config.EnableCameraRelativeDashes);
            SetItemTooltip("Changes dashes, such as En Avant and Elusive Jump, to be relative\nto the direction your camera is facing, rather than your character.");

            ImGui.Columns(1);

            if (save)
                ReAction.Config.Save();
        }

        private static void SetItemTooltip(string s, ImGuiHoveredFlags flags = ImGuiHoveredFlags.None)
        {
            if (ImGui.IsItemHovered(flags))
                ImGui.SetTooltip(s);
        }
    }
}
