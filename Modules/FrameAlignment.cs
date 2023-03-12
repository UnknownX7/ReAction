using System;
using System.Diagnostics;
using Dalamud.Game;
using Dalamud.Logging;

namespace ReAction.Modules;

public unsafe class FrameAlignment : PluginModule
{
    private static readonly Stopwatch timer = new();

    public override bool ShouldEnable => ReAction.Config.EnableFrameAlignment;

    protected override void Enable() => DalamudApi.Framework.Update += Update;

    protected override void Disable()
    {
        DalamudApi.Framework.Update -= Update;
        timer.Stop();
    }

    private static void Update(Framework framework)
    {
        if (timer.IsRunning)
        {
            var elapsedTime = timer.ElapsedTicks / (double)Stopwatch.Frequency;
            var remainingAnimationLock = Common.ActionManager->animationLock - elapsedTime;
            var remainingGCD = Common.ActionManager->gcdRecastTime - Common.ActionManager->elapsedGCDRecastTime - elapsedTime;
            var blockDuration = 0d;

            if (remainingAnimationLock > 0 && remainingAnimationLock <= elapsedTime * 1.1)
                blockDuration = Math.Round(remainingAnimationLock * Stopwatch.Frequency);

            if (remainingGCD > 0 && remainingGCD <= elapsedTime * 1.1)
            {
                var newBlockDuration = Math.Round(remainingGCD * Stopwatch.Frequency);
                if (newBlockDuration > blockDuration)
                    blockDuration = newBlockDuration;
            }

            if (blockDuration > 0)
            {
                PluginLog.Debug($"Blocking main thread for {blockDuration / Stopwatch.Frequency * 1000} ms");

                timer.Restart();
                while (timer.ElapsedTicks < blockDuration) ;
            }
        }

        timer.Restart();
    }
}