using System;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace ReAction;

// TODO: for the love of god why am i still putting every feature in this one fucking file
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
        P8,
        LowestHPPartyMember
    }

    private static bool isMountActionQueued = false;
    private static (uint actionType, uint actionID, long targetObjectID, uint useType, int pvp) queuedMountAction;
    private static readonly Stopwatch mountActionTimer = new();

    private static bool canceledCast = false;

    private static long queuedGroundTargetObjectID = 0;
    private static bool queuedItem = false;

    private static readonly Stopwatch timer = new();

    public static byte OnUseAction(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
    {
        try
        {
            if (DalamudApi.ClientState.LocalPlayer == null) return 0;

            if (queuedItem && useType == 1)
            {
                PluginLog.Debug("Applying queued item param");

                param = 65535;
                queuedItem = false;
            }
            else if (ReAction.Config.EnableQueuingMore && actionType == 5 && actionID == 4 && useType == 1)
            {
                actionType = 1;
                actionID = 3;
                targetObjectID = DalamudApi.ClientState.LocalPlayer.ObjectId;
            }

            var adjustedActionID = actionType == 1 ? actionManager->GetAdjustedActionId(actionID) : actionID;

            PluginLog.Debug($"UseAction called {actionType}, {actionID} -> {adjustedActionID}, {targetObjectID:X}, {param}, {useType}, {pvp}");

            if (ReAction.Config.EnableAutoDismount && TryDismount(actionType, adjustedActionID, targetObjectID, useType, pvp, out var ret))
                return ret;

            var succeeded = false;
            if (actionType == 1 && useType == 0 && ReAction.actionSheet.ContainsKey(adjustedActionID))
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
                               || (action.UseAdjustedID ? actionManager->GetAdjustedActionId(action.ID) : action.ID) == adjustedActionID))
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

            if (ReAction.Config.EnableAutoTarget && actionType == 1 && TryTabTarget(adjustedActionID, targetObjectID, out var newObjectID))
                targetObjectID = newObjectID;

            if (ReAction.Config.EnableCameraRelativeDashes)
                TryDashFromCamera(actionType, adjustedActionID);

            if (ReAction.Config.EnableQueuingMore && useType == 0)
                TryEnablingQueuing(actionType, adjustedActionID);

            ret = Game.UseActionHook.Original(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);

            if (Game.allowQueuingReplacer.IsEnabled)
                Game.allowQueuingReplacer.Disable();

            if (queuedItem && !Game.IsQueued)
                queuedItem = false;

            if (succeeded && ReAction.actionSheet[adjustedActionID].TargetArea)
            {
                *(long*)((IntPtr)Game.actionManager + 0x98) = targetObjectID;
                queuedGroundTargetObjectID = targetObjectID;
            }
            else if (useType == 1 && queuedGroundTargetObjectID != 0)
            {
                *(long*)((IntPtr)Game.actionManager + 0x98) = queuedGroundTargetObjectID;
                queuedGroundTargetObjectID = 0;
            }
            else
            {
                queuedGroundTargetObjectID = 0;
            }

            if (ReAction.Config.EnableInstantGroundTarget && !succeeded && queuedGroundTargetObjectID == 0)
                TryInstantGroundTarget(actionType, useType);

            return ret;
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
            var newID = item.ID != 0 ? Game.actionManager->GetAdjustedActionId(item.ID) : id;
            var newTarget = GetTarget(item.Target);
            if (newTarget == null || !CanUseAction(newID, newTarget) || useRange && Game.IsActionOutOfRange(newID, newTarget) || useCooldown && !Game.CanActionQueue(1, newID)) continue;

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
                return Game.GetGameObjectFromPronounID(1004);
            case TargetType.UITarget:
                return Game.UITarget;
            case TargetType.FieldTarget:
                o = DalamudApi.TargetManager.MouseOverTarget;
                break;
            case TargetType.TargetsTarget:
                return Game.GetGameObjectFromPronounID(1002);
            case TargetType.Self:
                return Game.GetGameObjectFromPronounID(1014);
            case TargetType.LastTarget:
                return Game.GetGameObjectFromPronounID(1006);
            case TargetType.LastEnemy:
                return Game.GetGameObjectFromPronounID(1084);
            case TargetType.LastAttacker:
                return Game.GetGameObjectFromPronounID(1008);
            case TargetType.P2:
                return Game.GetGameObjectFromPronounID(44);
            case TargetType.P3:
                return Game.GetGameObjectFromPronounID(45);
            case TargetType.P4:
                return Game.GetGameObjectFromPronounID(46);
            case TargetType.P5:
                return Game.GetGameObjectFromPronounID(47);
            case TargetType.P6:
                return Game.GetGameObjectFromPronounID(48);
            case TargetType.P7:
                return Game.GetGameObjectFromPronounID(49);
            case TargetType.P8:
                return Game.GetGameObjectFromPronounID(50);
            case TargetType.LowestHPPartyMember:
                return GetTargetWithLowestHP();
        }

        return o != null ? (GameObject*)o.Address : null;
    }

    private static GameObject* GetTargetWithLowestHP()
    {
        try 
        {
            return Game.GetGameObjectFromObjectID(
                DalamudApi.PartyList
                    .Where(member => member.CurrentHP > 0)
                    .Where(member => member.CurrentHP < member.MaxHP)
                    .MinBy(member => member.CurrentHP)
                    .ObjectId
            );
        }
        catch (Exception e)
        {
            // When party is not formed.
            return GetTarget(TargetType.Self);
        }
    }

    private static bool CanUseAction(uint id, GameObject* target)
        => Game.CanUseActionOnGameObject(id, target) && Game.actionManager->GetActionStatus(ActionType.Spell, id, target->ObjectID, 0, 0) == 0;

    private static bool TryDismount(uint actionType, uint actionID, long targetObjectID, uint useType, int pvp, out byte ret)
    {
        ret = 0;

        if (!DalamudApi.Condition[ConditionFlag.Mounted]
            || actionType == 1 && ReAction.mountActionsSheet.ContainsKey(actionID)
            || (actionType != 5 || actionID is not (3 or 4)) && (actionType != 1 || actionID is 5 or 6) // +Limit Break / +Sprint / -Teleport / -Return
            || Game.actionManager->GetActionStatus((ActionType)actionType, actionID, targetObjectID, 0, 0) == 0)
            return false;

        ret = Game.UseActionHook.Original(Game.actionManager, 5, 23, 0, 0, 0, 0, null);
        if (ret == 0) return true;

        PluginLog.Debug($"Dismounting {actionType}, {actionID}, {targetObjectID:X}, {useType}, {pvp}");

        isMountActionQueued = true;
        queuedMountAction = (actionType, actionID, targetObjectID, useType, pvp);
        mountActionTimer.Restart();
        return true;
    }

    private static bool TryTabTarget(uint actionID, long objectID, out long newObjectID)
    {
        newObjectID = 0;
        var targetObject = DalamudApi.TargetManager.Target is { } t ? (GameObject*)t.Address : null;
        if (!ReAction.Config.EnableAutoChangeTarget && targetObject != null
            || objectID != 0xE0000000 && Game.GetGameObjectFromObjectID(objectID) != targetObject
            || Game.CanUseActionOnGameObject(actionID, targetObject)
            || !ReAction.actionSheet.TryGetValue(actionID, out var a)
            || !a.CanTargetHostile)
            return false;

        PluginLog.Debug($"Attempting to swap target {actionID}, {objectID:X}");

        Game.TargetEnemy();
        if (DalamudApi.TargetManager.Target is not { } target) return false;

        newObjectID = Game.GetObjectID((GameObject*)target.Address);

        PluginLog.Debug($"Target swapped {objectID:X} -> {newObjectID:X}");

        return true;
    }

    private static void TryDashFromCamera(uint actionType, uint actionID)
    {
        if (!ReAction.actionSheet.TryGetValue(actionID, out var a)
            || !a.AffectsPosition
            || !a.CanTargetSelf
            || a.BehaviourType <= 1
            || ReAction.Config.EnableNormalBackwardDashes && a.BehaviourType is 3 or 4
            || Game.actionManager->GetActionStatus((ActionType)actionType, actionID) != 0
            || Game.AnimationLock != 0)
            return;

        PluginLog.Debug($"Rotating camera {actionType}, {actionID}");

        Game.SetCharacterRotationToCamera();
    }

    private static void TryEnablingQueuing(uint actionType, uint actionID)
    {
        if ((actionType != 5 || actionID != 4) && actionType != 2) return;

        PluginLog.Debug($"Enabling queuing {actionType}, {actionID}");

        Game.allowQueuingReplacer.Enable();
        queuedItem = actionType == 2;
    }

    private static void TryInstantGroundTarget(uint actionType, uint useType)
    {
        if (useType == 2 && actionType == 1 || actionType == 15) return;

        PluginLog.Debug($"Making ground target instant {actionType}, {useType}");

        *(byte*)((IntPtr)Game.actionManager + 0xB8) = 1;
    }

    private static void TryQueuedMountAction()
    {
        if (DalamudApi.Condition[ConditionFlag.Mounted]) return;

        if (mountActionTimer.ElapsedMilliseconds <= 2000)
        {
            PluginLog.Debug("Using queued mount action");

            OnUseAction(Game.actionManager, queuedMountAction.actionType, queuedMountAction.actionID,
                queuedMountAction.targetObjectID, 0, queuedMountAction.useType, queuedMountAction.pvp, null);
        }

        isMountActionQueued = false;
        mountActionTimer.Stop();
    }

    private static void TryCancelingCast()
    {
        if (canceledCast
            || Game.CastActionType != 1
            || !ReAction.actionSheet.TryGetValue(Game.CastActionID, out var a)
            || a.TargetArea)
            return;

        var o = Game.GetGameObjectFromObjectID(Game.CastTargetID);
        if (o == null || Game.CanUseActionOnGameObject(Game.CastActionID, o)) return;

        PluginLog.Debug($"Cancelling cast {Game.CastActionType}, {Game.CastActionID}, {Game.CastTargetID:X}");

        Game.CancelCast();
        canceledCast = true;
    }

    public static void Update()
    {
        if (isMountActionQueued)
            TryQueuedMountAction();

        if (ReAction.Config.EnableAutoCastCancel)
        {
            if (canceledCast && Game.CastActionType == 0)
                canceledCast = false;
            else
                TryCancelingCast();
        }

        if (ReAction.Config.EnableAutoRefocusTarget && DalamudApi.Condition[ConditionFlag.BoundByDuty])
            Game.RefocusTarget();

        if (!ReAction.Config.EnableFPSAlignment) return;

        if (timer.IsRunning && Game.IsQueued)
        {
            var elapsedTime = timer.ElapsedTicks / (double)Stopwatch.Frequency;
            var remainingAnimationLock = Game.AnimationLock - elapsedTime;
            var remainingGCD = Game.GCDRecastTime - Game.ElapsedGCDRecastTime - elapsedTime;
            var blockDuration = 0d;

            if (remainingAnimationLock > 0 && remainingAnimationLock <= elapsedTime * 1.1)
                blockDuration = Math.Round(remainingAnimationLock * Stopwatch.Frequency);

            if (remainingGCD > 0 && remainingGCD <= elapsedTime * 1.1)
            {
                var newBlockDuration = Math.Round(remainingGCD * Stopwatch.Frequency);
                if (newBlockDuration > blockDuration)
                    blockDuration = newBlockDuration;
            }

            if (blockDuration > 0)
            {
                PluginLog.Debug($"Blocking main thread for {blockDuration / Stopwatch.Frequency * 1000} ms");

                timer.Restart();
                while (timer.ElapsedTicks < blockDuration) ;
            }
        }

        timer.Restart();
    }
}