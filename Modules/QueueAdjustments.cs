using System;
using System.Diagnostics;
using FFXIVClientStructs.FFXIV.Client.Game;
using Hypostasis.Game.Structures;
using ActionManager = Hypostasis.Game.Structures.ActionManager;

namespace ReAction.Modules;

public unsafe class QueueAdjustments : PluginModule
{
    private static bool isRequeuing = false;
    private static float tempQueue = 0f;
    private static uint lastUsedActionID = 0;
    private static readonly Stopwatch lastUsedActionTimer = new();

    public override bool ShouldEnable => ReAction.Config.EnableQueueAdjustments;

    protected override bool Validate() => ActionManager.canQueueAction.IsValid && ActionManager.getAdjustedRecastTime.IsValid;

    protected override void Enable()
    {
        if (!ActionManager.canQueueAction.IsHooked)
            ActionManager.canQueueAction.CreateHook(CanQueueActionDetour);
        ActionManager.canQueueAction.Hook.Enable();
        ActionStackManager.PostActionStack += PostActionStack;
        ActionStackManager.PostUseAction += PostUseAction;
    }

    protected override void Disable()
    {
        ActionManager.canQueueAction.Hook.Disable();
        ActionStackManager.PostActionStack -= PostActionStack;
        ActionStackManager.PostUseAction -= PostUseAction;
    }

    private static void PostActionStack(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ref ulong targetObjectID, uint param, uint useType, int pvp)
    {
        if (useType == 1 && tempQueue > 0)
            tempQueue = 0;

        if (!ReAction.Config.EnableRequeuing
            || !actionManager->isQueued
            || GetRemainingActionRecast(actionManager, (uint)actionManager->CS.QueuedActionType, actionManager->CS.QueuedActionType == ActionType.Action
                    ? actionManager->CS.GetAdjustedActionId(actionManager->CS.QueuedActionId)
                    : actionManager->CS.QueuedActionId)
                is { } remaining && remaining <= ReAction.Config.QueueLockThreshold)
            return;
        actionManager->isQueued = false;
        isRequeuing = true;
    }

    private static void PostUseAction(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ulong targetObjectID, uint param, uint useType, int pvp, bool ret)
    {
        if (ret && (useType == 1 || !actionManager->isQueued))
        {
            lastUsedActionID = adjustedActionID;
            lastUsedActionTimer.Restart();
        }

        if (!isRequeuing) return;

        if (!ret)
            actionManager->isQueued = true;
        isRequeuing = false;
    }

    [HypostasisSignatureInjection("E8 ?? ?? ?? ?? 8B 4F 44 33 D2", Required = true)]
    private static delegate* unmanaged<ActionManager*, uint, uint, int> getAdditionalRecastGroup;

    [HypostasisSignatureInjection("E8 ?? ?? ?? ?? 84 C0 74 10 48 83 FF 0F", Required = true)]
    private static delegate* unmanaged<ActionManager*, uint, Bool> canUseActionAsCurrentClass;

    private static float? GetRemainingActionRecast(ActionManager* actionManager, uint actionType, uint actionID)
    {
        var recastGroupDetail = actionManager->CS.GetRecastGroupDetail(actionManager->CS.GetRecastGroup((int)actionType, actionID));
        if (recastGroupDetail == null) return null;

        var additionalRecastGroupDetail = actionManager->CS.GetRecastGroupDetail(getAdditionalRecastGroup(actionManager, actionType, actionID));
        var additionalRecastRemaining = additionalRecastGroupDetail != null && additionalRecastGroupDetail->IsActive != 0 ? additionalRecastGroupDetail->Total - additionalRecastGroupDetail->Elapsed : 0;

        if (recastGroupDetail->IsActive == 0) return additionalRecastRemaining;

        var charges = canUseActionAsCurrentClass(actionManager, recastGroupDetail->ActionId) ? FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetMaxCharges(ActionManager.GetSpellIDForAction(actionType, actionID), 90) : 1;
        var recastRemaining = recastGroupDetail->Total / charges - recastGroupDetail->Elapsed;
        return recastRemaining > additionalRecastRemaining ? recastRemaining : additionalRecastRemaining;
    }

    public static Bool CanQueueActionDetour(ActionManager* actionManager, uint actionType, uint actionID)
    {
        float threshold;
        if (tempQueue > 0)
        {
            threshold = tempQueue;
        }
        else if (ReAction.Config.EnableSlidecastQueuing && actionManager->isCasting)
        {
            threshold = Math.Max(actionManager->gcdRecastTime - actionManager->castTime + 0.5f, 0.5f);
            tempQueue = threshold;
        }
        else
        {
            threshold = (actionType == 1 ? actionManager->CS.GetAdjustedActionId(lastUsedActionID) == actionManager->CS.GetAdjustedActionId(actionID) : lastUsedActionID == actionID) && lastUsedActionTimer.Elapsed.TotalSeconds < ReAction.Config.QueueActionLockout
                ? 0
                : ReAction.Config.EnableGCDAdjustedQueueThreshold
                    ? ReAction.Config.QueueThreshold * ActionManager.GCDRecast / 2500f
                    : ReAction.Config.QueueThreshold;
        }

        return GetRemainingActionRecast(actionManager, actionType, actionID) is { } remaining && remaining <= threshold;
    }

    // Original implementation
    /*public static bool OnCanQueueAction(ActionManager* actionManager, uint actionType, uint actionID)
    {
        var recastGroupDetail = actionManager->CS.GetRecastGroupDetail(actionManager->CS.GetRecastGroup((int)actionType, actionID));
        if (recastGroupDetail == null) return false;

        var queueThreshold = 0.5f;
        var additionalRecastGroupDetail = actionManager->CS.GetRecastGroupDetail(Game.fpGetAdditionalRecastGroup(actionManager, actionType, actionID));
        if (additionalRecastGroupDetail != null && additionalRecastGroupDetail->IsActive != 0 && additionalRecastGroupDetail->Total - additionalRecastGroupDetail->Elapsed > queueThreshold) return false;

        if (recastGroupDetail->IsActive == 0) return true;

        if (Game.fpCanUseActionAsCurrentClass(actionManager, recastGroupDetail->ActionID))
        {
            var maxCharges = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetMaxCharges(ActionManager.GetSpellIDForAction(actionType, actionID), 90);
            if (recastGroupDetail->Total / maxCharges - recastGroupDetail->Elapsed > queueThreshold) return false;
        }
        else if (recastGroupDetail->Total - recastGroupDetail->Elapsed > queueThreshold)
        {
            return false;
        }

        return true;
    }*/
}