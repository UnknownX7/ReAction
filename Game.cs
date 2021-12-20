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

        public static uint CastActionType => *(uint*)((IntPtr)actionManager + 0x28);
        public static uint CastActionID => *(uint*)((IntPtr)actionManager + 0x2C);
        public static uint CastTargetID => *(uint*)((IntPtr)actionManager + 0x38);

        private static IntPtr pronounModule = IntPtr.Zero;
        public static GameObject* UITarget => (GameObject*)*(IntPtr*)(pronounModule + 0x290);

        private static delegate* unmanaged<IntPtr, uint, GameObject*> getGameObjectFromPronounID;
        public static GameObject* GetGameObjectFromPronounID(uint id) => getGameObjectFromPronounID(pronounModule, id);

        private static delegate* unmanaged<uint, GameObject*, byte> canUseActionOnGameObject;
        public static bool CanUseActionOnGameObject(uint actionID, GameObject* o) => canUseActionOnGameObject(actionID, o) != 0;

        private static delegate* unmanaged<void> cancelCast;
        public static void CancelCast() => cancelCast();

        private static delegate* unmanaged<void> targetEnemyNext;
        public static void TargetEnemyNext() => targetEnemyNext();

        // Returns the log message (562 = LoS, 565 = Not Facing Target, 566 = Out of Range)
        private static delegate* unmanaged<uint, GameObject*, GameObject*, uint> getActionOutOfRangeOrLoS;
        // The game is dumb and I cannot check LoS easily because not facing the target will override it
        public static bool IsActionOutOfRange(uint actionID, GameObject* o) => DalamudApi.ClientState.LocalPlayer is { } p && o != null
            && getActionOutOfRangeOrLoS(actionID, (GameObject*)p.Address, o) is 566;

        public delegate byte UseActionDelegate(ActionManager* actionManager, uint actionType, uint actionID, uint targetObjectID, uint param, uint useType, int pvp, IntPtr a8);
        public static Hook<UseActionDelegate> UseActionHook;
        private static byte UseActionDetour(ActionManager* actionManager, uint actionType, uint actionID, uint targetObjectID, uint param, uint useType, int pvp, IntPtr a8)
            => ActionStackManager.OnUseAction(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, a8);

        public static void Initialize()
        {
            try
            {
                actionManager = ActionManager.Instance();
                pronounModule = (IntPtr)Framework.Instance()->GetUiModule()->GetPronounModule();
                getGameObjectFromPronounID = (delegate* unmanaged<IntPtr, uint, GameObject*>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B D8 48 85 C0 0F 85 ?? ?? ?? ?? 8D 4F DD");
                canUseActionOnGameObject = (delegate* unmanaged<uint, GameObject*, byte>)DalamudApi.SigScanner.ScanText("48 89 5C 24 08 57 48 83 EC 20 48 8B DA 8B F9 E8 ?? ?? ?? ?? 4C 8B C3");
                getActionOutOfRangeOrLoS = (delegate* unmanaged<uint, GameObject*, GameObject*, uint>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 85 C0 75 02 33 C0");
                cancelCast = (delegate* unmanaged<void>)DalamudApi.SigScanner.ScanText("48 83 EC 38 33 D2 C7 44 24 20 00 00 00 00 45 33 C9");
                targetEnemyNext = (delegate* unmanaged<void>)DalamudApi.SigScanner.ScanText("48 83 EC 28 33 D2 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 33 C0");
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
