using Hypostasis.Game.Structures;
using Lumina.Excel.GeneratedSheets;

namespace ReAction.Modules;

public unsafe class EnhancedAutoFaceTarget : PluginModule
{
    // cmp byte ptr [r15+33h], 6 -> test byte ptr [r15+3Ah], 10
    private static readonly AsmPatch enhancedAutoFaceTargetPatch = new("41 80 7F 33 06 75 1E 48 8D 0D", new byte[] { 0x41, 0xF6, 0x47, 0x3A, 0x10 });
    private static readonly AsmPatch removeAutoFaceGroundTargetPatch = new("41 80 7F 33 06 74 22 49 8D 8E", new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0xEB });

    public override bool ShouldEnable => ReAction.Config.EnableEnhancedAutoFaceTarget;

    protected override bool Validate() => enhancedAutoFaceTargetPatch.IsValid;

    protected override void Enable()
    {
        enhancedAutoFaceTargetPatch.Disable();
        removeAutoFaceGroundTargetPatch.Enable();
        ActionStackManager.PostActionStack += PostActionStack;
    }

    protected override void Disable()
    {
        enhancedAutoFaceTargetPatch.Disable();
        removeAutoFaceGroundTargetPatch.Disable();
        ActionStackManager.PostActionStack -= PostActionStack;
    }

    private static void PostActionStack(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ref long targetObjectID, uint param, uint useType, int pvp)
    {
        if (DalamudApi.DataManager.GetExcelSheet<Action>()?.GetRow(adjustedActionID) is not { Unknown50: 6 }) // These actions avoid facing the target by default, but some of them fail the patched check
            enhancedAutoFaceTargetPatch.Enable();
        else
            enhancedAutoFaceTargetPatch.Disable();
    }
}