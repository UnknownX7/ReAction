using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using Hypostasis.Game.Structures;

namespace ReAction.Modules;

public unsafe class TurboHotbars : PluginModule
{
    private static bool hotbarPressed = false;

    public override bool ShouldEnable => ReAction.Config.EnableTurboHotbars;

    protected override bool Validate() => InputData.isInputIDPressed.IsValid && InputData.isInputIDHeld.IsValid;

    protected override void Enable()
    {
        if (!InputData.isInputIDPressed.IsHooked)
            InputData.isInputIDPressed.CreateHook(IsInputIDPressedDetour);
        CheckHotbarBindingsHook.Enable();
    }

    protected override void Disable()
    {
        InputData.isInputIDPressed.Hook.Disable();
        CheckHotbarBindingsHook.Disable();
    }

    private static Bool IsInputIDPressedDetour(InputData* inputData, uint id)
    {
        var ret = inputData->IsInputIDHeld(id);
        if (ret)
            hotbarPressed = true;
        return ret;
    }

    private delegate void CheckHotbarBindingsDelegate(nint a1, byte a2);
    [Signature("48 89 4C 24 08 53 41 55 41 57", Fallibility = Fallibility.Infallible), SignatureEx(EnableHook = false)]
    private static Hook<CheckHotbarBindingsDelegate> CheckHotbarBindingsHook;
    private static void CheckHotbarBindingsDetour(nint a1, byte a2)
    {
        InputData.isInputIDPressed.Hook.Enable();
        CheckHotbarBindingsHook.Original(a1, a2);
        InputData.isInputIDPressed.Hook.Disable();

        if (!hotbarPressed) return;
        hotbarPressed = false;

        if (ReAction.Config.TurboHotbarInterval <= 0) return;
        CheckHotbarBindingsHook.Disable();
        DalamudApi.Framework.RunOnTick(() =>
        {
            if (!ReAction.Config.EnableTurboHotbars || CheckHotbarBindingsHook.IsDisposed) return;
            CheckHotbarBindingsHook.Enable();
        }, new TimeSpan(0, 0, 0, 0, ReAction.Config.TurboHotbarInterval));
    }
}