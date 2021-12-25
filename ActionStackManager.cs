using System;
using System.Diagnostics;
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

        private static bool isMountActionQueued = false;
        private static (uint actionType, uint actionID, long targetObjectID, uint useType, int pvp) queuedMountAction;
        private static readonly Stopwatch mountActionTimer = new();

        private static bool canceledCast = false;

        private static long queuedGroundTargetObjectID = 0;

        public static byte OnUseAction(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
        {
            try
            {
                var adjustedActionID = actionManager->GetAdjustedActionId(actionID);

                if (ReAction.Config.EnableAutoDismount && TryDismount(actionType, adjustedActionID, targetObjectID, useType, pvp, out var ret))
                    return ret;

                var succeeded = false;
                if (actionType == 1 && useType == 0 && ReAction.actionSheet.ContainsKey(adjustedActionID))
                {
                    var modifierKeys = GetModifierKeys();
                    foreach (var stack in ReAction.Config.ActionStacks)
                    {
                        var exactMatch = (stack.ModifierKeys & 8) != 0;
                        if (exactMatch ? stack.ModifierKeys != modifierKeys : (stack.ModifierKeys & modifierKeys) != stack.ModifierKeys) continue;
                        if (stack.Actions.FirstOrDefault(action => action.ID == 0 || (action.UseAdjustedID ? actionManager->GetAdjustedActionId(action.ID) : action.ID) == adjustedActionID) == null) continue;

                        if (!CheckActionStack(adjustedActionID, stack, out var newAction, out var newTarget))
                        {
                            if (stack.BlockOriginal)
                                return 0;
                            break;
                        }

                        actionID = newAction;
                        targetObjectID = newTarget;
                        succeeded = true;
                        break;
                    }
                }

                if (ReAction.Config.EnableAutoTarget && actionType == 1 && TryTabTarget(adjustedActionID, targetObjectID, out var newObjectID))
                    targetObjectID = newObjectID;

                if (ReAction.Config.EnableCameraRelativeDashes)
                    TryDashFromCamera(actionType, actionID);

                ret = Game.UseActionHook.Original(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);

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

                if (ReAction.Config.EnableInstantGroundTarget)
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
            foreach (var item in stack.Items)
            {
                var newID = item.ID != 0 ? Game.actionManager->GetAdjustedActionId(item.ID) : id;
                var newTarget = GetTarget(item.Target);
                if (newTarget == null || !CanUseAction(newID, newTarget) || useRange && Game.IsActionOutOfRange(newID, newTarget)) continue;

                action = newID;

                if (newTarget->ObjectID != target)
                {
                    target = newTarget->ObjectID;
                }
                else
                {
                    var localObjectID = *(uint*)((IntPtr)newTarget + 0x78);
                    if (localObjectID != 0)
                        target = localObjectID | 0x1_0000_0000;
                }

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
            }

            return o != null ? (GameObject*)o.Address : null;
        }

        private static bool CanUseAction(uint id, GameObject* target)
            => Game.CanUseActionOnGameObject(id, target) && Game.actionManager->GetActionStatus(ActionType.Spell, id, target->ObjectID) is 0 or 580 or 582;

        private static bool TryTabTarget(uint actionID, long objectID, out uint newObjectID)
        {
            newObjectID = 0;
            var targetObject = DalamudApi.TargetManager.Target is { } t ? (GameObject*)t.Address : null;
            if (objectID != 0xE0000000 && Game.GetGameObjectFromObjectID(objectID) != targetObject
                || Game.CanUseActionOnGameObject(actionID, targetObject)
                || !ReAction.actionSheet.TryGetValue(actionID, out var a)
                || !a.CanTargetHostile)
                return false;

            Game.TargetEnemyNext();
            if (DalamudApi.TargetManager.Target is not { } target) return false;

            newObjectID = target.ObjectId;
            return true;
        }

        private static bool TryDismount(uint actionType, uint actionID, long targetObjectID, uint useType, int pvp, out byte ret)
        {
            ret = 0;

            if (!DalamudApi.Condition[ConditionFlag.Mounted]
                || actionType == 1 && ReAction.mountActionsSheet.ContainsKey(actionID)
                || (actionType != 5 || actionID is not (3 or 4)) && (actionType != 1 || actionID is 5 or 6) // +Limit Break / +Sprint / -Teleport / -Return
                || Game.actionManager->GetActionStatus((ActionType)actionType, actionID, (uint)targetObjectID, 0, 0) == 0)
                return false;

            ret = Game.UseActionHook.Original(Game.actionManager, 5, 23, 0, 0, 0, 0, null);
            if (ret == 0) return true;

            isMountActionQueued = true;
            queuedMountAction = (actionType, actionID, targetObjectID, useType, pvp);
            mountActionTimer.Restart();
            return true;
        }

        private static void TryDashFromCamera(uint actionType, uint actionID)
        {
            if (ReAction.actionSheet.TryGetValue(actionID, out var a) && a.AffectsPosition && a.CanTargetSelf && a.BehaviourType > 1
                && Game.actionManager->GetActionStatus((ActionType)actionType, actionID) == 0 && *((float*)Game.actionManager + 2) == 0)
                Game.SetCharacterRotationToCamera();
        }

        private static void TryInstantGroundTarget(uint actionType, uint useType)
        {
            if ((useType != 2 || actionType != 1) && actionType != 15)
                *(byte*)((IntPtr)Game.actionManager + 0xB8) = 1;
        }

        private static void TryQueuedMountAction()
        {
            if (DalamudApi.Condition[ConditionFlag.Mounted]) return;

            if (mountActionTimer.ElapsedMilliseconds <= 2000)
            {
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

            Game.CancelCast();
            canceledCast = true;
        }

        public static void Update()
        {
            if (isMountActionQueued)
                TryQueuedMountAction();

            if (!ReAction.Config.EnableAutoCastCancel) return;

            if (canceledCast && Game.CastActionType == 0)
                canceledCast = false;
            else
                TryCancelingCast();
        }
    }
}
