using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace ReAction.Modules;

public class AutoFocusTarget : PluginModule
{
    public override bool ShouldEnable => ReAction.Config.AutoFocusTargetID != 0;

    protected override bool Validate() => Game.SetFocusTargetByObjectIDHook is { Address: not 0 };
    protected override void Enable() => DalamudApi.Framework.Update += Update;
    protected override void Disable() => DalamudApi.Framework.Update -= Update;

    private static unsafe void Update(Framework framework)
    {
        var target = PronounManager.GetGameObjectFromID(ReAction.Config.AutoFocusTargetID);

        if (target == null && Game.FocusTargetInfo.Name != null)
            Game.RefocusTarget();
        else
            Game.SetFocusTargetByObjectIDHook.Original(TargetSystem.Instance(), Game.GetObjectID(target));
    }
}