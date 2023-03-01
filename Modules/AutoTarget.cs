using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Hypostasis.Game.Structures;

namespace ReAction.Modules;

public unsafe class AutoTarget : PluginModule
{
    public override bool ShouldEnable => ReAction.Config.EnableAutoTarget;

    protected override bool Validate() => Game.fpGetGameObjectFromObjectID != null;
    protected override void Enable() => ActionStackManager.PostActionStack += PostActionStack;
    protected override void Disable() => ActionStackManager.PostActionStack -= PostActionStack;

    private static void PostActionStack(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ref long targetObjectID, uint param, uint useType, int pvp)
    {
        if (actionType != 1) return;

        var targetObject = DalamudApi.TargetManager.Target is { } t ? (GameObject*)t.Address : null;
        if (!ReAction.Config.EnableAutoChangeTarget && targetObject != null
            || targetObjectID != Game.InvalidObjectID && Game.GetGameObjectFromObjectID(targetObjectID) != targetObject
            || ActionManager.CanUseActionOnGameObject(adjustedActionID, targetObject)
            || !ReAction.actionSheet.TryGetValue(adjustedActionID, out var a)
            || !a.CanTargetHostile)
            return;

        PluginLog.Debug($"Attempting to swap target {adjustedActionID}, {targetObjectID:X}");

        Game.TargetEnemy();
        if (DalamudApi.TargetManager.Target is not { } target) return;

        var prevTargetObjectID = targetObjectID;
        targetObjectID = Game.GetObjectID((GameObject*)target.Address);

        PluginLog.Debug($"Target swapped {prevTargetObjectID:X} -> {targetObjectID:X}");
    }
}