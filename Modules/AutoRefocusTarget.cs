using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;

namespace ReAction.Modules;

public class AutoRefocusTarget : Module
{
    public override bool ShouldEnable => ReAction.Config.EnableAutoRefocusTarget;

    protected override bool Validate() => Game.SetFocusTargetByObjectIDHook.Address != nint.Zero;
    protected override void Enable() => DalamudApi.Framework.Update += Update;
    protected override void Disable() => DalamudApi.Framework.Update -= Update;

    private static void Update(Framework framework)
    {
        if (!DalamudApi.Condition[ConditionFlag.BoundByDuty]) return;
        Game.RefocusTarget();
    }
}