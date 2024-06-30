using System;
using System.Linq;
using ActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Hypostasis.Game.Structures;

namespace ReAction;

public static unsafe class ActionStackManager
{
    public delegate void PreUseActionEventDelegate(ActionManager* actionManager, ref uint actionType, ref uint actionID, ref ulong targetObjectID, ref uint param, ref uint useType, ref int pvp);
    public static event PreUseActionEventDelegate PreUseAction;
    public delegate void PreActionStackDelegate(ActionManager* actionManager, ref uint actionType, ref uint actionID, ref uint adjustedActionID, ref ulong targetObjectID, ref uint param, uint useType, ref int pvp, out bool? ret);
    public static event PreActionStackDelegate PreActionStack;
    public delegate void PostActionStackDelegate(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ref ulong targetObjectID, uint param, uint useType, int pvp);
    public static event PostActionStackDelegate PostActionStack;
    public delegate void PostUseActionDelegate(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ulong targetObjectID, uint param, uint useType, int pvp, bool ret);
    public static event PostUseActionDelegate PostUseAction;

    private static ulong queuedGroundTargetObjectID = 0;

    public static Bool OnUseAction(ActionManager* actionManager, uint actionType, uint actionID, ulong targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
    {
        try
        {
            if (DalamudApi.ClientState.LocalPlayer == null) return 0;

            var tryStack = useType == 0;
            if (useType == 100)
            {
                useType = 0;
                DalamudApi.LogDebug("UseAction called from a macro using /macroqueue");
            }

            PreUseAction?.Invoke(actionManager, ref actionType, ref actionID, ref targetObjectID, ref param, ref useType, ref pvp);

            var adjustedActionID = actionType == 1 ? actionManager->CS.GetAdjustedActionId(actionID) : actionID;

            DalamudApi.LogDebug($"UseAction called {actionType}, {actionID} -> {adjustedActionID}, {targetObjectID:X}, {param}, {useType}, {pvp}");

            bool? ret = null;
            PreActionStack?.Invoke(actionManager, ref actionType, ref actionID, ref adjustedActionID, ref targetObjectID, ref param, useType, ref pvp, out ret);
            if (ret.HasValue)
                return ret.Value;

            var succeeded = false;
            if (PluginModuleManager.GetModule<Modules.ActionStacks>().IsValid && tryStack && actionType == 1 && ReAction.actionSheet.TryGetValue(adjustedActionID, out var a))
            {
                DalamudApi.LogDebug("Checking stacks");

                var modifierKeys = GetModifierKeys();
                foreach (var stack in ReAction.Config.ActionStacks)
                {
                    var exactMatch = (stack.ModifierKeys & 8) != 0;
                    if (exactMatch ? stack.ModifierKeys != modifierKeys : (stack.ModifierKeys & modifierKeys) != stack.ModifierKeys) continue;
                    if (!stack.Actions.Any(action
                            => action.ID == 0
                               || action.ID == 1 && a.CanTargetHostile
                               || action.ID == 2 && (a.CanTargetFriendly || a.CanTargetParty)
                               || (action.UseAdjustedID ? actionManager->CS.GetAdjustedActionId(action.ID) : action.ID) == adjustedActionID))
                        continue;

                    if (!CheckActionStack(adjustedActionID, stack, out var newAction, out var newTarget))
                    {
                        if (stack.BlockOriginal)
                        {
                            DalamudApi.LogDebug("Stack failed, blocking original");
                            return 0;
                        }
                        break;
                    }

                    DalamudApi.LogDebug($"Stack succeeded {adjustedActionID} -> {newAction}, {targetObjectID:X} -> {newTarget:X}");

                    actionID = newAction;
                    adjustedActionID = newAction;
                    targetObjectID = newTarget;
                    succeeded = true;
                    break;
                }
            }

            PostActionStack?.Invoke(actionManager, actionType, actionID, adjustedActionID, ref targetObjectID, param, useType, pvp);

            ret = Game.UseActionHook.Original(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);

            PostUseAction?.Invoke(actionManager, actionType, actionID, adjustedActionID, targetObjectID, param, useType, pvp, ret!.Value);

            if (succeeded && ReAction.actionSheet[adjustedActionID].TargetArea)
            {
                actionManager->queuedGroundTargetObjectID = targetObjectID;
                queuedGroundTargetObjectID = targetObjectID;
            }
            else if (useType == 1 && queuedGroundTargetObjectID != 0)
            {
                actionManager->queuedGroundTargetObjectID = queuedGroundTargetObjectID;
                queuedGroundTargetObjectID = 0;
            }
            else
            {
                queuedGroundTargetObjectID = 0;
            }

            if (ReAction.Config.EnableInstantGroundTarget && !succeeded && queuedGroundTargetObjectID == 0)
                SetInstantGroundTarget(actionType, useType);

            return ret!.Value;
        }
        catch (Exception e)
        {
            DalamudApi.LogError($"Failed to modify action\n{e}");
            return 0;
        }
    }

    private static uint GetModifierKeys()
    {
        var keys = 8u;
        if (DalamudApi.KeyState[16]) // Shift
            keys |= 1;
        if (DalamudApi.KeyState[17]) // Ctrl
            keys |= 2;
        if (DalamudApi.KeyState[18]) // Alt
            keys |= 4;
        return keys;
    }

    private static bool CheckActionStack(uint id, Configuration.ActionStack stack, out uint action, out ulong target)
    {
        action = 0;
        target = Game.InvalidObjectID;

        var useRange = stack.CheckRange;
        var useCooldown = stack.CheckCooldown;
        foreach (var item in stack.Items)
        {
            var newID = item.ID != 0 ? Common.ActionManager->CS.GetAdjustedActionId(item.ID) : id;
            var newTarget = PronounManager.GetGameObjectFromID(item.TargetID);
            if (newTarget == null || !CanUseAction(newID, newTarget) || useRange && Game.IsActionOutOfRange(newID, newTarget) || useCooldown && !Common.ActionManager->CanQueueAction(1, newID)) continue;

            action = newID;
            target = Game.GetObjectID(newTarget);
            return true;
        }

        return false;
    }

    private static bool CanUseAction(uint id, GameObject* target)
        => ActionManager.CanUseActionOnGameObject(id, target) && Common.ActionManager->CS.GetActionStatus(ActionType.Action, id, target->GetGameObjectId(), false, false) == 0;

    private static void SetInstantGroundTarget(uint actionType, uint useType)
    {
        if ((ReAction.Config.EnableBlockMiscInstantGroundTargets && actionType == 11) || useType == 2 && actionType == 1 || actionType == 15) return;

        DalamudApi.LogDebug($"Making ground target instant {actionType}, {useType}");

        Common.ActionManager->activateGroundTarget = 1;
    }
}