using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
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

        private static bool isMountActionQueued = false;
        private static (uint actionType, uint actionID, long targetedActorID, uint useType, int pvp) queuedMountAction;

        public static byte OnUseAction(ActionManager* actionManager, uint actionType, uint actionID, long targetedActorID, uint param, uint useType, int pvp, IntPtr a8)
        {
            try
            {
                if (actionType == 1 && useType == 0)
                {
                    foreach (var stack in ReAction.Config.ActionStacks)
                    {
                        var adjustedActionID = actionManager->GetAdjustedActionId(actionID);
                        if (stack.Actions.FirstOrDefault(action => action.ID == 0 || (action.UseAdjustedID ? actionManager->GetAdjustedActionId(action.ID) : action.ID) == adjustedActionID) == null) continue;

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
            }
            catch (Exception e)
            {
                PluginLog.Error($"Failed to modify action\n{e}");
            }

            if (ReAction.Config.EnableAutoDismount && TryDismount(actionType, actionID, targetedActorID, useType, pvp, out var ret))
                return ret;

            ret = Game.UseActionHook.Original(actionManager, actionType, actionID, targetedActorID, param, useType, pvp, a8);

            if (ReAction.Config.EnableInstantGroundTarget)
                CheckInstantGroundTarget(actionType, useType);

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

        private static bool TryDismount(uint actionType, uint actionID, long targetedActorID, uint useType, int pvp, out byte ret)
        {
            ret = 0;

            if (actionType == 1 && ReAction.mountActionsSheet.ContainsKey(actionID)
                || (actionType != 5 || actionID is not (3 or 4)) && (actionType != 1 || actionID is 5 or 6) // Limit Break / Sprint / Teleport / Return
                || !DalamudApi.Condition[ConditionFlag.Mounted])
                return false;

            isMountActionQueued = true;
            queuedMountAction = (actionType, actionID, targetedActorID, useType, pvp);
            ret = Game.UseActionHook.Original(Game.actionManager, 5, 23, 0, 0, 0, 0, IntPtr.Zero);
            return true;
        }

        private static void CheckInstantGroundTarget(uint actionType, uint useType)
        {
            if ((useType != 2 || actionType != 1) && actionType != 15)
                *(byte*)((IntPtr)Game.actionManager + 0xB8) = 1;
        }

        public static void Update()
        {
            if (!isMountActionQueued || DalamudApi.Condition[ConditionFlag.Mounted]) return;

            Game.UseActionHook.Original(Game.actionManager, queuedMountAction.actionType, queuedMountAction.actionID,
                queuedMountAction.targetedActorID, 0, queuedMountAction.useType, queuedMountAction.pvp, IntPtr.Zero);

            if (ReAction.Config.EnableInstantGroundTarget)
                CheckInstantGroundTarget(queuedMountAction.actionType, queuedMountAction.useType);

            isMountActionQueued = false;
        }
    }
}
