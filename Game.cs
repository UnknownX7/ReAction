using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace ReAction
{
    public static unsafe class Game
    {
        public static ActionManager* actionManager;
        public static readonly Memory.Replacer allowQueuingReplacer = new("76 30 80 F9 04", new byte[] { 0xEB });
        public static readonly Memory.Replacer queueGroundTargetsReplacer = new("74 24 41 81 FE F5 0D 00 00", new byte[] { 0xEB }, ReAction.Config.EnableGroundTargetQueuing);

        // cmp byte ptr [r15+33h], 6 -> test byte ptr [r15+3Ah], 10
        public static readonly Memory.Replacer enhancedAutoFaceTargetReplacer1 = new("41 80 7F 33 06 75 1E 48 8D 0D", new byte[] { 0x41, 0xF6, 0x47, 0x3A, 0x10 }, ReAction.Config.EnableEnhancedAutoFaceTarget);
        public static readonly Memory.Replacer enhancedAutoFaceTargetReplacer2 = new("41 80 7F 33 06 74 22 49 8D 8E", new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0xEB }, ReAction.Config.EnableEnhancedAutoFaceTarget);

        // test byte ptr [r15+39], 04
        // jnz A7h
        public static readonly Memory.Replacer spellAutoAttackReplacer = new("41 B0 01 41 0F B6 D0 E9 ?? ?? ?? ?? 41 B0 01", new byte[] { 0x41, 0xF6, 0x47, 0x39, 0x04, 0x0F, 0x85, 0xA7, 0x00, 0x00, 0x00, 0x90 }, ReAction.Config.EnableSpellAutoAttacks);

        // cmp rcx, 0DDAh
        // jz 41h
        // jmp 18h
        public static readonly Memory.Replacer decomboMeditationReplacer = new("48 8B 05 ?? ?? ?? ?? 48 85 C0 74 3E 48 8B 0D", new byte[] { 0x48, 0x81, 0xFA, 0xDA, 0x0D, 0x00, 0x00, 0x74, 0x41, 0xEB, 0x18, 0x90 }, ReAction.Config.EnableDecomboMeditation);

        public static float AnimationLock => *(float*)((IntPtr)actionManager + 0x8);
        public static uint CastActionType => *(uint*)((IntPtr)actionManager + 0x28);
        public static uint CastActionID => *(uint*)((IntPtr)actionManager + 0x2C);
        public static uint CastTargetID => *(uint*)((IntPtr)actionManager + 0x38);
        public static bool IsQueued => *(bool*)((IntPtr)actionManager + 0x68);
        public static float ElapsedGCDRecastTime => *(float*)((IntPtr)actionManager + 0x618);
        public static float GCDRecastTime => *(float*)((IntPtr)actionManager + 0x61C);

        private static IntPtr pronounModule = IntPtr.Zero;
        public static GameObject* UITarget => (GameObject*)*(IntPtr*)(pronounModule + 0x290);

        public static long GetObjectID(GameObject* o)
        {
            var id = o->GetObjectID();
            return (id.Type * 0x1_0000_0000) | id.ObjectID;
        }

        private static delegate* unmanaged<long, GameObject*> getGameObjectFromObjectID;
        public static GameObject* GetGameObjectFromObjectID(long id) => getGameObjectFromObjectID(id);

        private static delegate* unmanaged<IntPtr, uint, GameObject*> getGameObjectFromPronounID;
        public static GameObject* GetGameObjectFromPronounID(uint id) => getGameObjectFromPronounID(pronounModule, id);

        private static delegate* unmanaged<uint, GameObject*, byte> canUseActionOnGameObject;
        public static bool CanUseActionOnGameObject(uint actionID, GameObject* o)
            => canUseActionOnGameObject(actionID, o) != 0 || ReAction.actionSheet.TryGetValue(actionID, out var a) && a.TargetArea;

        private static delegate* unmanaged<void> cancelCast;
        public static void CancelCast() => cancelCast();

        public static void TargetEnemy()
        {
            if (cameraManager == null || DalamudApi.ClientState.LocalPlayer is not { } p) return;

            var worldCamera = cameraManager[0];
            if (worldCamera == IntPtr.Zero) return;

            var hRotation = *(float*)(worldCamera + 0x130) + Math.PI * 1.5;
            var firstPerson = *(int*)(worldCamera + 0x170) == 0;
            if (firstPerson)
                hRotation -= Math.PI;

            const double doublePI = Math.PI * 2;
            const double halfCone = Math.PI * 0.35;
            var minRotation = (hRotation + doublePI - halfCone) % doublePI;
            var maxRotation = (hRotation + halfCone) % doublePI;

            static bool IsBetween(double val, double a, double b)
            {
                if (a > b)
                    return val >= a || val <= b;
                return val >= a && val <= b;
            }

            Dalamud.Game.ClientState.Objects.Types.GameObject closest = null;
            foreach (var o in DalamudApi.ObjectTable.Where(o => o.YalmDistanceX < 30
                && o.ObjectKind is ObjectKind.Player or ObjectKind.BattleNpc
                && ((BattleChara)o).CurrentHp > 0
                && CanUseActionOnGameObject(7, (GameObject*)o.Address)))
            {
                var posDiff = o.Position - p.Position;
                var angle = Math.Atan2(-posDiff.Z, posDiff.X) + Math.PI;
                if (IsBetween(angle, minRotation, maxRotation) && (closest == null || closest.YalmDistanceX > o.YalmDistanceX))
                    closest = o;
            }

            if (closest != null)
                DalamudApi.TargetManager.Target = closest;
        }

        private static delegate* unmanaged<GameObject*, float, void> setGameObjectRotation;
        private static IntPtr* cameraManager;
        public static void SetCharacterRotationToCamera()
        {
            if (cameraManager == null) return;

            var worldCamera = cameraManager[0];
            if (worldCamera == IntPtr.Zero) return;

            var hRotation = *(float*)(worldCamera + 0x130);
            var localPlayer = (GameObject*)DalamudApi.ClientState.LocalPlayer!.Address;
            setGameObjectRotation(localPlayer, hRotation + MathF.PI);
        }

        // Returns the log message (562 = LoS, 565 = Not Facing Target, 566 = Out of Range)
        private static delegate* unmanaged<uint, GameObject*, GameObject*, uint> getActionOutOfRangeOrLoS;
        // The game is dumb and I cannot check LoS easily because not facing the target will override it
        public static bool IsActionOutOfRange(uint actionID, GameObject* o) => DalamudApi.ClientState.LocalPlayer is { } p && o != null
            && getActionOutOfRangeOrLoS(actionID, (GameObject*)p.Address, o) is 566;

        private static delegate* unmanaged<ActionManager*, uint, uint, byte> canActionQueue;
        public static bool CanActionQueue(uint actionType, uint actionID) => canActionQueue(actionManager, actionType, actionID) != 0;

        public static uint GetActionStatus(uint actionType, uint actionID, long targetObjectID = 0xE000_0000, byte checkCooldown = 1, byte checkCasting = 1)
        {
            var func = (delegate* unmanaged[Stdcall]<ActionManager*, uint, uint, long, uint, uint, uint>)ActionManager.fpGetActionStatus;
            return func(actionManager, actionType, actionID, targetObjectID, checkCooldown, checkCasting);
        }

        private static delegate* unmanaged<HotBarSlot*, IntPtr, byte, uint, void> setHotbarSlot;
        public static void SetHotbarSlot(int hotbar, int slot, byte type, uint id)
        {
            if (hotbar is < 0 or > 9 || slot is < 0 or > 11) return;
            var raptureHotbarModule = Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();
            setHotbarSlot(raptureHotbarModule->HotBar[hotbar]->Slot[slot], *(IntPtr*)((IntPtr)raptureHotbarModule + 0x48), type, id);
        }

        public delegate byte UseActionDelegate(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget);
        public static Hook<UseActionDelegate> UseActionHook;
        private static byte UseActionDetour(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
            => ActionStackManager.OnUseAction(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);

        public static void Initialize()
        {
            try
            {
                actionManager = ActionManager.Instance();
                pronounModule = (IntPtr)Framework.Instance()->GetUiModule()->GetPronounModule();
                getGameObjectFromObjectID = (delegate* unmanaged<long, GameObject*>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 44 0F B6 C3 48 8B D0");
                getGameObjectFromPronounID = (delegate* unmanaged<IntPtr, uint, GameObject*>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B D8 48 85 C0 0F 85 ?? ?? ?? ?? 8D 4F DD");
                canUseActionOnGameObject = (delegate* unmanaged<uint, GameObject*, byte>)DalamudApi.SigScanner.ScanText("48 89 5C 24 08 57 48 83 EC 20 48 8B DA 8B F9 E8 ?? ?? ?? ?? 4C 8B C3");
                getActionOutOfRangeOrLoS = (delegate* unmanaged<uint, GameObject*, GameObject*, uint>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 85 C0 75 02 33 C0");
                canActionQueue = (delegate* unmanaged<ActionManager*, uint, uint, byte>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 74 37 8B 84 24 90 00 00 00");
                cancelCast = (delegate* unmanaged<void>)DalamudApi.SigScanner.ScanText("48 83 EC 38 33 D2 C7 44 24 20 00 00 00 00 45 33 C9");
                setGameObjectRotation = (delegate* unmanaged<GameObject*, float, void>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 83 FE 4F");
                cameraManager = (IntPtr*)DalamudApi.SigScanner.GetStaticAddressFromSig("48 8D 35 ?? ?? ?? ?? 48 8B 09");
                setHotbarSlot = (delegate* unmanaged<HotBarSlot*, IntPtr, byte, uint, void>)DalamudApi.SigScanner.ScanText("E8 ?? ?? ?? ?? 4C 39 6F 08");
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
