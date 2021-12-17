using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace ReAction
{
    public static unsafe class ActionStackManager
    {
        public enum TargetType
        {
            Target,
            SoftTarget,
            FocusTarget,
            UITarget,
            FieldTarget,
            TargetsTarget,
            Self
        }

        public static byte OnUseAction(ActionManager* actionManager, uint actionType, uint actionID, long targetedActorID, uint param, uint useType, int pvp, IntPtr a8)
        {
            try
            {
                PluginLog.Error($"{actionType} start {actionID} -> {actionManager->GetAdjustedActionId(actionID)} {targetedActorID:X} {pvp}");
                if (actionType == 1 && useType == 0)
                {
                    foreach (var stack in ReAction.Config.ActionStacks)
                    {
                        var adjustedActionID = actionManager->GetAdjustedActionId(actionID);
                        if (stack.Actions.FirstOrDefault(action => (action.UseAdjustedID ? actionManager->GetAdjustedActionId(action.ID) : action.ID) == adjustedActionID) == null) continue;

                        if (!CheckActionStack(adjustedActionID, stack.Items, out var newAction, out var newTarget))
                        {
                            if (stack.BlockOriginal)
                                return 0;
                            continue;
                        }

                        actionID = newAction;
                        targetedActorID = newTarget;
                    }
                }
                PluginLog.Error($"end {actionID} {targetedActorID:X}");
            }
            catch (Exception e)
            {
                PluginLog.Error($"Failed to modify action\n{e}");
            }

            var ret = Game.UseActionHook.Original(actionManager, actionType, actionID, targetedActorID, param, useType, pvp, a8);
            if ((useType != 2 || actionType != 1) && actionType != 15 && ReAction.Config.EnableInstantGroundTarget)
                *(byte*)((IntPtr)actionManager + 0xB8) = 1;
            return ret;
        }

        private static bool CheckActionStack(uint id, List<Configuration.ActionStackItem> stack, out uint action, out uint target)
        {
            action = 0;
            target = 0xE0000000;

            foreach (var item in stack)
            {
                var newID = item.ID != 0 ? item.ID : id;
                var newTarget = GetTarget(item.Target);
                if (newTarget == null || !CanUseAction(newID, newTarget)) continue;
                action = newID;
                target = newTarget->ObjectID;
                return true;
            }

            return false;
        }

        private static GameObject* GetTarget(TargetType target)
        {
            Dalamud.Game.ClientState.Objects.Types.GameObject o = null;

            switch (target)
            {
                case TargetType.Target:
                    o = DalamudApi.TargetManager.Target;
                    break;
                case TargetType.SoftTarget:
                    o = DalamudApi.TargetManager.SoftTarget;
                    break;
                case TargetType.FocusTarget:
                    o = DalamudApi.TargetManager.FocusTarget;
                    break;
                case TargetType.UITarget:
                    return Game.UITarget;
                case TargetType.FieldTarget:
                    o = DalamudApi.TargetManager.MouseOverTarget;
                    break;
                case TargetType.TargetsTarget when DalamudApi.TargetManager.Target is { TargetObjectId: not 0xE0000000 }:
                    o = DalamudApi.TargetManager.Target.TargetObject;
                    break;
                case TargetType.Self:
                    o = DalamudApi.ClientState.LocalPlayer;
                    break;
            }

            return o != null ? (GameObject*)o.Address : null;
        }

        private static bool CanUseAction(uint id, GameObject* target)
            => Game.CanUseActionOnObject(id, target) != 0 && Game.actionManager->GetActionStatus(ActionType.Spell, id, target->ObjectID) is 0 or 580 or 582;
    }
}
