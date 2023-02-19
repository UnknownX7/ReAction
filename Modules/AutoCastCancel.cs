using Dalamud.Game;
using Dalamud.Logging;
using Hypostasis.Game.Structures;

namespace ReAction.Modules;

public unsafe class AutoCastCancel : PluginModule
{
    private static bool canceledCast = false;

    public override bool ShouldEnable => ReAction.Config.EnableAutoCastCancel;

    protected override bool Validate() => Game.fpGetGameObjectFromObjectID != null && Game.fpCancelCast != null;
    protected override void Enable() => DalamudApi.Framework.Update += Update;
    protected override void Disable() => DalamudApi.Framework.Update -= Update;

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

            PluginLog.Debug($"Cancelling cast {Common.ActionManager->castActionType}, {Common.ActionManager->castActionID}, {Common.ActionManager->castTargetObjectID:X}");

            Game.CancelCast();
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