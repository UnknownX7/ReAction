using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using Hypostasis.Game.Structures;
using ActionManager = Hypostasis.Game.Structures.ActionManager;

namespace ReAction.Modules;

public unsafe class QueueAdjustments : PluginModule
{
    private static bool isRequeuing = false;
    private static float tempQueue = 0f;

    public override bool ShouldEnable => ReAction.Config.EnableQueueAdjustments;

    protected override bool Validate() => Game.fpGetAdditionalRecastGroup != null && Game.fpCanUseActionAsCurrentClass != null;

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

    private static void PostActionStack(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ref long targetObjectID, uint param, uint useType, int pvp)
    {
        if (useType == 1 && tempQueue > 0)
            tempQueue = 0;

        if (!ReAction.Config.EnableRequeuing
            || !actionManager->isQueued
            || GetRemainingActionRecast(actionManager, (uint)actionManager->CS.QueuedActionType, actionManager->CS.QueuedActionType == ActionType.Spell
                    ? actionManager->CS.GetAdjustedActionId(actionManager->CS.QueuedActionId)
                    : actionManager->CS.QueuedActionId)
                is { } remaining && remaining <= ReAction.Config.QueueLockThreshold)
            return;
        actionManager->isQueued = false;
        isRequeuing = true;
    }

    private static void PostUseAction(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, long targetObjectID, uint param, uint useType, int pvp, bool ret)
    {
        if (!isRequeuing) return;
        if (!ret)
            actionManager->isQueued = true;
        isRequeuing = false;
    }

    private static float? GetRemainingActionRecast(ActionManager* actionManager, uint actionType, uint actionID)
    {
        var recastGroupDetail = actionManager->CS.GetRecastGroupDetail(actionManager->CS.GetRecastGroup((int)actionType, actionID));
        if (recastGroupDetail == null) return null;

        var additionalRecastGroupDetail = actionManager->CS.GetRecastGroupDetail(Game.GetAdditionalRecastGroup(actionType, actionID));
        var additionalRecastRemaining = additionalRecastGroupDetail != null && additionalRecastGroupDetail->IsActive != 0 ? additionalRecastGroupDetail->Total - additionalRecastGroupDetail->Elapsed : 0;

        if (recastGroupDetail->IsActive == 0) return additionalRecastRemaining;

        var charges = Game.CanUseActionAsCurrentClass(recastGroupDetail->ActionID) ? FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetMaxCharges(ActionManager.GetSpellIDForAction(actionType, actionID), 90) : 1;
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
            threshold = ReAction.Config.QueueThreshold;
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