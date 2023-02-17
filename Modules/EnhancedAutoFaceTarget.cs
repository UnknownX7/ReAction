using Hypostasis.Game.Structures;
using Lumina.Excel.GeneratedSheets;

namespace ReAction.Modules;

public unsafe class EnhancedAutoFaceTarget : PluginModule
{
    public override bool ShouldEnable => ReAction.Config.EnableEnhancedAutoFaceTarget;

    protected override bool Validate() => Game.enhancedAutoFaceTargetPatch.IsValid;
    protected override void Enable() => ActionStackManager.PostActionStack += PostActionStack;
    protected override void Disable() => ActionStackManager.PostActionStack -= PostActionStack;

    private static void PostActionStack(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ref long targetObjectID, uint param, uint useType, int pvp)
    {
        if (DalamudApi.DataManager.GetExcelSheet<Action>()?.GetRow(adjustedActionID) is not { Unknown50: 6 }) // These actions avoid facing the target by default, but some of them fail the patched check
            Game.enhancedAutoFaceTargetPatch.Enable();
        else
            Game.enhancedAutoFaceTargetPatch.Disable();
    }
}