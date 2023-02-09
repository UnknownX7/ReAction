using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using Hypostasis.Game.Structures;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace ReAction;

public static unsafe class Game
{
    public static readonly AsmPatch allowQueuingPatch = new("76 2F 80 F9 04", new byte[] { 0xEB });
    public static readonly AsmPatch queueGroundTargetsPatch = new("74 24 41 81 FE F5 0D 00 00", new byte[] { 0xEB }, ReAction.Config.EnableGroundTargetQueuing);

    // cmp byte ptr [r15+33h], 6 -> test byte ptr [r15+3Ah], 20
    public static readonly AsmPatch enhancedAutoFaceTargetPatch1 = new("41 80 7F 33 06 75 1E 48 8D 0D", new byte[] { 0x41, 0xF6, 0x47, 0x3A, 0x20 }, ReAction.Config.EnableEnhancedAutoFaceTarget);
    public static readonly AsmPatch enhancedAutoFaceTargetPatch2 = new("41 80 7F 33 06 74 22 49 8D 8E", new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0xEB }, ReAction.Config.EnableEnhancedAutoFaceTarget);

    // test byte ptr [r15+39], 04
    // jnz A7h
    public static readonly AsmPatch spellAutoAttackPatch = new("41 B0 01 41 0F B6 D0 E9 ?? ?? ?? ?? 41 B0 01", new byte[] { 0x41, 0xF6, 0x47, 0x39, 0x04, 0x0F, 0x85, 0xA7, 0x00, 0x00, 0x00, 0x90 });

    // 6.2 inlined this function, but the original still exists so the sig matches both
    // cmp rbx, 0DDAh
    // jz 41h
    // jmp 18h
    public static readonly AsmPatch decomboMeditationPatch = new("48 8B 05 ?? ?? ?? ?? 48 85 C0 74 3E 48 8B 0D",
        new byte[] {
            0x48, 0x81, 0xFB, 0xDA, 0x0D, 0x00, 0x00,
            0x74, 0x41,
            0xEB, 0x18,
            0x90
        },
        ReAction.Config.EnableDecomboMeditation);

    // Inlined, same as above
    // mov eax, ebx
    // jmp 1Eh
    public static readonly AsmPatch decomboBunshinPatch = new("BA A3 0A 00 00 48 8D 0D ?? ?? ?? ?? E8",
        new byte[] {
            0x8B, 0xC3,
            0xEB, 0x1E
        },
        ReAction.Config.EnableDecomboBunshin);

    // mov eax, ebx
    // ret
    public static readonly AsmPatch decomboWanderersMinuetPatch = new("48 8B 0D ?? ?? ?? ?? 48 85 C9 74 27 48 8B 05",
        new byte[] {
            0x8B, 0xC3,
            0xC3,
            0x90, 0x90, 0x90, 0x90
        },
        ReAction.Config.EnableDecomboWanderersMinuet);

    // Also inlined
    // mov eax, ebx
    // jmp 1Eh
    public static readonly AsmPatch decomboLiturgyPatch = new("BA 95 0A 00 00 48 8D 0D ?? ?? ?? ?? E8",
        new byte[] {
            0x8B, 0xC3,
            0xEB, 0x1E
        },
        ReAction.Config.EnableDecomboLiturgy);

    // Again... inlined...
    // mov eax, ebx
    // nop (until an already existing jmp to a ret)
    public static readonly AsmPatch decomboEarthlyStarPatch = new("48 83 3D ?? ?? ?? ?? ?? 75 0A B8 0F 1D 00 00",
        new byte[] {
            0x8B, 0xC3,
            0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
            0x90, 0x90,
            0x90, 0x90, 0x90, 0x90, 0x90
        },
        ReAction.Config.EnableDecomboEarthlyStar);

    public static readonly AsmPatch queueACCommandPatch = new("02 00 00 00 41 8B D7 89", new byte[] { 0x64 }, ReAction.Config.EnableMacroQueue);

    public static long GetObjectID(GameObject* o)
    {
        var id = o->GetObjectID();
        return (id.Type * 0x1_0000_0000) | id.ObjectID;
    }

    [Signature("E8 ?? ?? ?? ?? 44 0F B6 C3 48 8B D0")]
    public static delegate* unmanaged<long, GameObject*> fpGetGameObjectFromObjectID;
    public static GameObject* GetGameObjectFromObjectID(long id) => fpGetGameObjectFromObjectID(id);

    [Signature("48 83 EC 38 33 D2 C7 44 24 20 00 00 00 00 45 33 C9")]
    public static delegate* unmanaged<void> fpCancelCast;
    public static void CancelCast() => fpCancelCast();

    public static void TargetEnemy()
    {
        if (DalamudApi.ClientState.LocalPlayer is not { } p) return;

        var worldCamera = Common.CameraManager->WorldCamera;
        if (worldCamera == null) return;

        var hRotation = worldCamera->currentHRotation + Math.PI * 1.5;
        if (worldCamera->IsHRotationOffset)
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
            && ActionManager.CanUseActionOnGameObject(7, (GameObject*)o.Address)))
        {
            var posDiff = o.Position - p.Position;
            var angle = Math.Atan2(-posDiff.Z, posDiff.X) + Math.PI;
            if (IsBetween(angle, minRotation, maxRotation) && (closest == null || closest.YalmDistanceX > o.YalmDistanceX))
                closest = o;
        }

        if (closest != null)
            DalamudApi.TargetManager.Target = closest;
    }

    [Signature("E8 ?? ?? ?? ?? 83 FE 4F")]
    public static delegate* unmanaged<GameObject*, float, void> fpSetGameObjectRotation;
    public static void SetCharacterRotationToCamera()
    {
        var worldCamera = Common.CameraManager->WorldCamera;
        if (worldCamera == null) return;
        fpSetGameObjectRotation((GameObject*)DalamudApi.ClientState.LocalPlayer!.Address, worldCamera->GameObjectHRotation);
    }

    // The game is dumb and I cannot check LoS easily because not facing the target will override it
    public static bool IsActionOutOfRange(uint actionID, GameObject* o) => DalamudApi.ClientState.LocalPlayer is { } p && o != null
        && FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetActionInRangeOrLoS(actionID, (GameObject*)p.Address, o) is 566; // Returns the log message (562 = LoS, 565 = Not Facing Target, 566 = Out of Range)

    [Signature("E8 ?? ?? ?? ?? 4C 39 6F 08")]
    public static delegate* unmanaged<HotBarSlot*, UIModule*, byte, uint, void> fpSetHotbarSlot;
    public static void SetHotbarSlot(int hotbar, int slot, byte type, uint id)
    {
        if (hotbar is < 0 or > 17 || (hotbar < 10 ? slot is < 0 or > 11 : slot is < 0 or > 15)) return;
        var raptureHotbarModule = Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();
        fpSetHotbarSlot(raptureHotbarModule->HotBar[hotbar]->Slot[slot], raptureHotbarModule->UiModule, type, id);
    }

    public delegate byte UseActionDelegate(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget);
    [ClientStructs(typeof(FFXIVClientStructs.FFXIV.Client.Game.ActionManager.MemberFunctionPointers))]
    public static Hook<UseActionDelegate> UseActionHook;
    private static byte UseActionDetour(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
        => ActionStackManager.OnUseAction(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);

    private static (string Name, uint DataID) focusTargetInfo = (null, 0);
    public delegate void SetFocusTargetByObjectIDDelegate(TargetSystem* targetSystem, long objectID);
    [Signature("E8 ?? ?? ?? ?? BA 0C 00 00 00 48 8D 0D")]
    public static Hook<SetFocusTargetByObjectIDDelegate> SetFocusTargetByObjectIDHook;
    private static void SetFocusTargetByObjectIDDetour(TargetSystem* targetSystem, long objectID)
    {
        SetFocusTargetByObjectIDHook.Original(targetSystem, objectID);
        focusTargetInfo = DalamudApi.TargetManager.FocusTarget is { } o ? (o.Name.ToString(), o.DataId) : (null, 0);
    }

    public static void RefocusTarget()
    {
        if (focusTargetInfo.Name == null || DalamudApi.TargetManager.FocusTarget != null) return;

        var foundTarget = DalamudApi.ObjectTable.FirstOrDefault(o => o.DataId == focusTargetInfo.DataID && o.Name.ToString() == focusTargetInfo.Name);
        if (foundTarget == null) return;

        DalamudApi.TargetManager.FocusTarget = foundTarget;
    }

    public delegate void ExecuteMacroDelegate(RaptureShellModule* raptureShellModule, RaptureMacroModule.Macro* macro);
    [ClientStructs(typeof(RaptureShellModule.MemberFunctionPointers))]
    public static Hook<ExecuteMacroDelegate> ExecuteMacroHook;
    public static void ExecuteMacroDetour(RaptureShellModule* raptureShellModule, RaptureMacroModule.Macro* macro)
    {
        if (ReAction.Config.EnableMacroQueue)
            queueACCommandPatch.Enable();
        else
            queueACCommandPatch.Disable();
        ExecuteMacroHook.Original(raptureShellModule, macro);
    }

    public static void Initialize()
    {
        DalamudApi.SigScanner.Inject(typeof(Game));
        Common.InitializeStructure<ActionManager>(false);
        Common.GetGameObjectFromPronounID(Common.PronounID.None); // Test that this is working
        if (Common.ActionManager == null || ActionManager.fpCanUseActionOnGameObject == null || ActionManager.fpCanActionQueue == null)
            throw new ApplicationException("Failed to find core signatures!");
    }

    public static void Dispose()
    {

    }
}