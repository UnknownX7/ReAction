using Dalamud.Game.ClientState.Conditions;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Diagnostics;
using Dalamud.Game;
using ActionManager = Hypostasis.Game.Structures.ActionManager;

namespace ReAction.Modules;

public unsafe class AutoDismount : PluginModule
{
    private static bool isMountActionQueued = false;
    private static (uint actionType, uint actionID, long targetObjectID, uint useType, int pvp) queuedMountAction;
    private static readonly Stopwatch mountActionTimer = new();

    public override bool ShouldEnable => ReAction.Config.EnableAutoDismount;

    protected override void Enable()
    {
        ActionStackManager.PreActionStack += PreActionStack;
        DalamudApi.Framework.Update += Update;
    }

    protected override void Disable()
    {
        ActionStackManager.PreActionStack -= PreActionStack;
        DalamudApi.Framework.Update -= Update;
    }

    private static void PreActionStack(ActionManager* actionManager, ref uint actionType, ref uint actionID, ref uint adjustedActionID, ref long targetObjectID, ref uint param, uint useType, ref int pvp, out bool? ret)
    {
        ret = null;

        if (!DalamudApi.Condition[ConditionFlag.Mounted] || !CheckAction(actionType, actionID, adjustedActionID)
            || actionManager->CS.GetActionStatus((ActionType)actionType, actionID, targetObjectID, false, false) == 0)
            return;

        ret = Game.UseActionHook.Original(actionManager, 5, 23, 0, 0, 0, 0, null);
        if (!ret.Value) return;

        PluginLog.Debug($"Dismounting {actionType}, {actionID}, {targetObjectID:X}, {useType}, {pvp}");

        isMountActionQueued = true;
        queuedMountAction = (actionType, actionID, targetObjectID, useType, pvp);
        mountActionTimer.Restart();
    }

    private static void Update(Framework framework)
    {
        if (!isMountActionQueued || DalamudApi.Condition[ConditionFlag.Mounted]) return;

        if (mountActionTimer.ElapsedMilliseconds <= 2000)
        {
            PluginLog.Debug("Using queued mount action");

            ActionStackManager.OnUseAction(Common.ActionManager, queuedMountAction.actionType, queuedMountAction.actionID,
                queuedMountAction.targetObjectID, 0, queuedMountAction.useType, queuedMountAction.pvp, null);
        }

        isMountActionQueued = false;
        mountActionTimer.Stop();
    }

    private static bool CheckAction(uint actionType, uint actionID, uint adjustedActionID) =>
        actionType switch
        {
            1 when !ReAction.mountActionsSheet.ContainsKey(adjustedActionID) && actionID is not (5 or 6) => true, // Block mount actions, Teleport and Return
            5 when actionID is 3 or 4 => true, // Allow LB and Sprint
            _ => false
        };
}