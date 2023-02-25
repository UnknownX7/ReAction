using Hypostasis.Game.Structures;

namespace ReAction.Modules;

public class TurboHotbars : PluginModule
{
    public override bool ShouldEnable => ReAction.Config.EnableTurboHotbars;

    protected override bool Validate() => InputData.isInputIDPressed.IsValid && InputData.isInputIDHeld.IsValid && Game.CheckHotbarBindingsHook is { Address: not 0 };

    protected override void Enable() => Game.CheckHotbarBindingsHook.Enable();

    protected override void Disable()
    {
        InputData.isInputIDPressed.Hook.Disable();
        Game.CheckHotbarBindingsHook.Disable();
    }
}