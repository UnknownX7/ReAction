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
            if (canceledCast
                || Common.ActionManager->castActionType != 1
                || !ReAction.actionSheet.TryGetValue(Common.ActionManager->castActionID, out var a)
                || a.TargetArea)
                return;

            var o = Game.GetGameObjectFromObjectID(Common.ActionManager->castTargetObjectID);
            if (o == null || ActionManager.CanUseActionOnGameObject(Common.ActionManager->castActionID, o)) return;

            PluginLog.Debug($"Cancelling cast {Common.ActionManager->castActionType}, {Common.ActionManager->castActionID}, {Common.ActionManager->castTargetObjectID:X}");

            Game.CancelCast();
            canceledCast = true;
        }
    }
}