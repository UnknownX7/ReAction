using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using ActionManager = Hypostasis.Game.Structures.ActionManager;

namespace ReAction.Modules;

public unsafe class CameraRelativeDashes : PluginModule
{
    public override bool ShouldEnable => ReAction.Config.EnableCameraRelativeDashes;

    protected override bool Validate() => Game.fpSetGameObjectRotation != null && Common.CameraManager != null;
    protected override void Enable() => ActionStackManager.PostActionStack += PostActionStack;
    protected override void Disable() => ActionStackManager.PostActionStack -= PostActionStack;

    private static void PostActionStack(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ref long targetObjectID, uint param, uint useType, int pvp)
    {
        if (!ReAction.actionSheet.TryGetValue(adjustedActionID, out var a)
            || (!a.AffectsPosition && adjustedActionID != 29494) // Elusive Jump isn't classified as a movement ability for some reason
            || !a.CanTargetSelf
            || a.BehaviourType <= 1
            || ReAction.Config.EnableNormalBackwardDashes && a.BehaviourType is 3 or 4
            || actionManager->CS.GetActionStatus((ActionType)actionType, adjustedActionID) != 0
            || actionManager->animationLock != 0)
            return;

        PluginLog.Debug($"Rotating camera {actionType}, {adjustedActionID}");

        Game.SetCharacterRotationToCamera();
    }
}