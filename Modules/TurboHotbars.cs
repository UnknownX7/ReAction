using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Hypostasis.Game.Structures;

namespace ReAction.Modules;

public unsafe class TurboHotbars : PluginModule
{
    private class TurboInfo
    {
        public Stopwatch LastPress { get; } = new();
        public bool LastFramePressed { get; set; } = false;
        public bool LastFrameHeld { get; set; } = false;
        public int RepeatDelay { get; set; } = 0;

        public bool IsReady => LastPress.IsRunning && LastPress.ElapsedMilliseconds >= RepeatDelay;
    }

    private static readonly Dictionary<uint, TurboInfo> inputIDInfos = new();
    private static bool isAnyTurboRunning;

    public override bool ShouldEnable => ReAction.Config.EnableTurboHotbars;

    protected override bool Validate() => InputData.isInputIDPressed.IsValid && InputData.isInputIDHeld.IsValid;

    protected override void Enable()
    {
        if (!InputData.isInputIDPressed.IsHooked)
            InputData.isInputIDPressed.CreateHook(IsInputIDPressedDetour, false);
        CheckHotbarBindingsHook.Enable();
        //CheckCrossbarBindingsHook.Enable();
    }

    protected override void Disable()
    {
        InputData.isInputIDPressed.Hook.Disable();
        CheckHotbarBindingsHook.Disable();
        //CheckCrossbarBindingsHook.Disable();
    }

    private static Bool IsInputIDPressedDetour(InputData* inputData, uint id)
    {
        if (!inputIDInfos.TryGetValue(id, out var info))
            inputIDInfos[id] = info = new TurboInfo();

        var isPressed = InputData.isInputIDPressed.Original(inputData, id);
        var isHeld = inputData->IsInputIDHeld(id);
        var useHeld = info.IsReady && (ReAction.Config.EnableTurboHotbarsOutOfCombat || DalamudApi.Condition[ConditionFlag.InCombat]);
        var ret = useHeld ? isHeld : (bool)isPressed;

        if (isHeld && !useHeld && !info.LastFrameHeld && !ret && isAnyTurboRunning)
        {
            info.RepeatDelay = 200;
            info.LastPress.Restart();
        }
        else if (ret)
        {
            info.RepeatDelay = isPressed && ReAction.Config.InitialTurboHotbarInterval > 0 ? ReAction.Config.InitialTurboHotbarInterval : ReAction.Config.TurboHotbarInterval;
            info.LastPress.Restart();
        }
        else if (!isHeld && info.LastFrameHeld)
        {
            info.LastPress.Reset();
        }

        info.LastFrameHeld = isHeld;
        info.LastFramePressed = isPressed;
        return ret;
    }

    private delegate void CheckHotbarBindingsDelegate(nint a1, byte a2);
    [HypostasisSignatureInjection("48 89 4C 24 08 53 41 55 41 57", Required = true, EnableHook = false)]
    private static Hook<CheckHotbarBindingsDelegate> CheckHotbarBindingsHook;
    private static void CheckHotbarBindingsDetour(nint a1, byte a2)
    {
        isAnyTurboRunning = inputIDInfos.Any(t => t.Value.LastPress.IsRunning);
        InputData.isInputIDPressed.Hook.Enable();
        CheckHotbarBindingsHook.Original(a1, a2);
        InputData.isInputIDPressed.Hook.Disable();
    }

    /*private delegate void CheckCrossbarBindingsDelegate(nint a1, uint a2);
    [HypostasisSignatureInjection("E8 ?? ?? ?? ?? EB 20 E8 ?? ?? ?? ?? 84 C0", Required = true, EnableHook = false)]
    private static Hook<CheckCrossbarBindingsDelegate> CheckCrossbarBindingsHook;
    private static void CheckCrossbarBindingsDetour(nint a1, uint a2)
    {
        isAnyTurboRunning = inputIDInfos.Any(t => t.Value.LastPress.IsRunning);
        // Needs different input functions
        CheckCrossbarBindingsHook.Original(a1, a2);
    }*/
}