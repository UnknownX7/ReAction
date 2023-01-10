using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace ReAction;

public unsafe class Game
{
    public static ActionManager* actionManager;
    public static readonly Memory.Replacer allowQueuingReplacer = new("76 2F 80 F9 04", new byte[] { 0xEB });
    public static readonly Memory.Replacer queueGroundTargetsReplacer = new("74 24 41 81 FE F5 0D 00 00", new byte[] { 0xEB }, ReAction.Config.EnableGroundTargetQueuing);

    // cmp byte ptr [r15+33h], 6 -> test byte ptr [r15+3Ah], 10
    public static readonly Memory.Replacer enhancedAutoFaceTargetReplacer1 = new("41 80 7F 33 06 75 1E 48 8D 0D", new byte[] { 0x41, 0xF6, 0x47, 0x3A, 0x10 }, ReAction.Config.EnableEnhancedAutoFaceTarget);
    public static readonly Memory.Replacer enhancedAutoFaceTargetReplacer2 = new("41 80 7F 33 06 74 22 49 8D 8E", new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0xEB }, ReAction.Config.EnableEnhancedAutoFaceTarget);

    // test byte ptr [r15+39], 04
    // jnz A7h
    public static readonly Memory.Replacer spellAutoAttackReplacer = new("41 B0 01 41 0F B6 D0 E9 ?? ?? ?? ?? 41 B0 01", new byte[] { 0x41, 0xF6, 0x47, 0x39, 0x04, 0x0F, 0x85, 0xA7, 0x00, 0x00, 0x00, 0x90 }, ReAction.Config.EnableSpellAutoAttacks);

    // 6.2 inlined this function, but the original still exists so the sig matches both
    // cmp rbx, 0DDAh
    // jz 41h
    // jmp 18h
    public static readonly Memory.Replacer decomboMeditationReplacer = new("48 8B 05 ?? ?? ?? ?? 48 85 C0 74 3E 48 8B 0D",
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
    public static readonly Memory.Replacer decomboBunshinReplacer = new("BA A3 0A 00 00 48 8D 0D ?? ?? ?? ?? E8",
        new byte[] {
            0x8B, 0xC3,
            0xEB, 0x1E
        },
        ReAction.Config.EnableDecomboBunshin);

    // mov eax, ebx
    // ret
    public static readonly Memory.Replacer decomboWanderersMinuetReplacer = new("48 8B 0D ?? ?? ?? ?? 48 85 C9 74 27 48 8B 05",
        new byte[] {
            0x8B, 0xC3,
            0xC3,
            0x90, 0x90, 0x90, 0x90
        },
        ReAction.Config.EnableDecomboWanderersMinuet);

    // Also inlined
    // mov eax, ebx
    // jmp 1Eh
    public static readonly Memory.Replacer decomboLiturgyReplacer = new("BA 95 0A 00 00 48 8D 0D ?? ?? ?? ?? E8",
        new byte[] {
            0x8B, 0xC3,
            0xEB, 0x1E
        },
        ReAction.Config.EnableDecomboLiturgy);

    // Again... inlined...
    // mov eax, ebx
    // nop (until an already existing jmp to a ret)
    public static readonly Memory.Replacer decomboEarthlyStarReplacer = new("48 83 3D ?? ?? ?? ?? ?? 75 0A B8 0F 1D 00 00",
        new byte[] {
            0x8B, 0xC3,
            0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
            0x90, 0x90,
            0x90, 0x90, 0x90, 0x90, 0x90
        },
        ReAction.Config.EnableDecomboEarthlyStar);

    public static readonly Memory.Replacer queueACCommandReplacer = new("02 00 00 00 41 8B D7 89", new byte[] { 0x00 });

    public static float AnimationLock => *(float*)((nint)actionManager + 0x8);
    public static uint CastActionType => *(uint*)((nint)actionManager + 0x28);
    public static uint CastActionID => *(uint*)((nint)actionManager + 0x2C);
    public static uint CastTargetID => *(uint*)((nint)actionManager + 0x38);
    public static bool IsQueued => *(bool*)((nint)actionManager + 0x68);
    public static float ElapsedGCDRecastTime => *(float*)((nint)actionManager + 0x5F0);
    public static float GCDRecastTime => *(float*)((nint)actionManager + 0x5F4);

    private static nint pronounModule = nint.Zero;
    public static GameObject* UITarget => (GameObject*)*(nint*)(pronounModule + 0x290);

    public static long GetObjectID(GameObject* o)
    {
        var id = o->GetObjectID();
        return (id.Type * 0x1_0000_0000) | id.ObjectID;
    }

    [Signature("E8 ?? ?? ?? ?? 44 0F B6 C3 48 8B D0")]
    private static delegate* unmanaged<long, GameObject*> getGameObjectFromObjectID;
    public static GameObject* GetGameObjectFromObjectID(long id) => getGameObjectFromObjectID(id);

    [Signature("E8 ?? ?? ?? ?? 48 8B D8 48 85 C0 0F 85 ?? ?? ?? ?? 8D 4F DD")]
    private static delegate* unmanaged<nint, uint, GameObject*> getGameObjectFromPronounID;
    public static GameObject* GetGameObjectFromPronounID(uint id) => getGameObjectFromPronounID(pronounModule, id);

    [Signature("48 89 5C 24 08 57 48 83 EC 20 48 8B DA 8B F9 E8 ?? ?? ?? ?? 4C 8B C3")]
    private static delegate* unmanaged<uint, GameObject*, byte> canUseActionOnGameObject;
    public static bool CanUseActionOnGameObject(uint actionID, GameObject* o)
        => canUseActionOnGameObject(actionID, o) != 0 || ReAction.actionSheet.TryGetValue(actionID, out var a) && a.TargetArea;

    [Signature("48 83 EC 38 33 D2 C7 44 24 20 00 00 00 00 45 33 C9")]
    private static delegate* unmanaged<void> cancelCast;
    public static void CancelCast() => cancelCast();

    public static void TargetEnemy()
    {
        if (cameraManager == null || DalamudApi.ClientState.LocalPlayer is not { } p) return;

        var worldCamera = cameraManager[0];
        if (worldCamera == nint.Zero) return;

        var hRotation = *(float*)(worldCamera + 0x130) + Math.PI * 1.5;
        var flipped = *(int*)(worldCamera + 0x170) == *(byte*)(worldCamera + 0x1E4);
        if (flipped)
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

    [Signature("E8 ?? ?? ?? ?? 83 FE 4F")]
    private static delegate* unmanaged<GameObject*, float, void> setGameObjectRotation;
    [Signature("4C 8D 35 ?? ?? ?? ?? 85 D2", ScanType = ScanType.StaticAddress)]
    private static nint* cameraManager;
    public static void SetCharacterRotationToCamera()
    {
        if (cameraManager == null) return;

        var worldCamera = cameraManager[0];
        if (worldCamera == nint.Zero) return;

        var hRotation = *(float*)(worldCamera + 0x130);
        var flipped = *(int*)(worldCamera + 0x170) == *(byte*)(worldCamera + 0x1E4);
        var localPlayer = (GameObject*)DalamudApi.ClientState.LocalPlayer!.Address;
        setGameObjectRotation(localPlayer, !flipped ? (hRotation > 0 ? hRotation - MathF.PI : hRotation + MathF.PI) : hRotation);
    }

    // Returns the log message (562 = LoS, 565 = Not Facing Target, 566 = Out of Range)
    [Signature("E8 ?? ?? ?? ?? 85 C0 75 02 33 C0")]
    private static delegate* unmanaged<uint, GameObject*, GameObject*, uint> getActionOutOfRangeOrLoS;
    // The game is dumb and I cannot check LoS easily because not facing the target will override it
    public static bool IsActionOutOfRange(uint actionID, GameObject* o) => DalamudApi.ClientState.LocalPlayer is { } p && o != null
        && getActionOutOfRangeOrLoS(actionID, (GameObject*)p.Address, o) is 566;

    [Signature("E8 ?? ?? ?? ?? 84 C0 74 37 8B 84 24 ?? ?? 00 00")]
    private static delegate* unmanaged<ActionManager*, uint, uint, byte> canActionQueue;
    public static bool CanActionQueue(uint actionType, uint actionID) => canActionQueue(actionManager, actionType, actionID) != 0;

    [Signature("E8 ?? ?? ?? ?? 4C 39 6F 08")]
    private static delegate* unmanaged<HotBarSlot*, nint, byte, uint, void> setHotbarSlot;
    public static void SetHotbarSlot(int hotbar, int slot, byte type, uint id)
    {
        if (hotbar is < 0 or > 17 || (hotbar < 10 ? slot is < 0 or > 11 : slot is < 0 or > 15)) return;
        var raptureHotbarModule = Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();
        setHotbarSlot(raptureHotbarModule->HotBar[hotbar]->Slot[slot], *(nint*)((nint)raptureHotbarModule + 0x48), type, id);
    }

    public delegate byte UseActionDelegate(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget);
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

    private static RaptureShellModule* raptureShellModule;
    public static bool IsMacroRunning => raptureShellModule->MacroCurrentLine >= 0;
    public delegate void ExecuteMacroDelegate(RaptureShellModule* raptureShellModule, RaptureMacroModule.Macro* macro);
    public static Hook<ExecuteMacroDelegate> ExecuteMacroHook;
    public static void ExecuteMacroDetour(RaptureShellModule* raptureShellModule, RaptureMacroModule.Macro* macro)
    {
        queueACCommandReplacer.Disable();
        ExecuteMacroHook.Original(raptureShellModule, macro);
    }

    public static void Initialize()
    {
        actionManager = ActionManager.Instance();
        pronounModule = (nint)Framework.Instance()->GetUiModule()->GetPronounModule();
        raptureShellModule = RaptureShellModule.Instance;

        // TODO change back to static whenever support is added
        //SignatureHelper.Initialise(typeof(Game));
        SignatureHelper.Initialise(new Game());
        UseActionHook = new Hook<UseActionDelegate>((nint)ActionManager.MemberFunctionPointers.UseAction, UseActionDetour);
        ExecuteMacroHook = new Hook<ExecuteMacroDelegate>((nint)RaptureShellModule.MemberFunctionPointers.ExecuteMacro, ExecuteMacroDetour);
        UseActionHook.Enable();
        SetFocusTargetByObjectIDHook.Enable();
        ExecuteMacroHook.Enable();
    }

    public static void Dispose()
    {
        UseActionHook?.Dispose();
        SetFocusTargetByObjectIDHook?.Dispose();
        ExecuteMacroHook?.Dispose();
    }
}