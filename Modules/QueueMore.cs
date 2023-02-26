using Dalamud.Logging;
using Hypostasis.Game.Structures;
using Lumina.Excel.GeneratedSheets;

namespace ReAction.Modules;

public unsafe class QueueMore : PluginModule
{
    private static bool queuedItem = false;

    public override bool ShouldEnable => ReAction.Config.EnableQueuingMore;

    protected override bool Validate() => Game.allowQueuingPatch.IsValid;

    protected override void Enable()
    {
        ActionStackManager.PreUseAction += PreUseAction;
        ActionStackManager.PostActionStack += PostActionStack;
        ActionStackManager.PostUseAction += PostUseAction;
    }

    protected override void Disable()
    {
        ActionStackManager.PreUseAction -= PreUseAction;
        ActionStackManager.PostActionStack -= PostActionStack;
        ActionStackManager.PostUseAction -= PostUseAction;
    }

    private static void PreUseAction(ActionManager* actionManager, ref uint actionType, ref uint actionID, ref long targetObjectID, ref uint param, ref uint useType, ref int pvp)
    {
        if (queuedItem && useType == 1)
        {
            PluginLog.Debug("Applying queued item param");

            param = 65535;
            queuedItem = false;
        }
        else if (actionType == 5 && actionID == 4 && useType == 1)
        {
            actionType = 1;
            actionID = 3;
            targetObjectID = DalamudApi.ClientState.LocalPlayer!.ObjectId;
        }
    }

    private static void PostActionStack(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ref long targetObjectID, uint param, uint useType, int pvp)
    {
        if (useType != 0 || !CheckAction(actionType, actionID, adjustedActionID)) return;

        PluginLog.Debug($"Enabling queuing {actionType}, {adjustedActionID}");

        Game.allowQueuingPatch.Enable();
        queuedItem = actionType == 2;
    }

    private static void PostUseAction(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, long targetObjectID, uint param, uint useType, int pvp, bool ret)
    {
        Game.allowQueuingPatch.Disable();

        if (queuedItem && !actionManager->isQueued)
            queuedItem = false;
    }

    private static bool CheckAction(uint actionType, uint actionID, uint adjustedActionID) =>
        actionType switch
        {
            1 when DalamudApi.DataManager.GetExcelSheet<Action>()?.GetRow(adjustedActionID) is { ActionCategory.Row: 9 or 15 } => true, // Allow LB
            2 => true, // Allow items
            5 when actionID == 4 => true, // Allow Sprint
            _ => false
        };
}