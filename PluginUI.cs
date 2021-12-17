using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;

namespace ReAction
{
    public static class PluginUI
    {
        public static bool isVisible = true;
        private static int selectedStack = -1;
        private static Configuration.ActionStack CurrentStack => 0 <= selectedStack && selectedStack < ReAction.Config.ActionStacks.Count ? ReAction.Config.ActionStacks[selectedStack] : null;

        public static void Draw()
        {
            if (!isVisible) return;

            ImGui.SetNextWindowSizeConstraints(new Vector2(700, 660) * ImGuiHelpers.GlobalScale, new Vector2(9999));
            ImGui.Begin("ReAction Configuration", ref isVisible);

            if (ImGui.BeginTabBar("ReActionTabs"))
            {
                if (ImGui.BeginTabItem("Config"))
                {
                    DrawStackList();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Other Settings"))
                {
                    DrawOtherSettings();
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

            if (ImGui.Button(FontAwesomeIcon.PlusCircle.ToIconString()))
            {
                ReAction.Config.ActionStacks.Add(new() { Name = "New Stack" });
                ReAction.Config.Save();
            }

            ImGui.SameLine();

            if (ImGui.Button(FontAwesomeIcon.ArrowCircleUp.ToIconString()) && hasSelectedStack)
            {
                var preset = CurrentStack;
                ReAction.Config.ActionStacks.RemoveAt(selectedStack);

                selectedStack = Math.Max(selectedStack - 1, 0);

                ReAction.Config.ActionStacks.Insert(selectedStack, preset);
                ReAction.Config.Save();
            }

            ImGui.SameLine();

            if (ImGui.Button(FontAwesomeIcon.ArrowCircleDown.ToIconString()) && hasSelectedStack)
            {
                var preset = CurrentStack;
                ReAction.Config.ActionStacks.RemoveAt(selectedStack);

                selectedStack = Math.Min(selectedStack + 1, ReAction.Config.ActionStacks.Count);

                ReAction.Config.ActionStacks.Insert(selectedStack, preset);
                ReAction.Config.Save();
            }

            ImGui.SameLine();

            ImGui.Button(FontAwesomeIcon.TimesCircle.ToIconString());
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

            ImGui.BeginChild("ReActionPresetList", new Vector2(250 * ImGuiHelpers.GlobalScale, 0), true);

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

            ImGui.SameLine();
            ImGui.BeginChild("ReActionStackEditor", ImGui.GetContentRegionAvail(), true);
            DrawStackEditor(currentStack);
            ImGui.EndChild();
        }

        private static void DrawStackEditor(Configuration.ActionStack stack)
        {
            if (ImGui.InputText("Name", ref stack.Name, 64))
                ReAction.Config.Save();

            if (ImGui.Checkbox("Block Original on Stack Fail", ref stack.BlockOriginal))
                ReAction.Config.Save();

            ImGui.Spacing();

            var contentRegion = ImGui.GetContentRegionAvail();
            ImGui.BeginChild("ReActionActionEditor", new Vector2(contentRegion.X, contentRegion.Y / 2), true);
            DrawActionEditor(stack);
            ImGui.EndChild();

            ImGui.BeginChild("ReActionItemEditor", ImGui.GetContentRegionAvail(), true);
            DrawItemEditor(stack);
            ImGui.EndChild();
        }

        private static void DrawActionEditor(Configuration.ActionStack stack)
        {
            var buttonWidth = ImGui.GetContentRegionAvail().X / 2;
            for (int i = 0; i < stack.Actions.Count; i++)
            {
                ImGui.PushID(i);

                var action = stack.Actions[i];

                ImGui.SetNextItemWidth(buttonWidth);
                ActionComboBox(ref action.ID, false);

                ImGui.SameLine();

                if (ImGui.Checkbox("Adjust ID", ref action.UseAdjustedID))
                    ReAction.Config.Save();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Allows the action to match any other action that it transforms into. E.g. Aero will match Dia and vice versa." +
                        "\nEnable this for skills that upgrade. Disable this for compatibility with certain XIVCombos.");

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
        }

        private static void DrawItemEditor(Configuration.ActionStack stack)
        {
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
                ActionComboBox(ref item.ID, true);

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
        }

        private static string FormatActionRowName(Lumina.Excel.GeneratedSheets.Action a) => $"[#{a.RowId} {a.ClassJob.Value?.Abbreviation}{(a.IsPvP ? " PVP" : string.Empty)}] {a.Name}";

        private static string search = string.Empty;
        private static void ActionComboBox(ref uint option, bool allowSameSkill)
        {
            var selected = option == 0 ? "Same Skill" : ReAction.actionSheet.TryGetValue(option, out var a) ? FormatActionRowName(a) : option.ToString();
            if (!ImGui.BeginCombo("##Action", selected))
                return;

            ImGui.InputText("##ActionSearch", ref search, 64);

            if (allowSameSkill && ImGui.Selectable("Same Skill", option == 0))
            {
                option = 0;
                ReAction.Config.Save();
            }

            var doSearch = !string.IsNullOrEmpty(search);
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

        private static void AddActionList(List<Configuration.Action> actions, float buttonWidth)
        {
            ImGui.PushFont(UiBuilder.IconFont);

            if (ImGui.Button(FontAwesomeIcon.PlusCircle.ToIconString(), new Vector2(buttonWidth, 0)))
                ImGui.OpenPopup("ReActionAddSkillsPopup");

            ImGui.PopFont();

            ImGui.SetNextWindowSize(new Vector2(0, 200 * ImGuiHelpers.GlobalScale));

            if (!ImGui.BeginPopup("ReActionAddSkillsPopup")) return;

            ImGui.InputText("##ActionSearch", ref search, 64);

            var doSearch = !string.IsNullOrEmpty(search);
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

        private static void DrawOtherSettings()
        {
            ImGui.Columns(2, null, false);

            var save = false;

            if (ImGui.Checkbox("Enable Ground Target Queuing", ref ReAction.Config.EnableGroundTargetQueuing))
            {
                Game.queueGroundTargetsReplacer.Toggle();
                save = true;
            }

            ImGui.NextColumn();

            save |= ImGui.Checkbox("Enable Instant Ground Targets", ref ReAction.Config.EnableInstantGroundTarget);

            ImGui.NextColumn();

            if (ImGui.Checkbox("Enhanced Auto Face Target", ref ReAction.Config.EnhancedAutoFaceTarget))
            {
                Game.enhancedAutoFaceTargetReplacer.Toggle();
                save = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Actions that don't require facing a target will no longer automatically face the target.");

            ImGui.Columns(1);

            if (save)
                ReAction.Config.Save();
        }
    }
}
