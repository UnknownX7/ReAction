using Hypostasis.Game.Structures;
using Lumina.Excel.GeneratedSheets;

namespace ReAction.Modules;

public unsafe class EnhancedAutoFaceTarget : PluginModule
{
    private static readonly AsmPatch removeAutoFaceTargetPatch = new("41 80 7F 33 06 75 1E 48 8D 0D", new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0xEB, 0x1C });
    private static readonly AsmPatch removeAutoFaceGroundTargetPatch = new("41 80 7F 33 06 74 22 49 8D 8E", new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0xEB });

    public override bool ShouldEnable => ReAction.Config.EnableEnhancedAutoFaceTarget;

    protected override bool Validate() => removeAutoFaceTargetPatch.IsValid;

    protected override void Enable()
    {
        removeAutoFaceTargetPatch.Disable();
        removeAutoFaceGroundTargetPatch.Enable();
        ActionStackManager.PostActionStack += PostActionStack;
    }

    protected override void Disable()
    {
        removeAutoFaceTargetPatch.Disable();
        removeAutoFaceGroundTargetPatch.Disable();
        ActionStackManager.PostActionStack -= PostActionStack;
    }

    private static void PostActionStack(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ref long targetObjectID, uint param, uint useType, int pvp)
    {
        if (DalamudApi.DataManager.GetExcelSheet<Action>()?.GetRow(adjustedActionID) is { Unknown26: false }) // This is checked by Client::Game::ActionManager_GetActionInRangeOrLoS
            removeAutoFaceTargetPatch.Enable();
        else
            removeAutoFaceTargetPatch.Disable();
    }
}