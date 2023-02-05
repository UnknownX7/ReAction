using Dalamud.Logging;
using Hypostasis.Game.Structures;

namespace ReAction.Modules;

public unsafe class QueueMore : Module
{
    private static bool queuedItem = false;

    public override bool ShouldEnable => ReAction.Config.EnableQueuingMore;

    protected override bool Validate() => Game.allowQueuingEdit.IsValid;

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

    private static void PreUseAction(ActionManager* actionManager, ref uint actionType, ref uint actionID, ref long targetObjectID, ref uint param, ref uint useType, ref int pvp)
    {
        if (queuedItem && useType == 1)
        {
            PluginLog.Debug("Applying queued item param");

            param = 65535;
            queuedItem = false;
        }
        else if (actionType == 5 && actionID == 4 && useType == 1)
        {
            actionType = 1;
            actionID = 3;
            targetObjectID = DalamudApi.ClientState.LocalPlayer!.ObjectId;
        }
    }

    private static void PostActionStack(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, ref long targetObjectID, uint param, uint useType, int pvp)
    {
        if (useType != 0 || ((actionType != 5 || adjustedActionID != 4) && actionType != 2)) return;

        PluginLog.Debug($"Enabling queuing {actionType}, {adjustedActionID}");

        Game.allowQueuingEdit.Enable();
        queuedItem = actionType == 2;
    }

    private static void PostUseAction(ActionManager* actionManager, uint actionType, uint actionID, uint adjustedActionID, long targetObjectID, uint param, uint useType, int pvp)
    {
        if (Game.allowQueuingEdit.IsEnabled)
            Game.allowQueuingEdit.Disable();

        if (queuedItem && !actionManager->isQueued)
            queuedItem = false;
    }
}