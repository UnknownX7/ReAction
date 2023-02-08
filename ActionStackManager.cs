using System;
using System.Linq;
using Dalamud.Logging;
using ActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Hypostasis.Game.Structures;

namespace ReAction;

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
        Self,
        LastTarget,
        LastEnemy,
        LastAttacker,
        P2,
        P3,
        P4,
        P5,
        P6,
        P7,
        P8
    }

    public delegate void PreUseActionEventDelegate(ActionManager* actionManager, ref uint actionType, ref uint actionID, ref long targetObjectID, ref uint param, ref uint useType, ref int pvp);
    public static event PreUseActionEventDelegate PreUseAction;
    public delegate void PreActionStackDelegate(ActionManager* actionManager, ref uint actionType, ref uint actionID, ref uint adjustedActionID, ref long targetObjectID, ref uint param, uint useType, ref int pvp, out byte? ret);
    public static event PreActionStackDelegate PreActionStack;
    public delegate void PostActionStackDelegate(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ref long targetObjectID, uint param, uint useType, int pvp);
    public static event PostActionStackDelegate PostActionStack;
    public delegate void PostUseActionDelegate(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, long targetObjectID, uint param, uint useType, int pvp);
    public static event PostUseActionDelegate PostUseAction;

    private static long queuedGroundTargetObjectID = 0;

    public static byte OnUseAction(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
    {
        try
        {
            if (DalamudApi.ClientState.LocalPlayer == null) return 0;

            var tryStack = useType == 0;
            if (useType == 100)
            {
                useType = 0;
                PluginLog.Debug("UseAction called from a macro using /macroqueue");
            }

            PreUseAction?.Invoke(actionManager, ref actionType, ref actionID, ref targetObjectID, ref param, ref useType, ref pvp);

            var adjustedActionID = actionType == 1 ? actionManager->CS.GetAdjustedActionId(actionID) : actionID;

            PluginLog.Debug($"UseAction called {actionType}, {actionID} -> {adjustedActionID}, {targetObjectID:X}, {param}, {useType}, {pvp}");

            byte? ret = null;
            PreActionStack?.Invoke(actionManager, ref actionType, ref actionID, ref adjustedActionID, ref targetObjectID, ref param, useType, ref pvp, out ret);
            if (ret.HasValue)
                return ret.Value;

            var succeeded = false;
            if (tryStack && actionType == 1 && ReAction.actionSheet.ContainsKey(adjustedActionID))
            {
                PluginLog.Debug("Checking stacks");

                var a = ReAction.actionSheet[adjustedActionID];
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
                            PluginLog.Debug("Stack failed, blocking original");
                            return 0;
                        }
                        break;
                    }

                    PluginLog.Debug($"Stack succeeded {adjustedActionID} -> {newAction}, {targetObjectID:X} -> {newTarget:X}");

                    actionID = newAction;
                    adjustedActionID = newAction;
                    targetObjectID = newTarget;
                    succeeded = true;
                    break;
                }
            }

            PostActionStack?.Invoke(actionManager, actionType, actionID, adjustedActionID, ref targetObjectID, param, useType, pvp);

            ret = Game.UseActionHook.Original(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);

            PostUseAction?.Invoke(actionManager, actionType, actionID, adjustedActionID, targetObjectID, param, useType, pvp);

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

            return ret.Value;
        }
        catch (Exception e)
        {
            PluginLog.Error($"Failed to modify action\n{e}");
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

    private static bool CheckActionStack(uint id, Configuration.ActionStack stack, out uint action, out long target)
    {
        action = 0;
        target = 0xE0000000;

        var useRange = stack.CheckRange;
        var useCooldown = stack.CheckCooldown;
        foreach (var item in stack.Items)
        {
            var newID = item.ID != 0 ? Common.ActionManager->CS.GetAdjustedActionId(item.ID) : id;
            var newTarget = GetTarget(item.Target);
            if (newTarget == null || !CanUseAction(newID, newTarget) || useRange && Game.IsActionOutOfRange(newID, newTarget) || useCooldown && !Common.ActionManager->CanActionQueue(1, newID)) continue;

            action = newID;
            target = Game.GetObjectID(newTarget);
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
                return Common.GetGameObjectFromPronounID(1004);
            case TargetType.UITarget:
                return Common.UITarget;
            case TargetType.FieldTarget:
                o = DalamudApi.TargetManager.MouseOverTarget;
                break;
            case TargetType.TargetsTarget:
                return Common.GetGameObjectFromPronounID(1002);
            case TargetType.Self:
                return Common.GetGameObjectFromPronounID(1014);
            case TargetType.LastTarget:
                return Common.GetGameObjectFromPronounID(1006);
            case TargetType.LastEnemy:
                return Common.GetGameObjectFromPronounID(1084);
            case TargetType.LastAttacker:
                return Common.GetGameObjectFromPronounID(1008);
            case TargetType.P2:
                return Common.GetGameObjectFromPronounID(44);
            case TargetType.P3:
                return Common.GetGameObjectFromPronounID(45);
            case TargetType.P4:
                return Common.GetGameObjectFromPronounID(46);
            case TargetType.P5:
                return Common.GetGameObjectFromPronounID(47);
            case TargetType.P6:
                return Common.GetGameObjectFromPronounID(48);
            case TargetType.P7:
                return Common.GetGameObjectFromPronounID(49);
            case TargetType.P8:
                return Common.GetGameObjectFromPronounID(50);
        }

        return o != null ? (GameObject*)o.Address : null;
    }

    private static bool CanUseAction(uint id, GameObject* target)
        => ActionManager.CanUseActionOnGameObject(id, target) && Common.ActionManager->CS.GetActionStatus(ActionType.Spell, id, target->ObjectID, false, false) == 0;

    private static void SetInstantGroundTarget(uint actionType, uint useType)
    {
        if (useType == 2 && actionType == 1 || actionType == 15) return;

        PluginLog.Debug($"Making ground target instant {actionType}, {useType}");

        Common.ActionManager->activateGroundTarget = 1;
    }
}