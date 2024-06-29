using Hypostasis.Game.Structures;
using Lumina.Excel.GeneratedSheets;

namespace ReAction.Modules;

public unsafe class QueueMore : PluginModule
{
    private static readonly AsmPatch allowQueuingPatch = new("76 0A 41 80 F8 04", [ 0xEB ]);
    private static ushort lastLBSequence = 0;

    public override bool ShouldEnable => ReAction.Config.EnableQueuingMore;

    protected override bool Validate() => allowQueuingPatch.IsValid;

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

    private static void PreUseAction(ActionManager* actionManager, ref uint actionType, ref uint actionID, ref ulong targetObjectID, ref uint param, ref uint useType, ref int pvp)
    {
        if (useType != 1) return;

        switch (actionType)
        {
            case 2:
                DalamudApi.LogDebug("Applying queued item param");
                param = 65535;
                break;
            case 5 when actionID == 4:
                actionType = 1;
                actionID = 3;
                targetObjectID = DalamudApi.ClientState.LocalPlayer!.GameObjectId;
                break;
        }
    }

    private static void PostActionStack(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ref ulong targetObjectID, uint param, uint useType, int pvp)
    {
        if (useType != 0 || !CheckAction(actionType, actionID, adjustedActionID)) return;

        allowQueuingPatch.Enable();
        DalamudApi.LogDebug($"Enabling queuing {actionType}, {adjustedActionID}");
    }

    private static void PostUseAction(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ulong targetObjectID, uint param, uint useType, int pvp, bool ret)
    {
        allowQueuingPatch.Disable();

        if (ret && DalamudApi.DataManager.GetExcelSheet<Action>()?.GetRow(adjustedActionID) is { ActionCategory.Row: 9 or 15 })
            lastLBSequence = actionManager->currentSequence;
    }

    private static bool CheckAction(uint actionType, uint actionID, uint adjustedActionID) =>
        actionType switch
        {
            1 when DalamudApi.DataManager.GetExcelSheet<Action>()?.GetRow(adjustedActionID) is { ActionCategory.Row: 9 or 15 } => lastLBSequence != Common.ActionManager->currentSequence, // Allow LB
            2 => true, // Allow items
            5 when actionID == 4 => true, // Allow Sprint
            _ => false
        };
}