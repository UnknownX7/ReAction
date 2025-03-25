using Hypostasis.Game.Structures;
using Lumina.Excel.Sheets;

namespace ReAction.Modules;

public unsafe class QueueMore : PluginModule
{
    // Causes switch statement to behave as if ActionCategory is 2 or 3 (Spell / Weaponskill)
    // jz -> jmp ??
    private static readonly AsmPatch allowQueuingPatch = new("0F B6 49 22 83 E9 02 0F 84", [ null, null, null, null, null, null, null, 0x90, 0xE9 ]);
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

        if (ret && DalamudApi.DataManager.GetExcelSheet<Action>().GetRowOrDefault(adjustedActionID) is { ActionCategory.RowId: 9 or 15 })
            lastLBSequence = actionManager->currentSequence;
    }

    private static bool CheckAction(uint actionType, uint actionID, uint adjustedActionID) =>
        actionType switch
        {
            1 when DalamudApi.DataManager.GetExcelSheet<Action>().GetRowOrDefault(adjustedActionID) is { ActionCategory.RowId: 9 or 15 } => lastLBSequence != Common.ActionManager->currentSequence, // Allow LB
            2 => true, // Allow items
            _ => false
        };
}