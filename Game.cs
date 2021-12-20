using System;
using Dalamud.Hooking;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace ReAction
{
    public static unsafe class Game
    {
        public static ActionManager* actionManager;
        public static readonly Memory.Replacer queueGroundTargetsReplacer = new("74 24 41 81 FE F5 0D 00 00", new byte[] { 0xEB }, ReAction.Config.EnableGroundTargetQueuing);
        // cmp byte ptr [r15+33h], 6 -> test byte ptr [r15+3Ah], 10
        public static readonly Memory.Replacer enhancedAutoFaceTargetReplacer = new("41 80 7F 33 06 75 1E 48 8D 0D", new byte[] { 0x41, 0xF6, 0x47, 0x3A, 0x10 }, ReAction.Config.EnableEnhancedAutoFaceTarget);

        private static IntPtr pronounModule = IntPtr.Zero;
        public static GameObject* UITarget => (GameObject*)*(IntPtr*)(pronounModule + 0x290);

        private static delegate* unmanaged<IntPtr, uint, GameObject*> getGameObjectFromPronounID;
        public static GameObject* GetGameObjectFromPronounID(uint id) => getGameObjectFromPronounID(pronounModule, id);

        private static delegate* unmanaged<uint, GameObject*, byte> canUseActionOnGameObject;
        public static byte CanUseActionOnGameObject(uint actionID, GameObject* o) => canUseActionOnGameObject(actionID, o);

        public delegate byte UseActionDelegate(ActionManager* actionManager, uint actionType, uint actionID, long targetedActorID, uint param, uint useType, int pvp, IntPtr a8);
        public static Hook<UseActionDelegate> UseActionHook;
        private static byte UseActionDetour(ActionManager* actionManager, uint actionType, uint actionID, long targetedActorID, uint param, uint useType, int pvp, IntPtr a8)
            => ActionStackManager.OnUseAction(actionManager, actionType, actionID, targetedActorID, param, useType, pvp, a8);

        public static void Initialize()
        {
            try
            {
                actionManager = ActionManager.Instance();
                pronounModule = (IntPtr)Framework.Instance()->GetUiModule()->GetPronounModule();
                getGameObjectFromPronounID = (delegate* unmanaged<IntPtr, uint, GameObject*>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B D8 48 85 C0 0F 85 ?? ?? ?? ?? 8D 4F DD");
                canUseActionOnGameObject = (delegate* unmanaged<uint, GameObject*, byte>)DalamudApi.SigScanner.ScanText("48 89 5C 24 08 57 48 83 EC 20 48 8B DA 8B F9 E8 ?? ?? ?? ?? 4C 8B C3");
                UseActionHook = new Hook<UseActionDelegate>(DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 89 9F 14 79 02 00"), UseActionDetour);
                UseActionHook.Enable();
            }
            catch (Exception e)
            {
                PluginLog.Error($"Failed loading ReAction\n{e}");
            }
        }

        public static void Dispose() => UseActionHook?.Dispose();
    }
}
