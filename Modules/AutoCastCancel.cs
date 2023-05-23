using Dalamud.Game;
using Hypostasis.Game.Structures;

namespace ReAction.Modules;

public unsafe class AutoCastCancel : PluginModule
{
    private static bool canceledCast = false;

    public override bool ShouldEnable => ReAction.Config.EnableAutoCastCancel;

    protected override bool Validate() => Game.fpGetGameObjectFromObjectID != null && ActionManager.canUseActionOnGameObject.IsValid;
    protected override void Enable() => DalamudApi.Framework.Update += Update;
    protected override void Disable() => DalamudApi.Framework.Update -= Update;

    [HypostasisSignatureInjection("48 83 EC 38 33 D2 C7 44 24 20 00 00 00 00 45 33 C9", Required = true)]
    private static delegate* unmanaged<void> cancelCast;

    private static void Update(Framework framework)
    {
        if (canceledCast && Common.ActionManager->castActionType == 0)
        {
            canceledCast = false;
        }
        else
        {
            if (canceledCast || !CheckAction(Common.ActionManager->castActionType, Common.ActionManager->castActionID)) return;

            var o = Game.GetGameObjectFromObjectID(Common.ActionManager->castTargetObjectID);
            if (o == null || ActionManager.CanUseActionOnGameObject(Common.ActionManager->castActionID, o)) return;

            DalamudApi.LogDebug($"Cancelling cast {Common.ActionManager->castActionType}, {Common.ActionManager->castActionID}, {Common.ActionManager->castTargetObjectID:X}");

            cancelCast();
            canceledCast = true;
        }
    }

    private static bool CheckAction(uint actionType, uint actionID)
    {
        if (actionType != 1) return false; // Block non normal actions
        if (!ReAction.actionSheet.TryGetValue(actionID, out var a)) return false;
        return !a.TargetArea; // Block ground targets
    }
}