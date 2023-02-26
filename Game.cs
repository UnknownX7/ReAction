using System;
using System.Linq;
using System.Runtime.InteropServices;
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

[InjectSignatures]
public static unsafe class Game
{
    public static readonly AsmPatch allowQueuingPatch = new("76 2F 80 F9 04", new byte[] { 0xEB });
    public static readonly AsmPatch queueGroundTargetsPatch = new("74 24 41 81 FE F5 0D 00 00", new byte[] { 0xEB }, ReAction.Config.EnableGroundTargetQueuing);

    // cmp byte ptr [r15+33h], 6 -> test byte ptr [r15+3Ah], 10
    public static readonly AsmPatch enhancedAutoFaceTargetPatch = new("41 80 7F 33 06 75 1E 48 8D 0D", new byte[] { 0x41, 0xF6, 0x47, 0x3A, 0x10 });
    public static readonly AsmPatch removeAutoFaceGroundTargetPatch = new("41 80 7F 33 06 74 22 49 8D 8E", new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0xEB }, ReAction.Config.EnableEnhancedAutoFaceTarget);

    // test byte ptr [r15+39], 04
    // jnz A7h
    public static readonly AsmPatch spellAutoAttackPatch = new("41 B0 01 41 0F B6 D0 E9 ?? ?? ?? ?? 41 B0 01", new byte[] { 0x41, 0xF6, 0x47, 0x39, 0x04, 0x0F, 0x85, 0xA7, 0x00, 0x00, 0x00, 0x90 }, ReAction.Config.EnableSpellAutoAttacks && ReAction.Config.EnableSpellAutoAttacksOutOfCombat);

    public static readonly AsmPatch allowUnassignableActionsPatch = new("75 07 32 C0 E9 ?? ?? ?? ?? 48 8B 00", new byte[] { 0xEB }, ReAction.Config.EnableUnassignableActions);

    // mov eax, 1000f
    // movd xmm1, eax
    // mulss xmm0, xmm1
    // cvttss2si rcx, xmm0
    public static readonly AsmPatch waitSyntaxDecimalPatch = new("F3 0F 58 05 ?? ?? ?? ?? F3 48 0F 2C C0 69 C8",
        new byte[] {
            0xB8, 0x00, 0x00, 0x7A, 0x44,
            0x66, 0x0F, 0x6E, 0xC8,
            0xF3, 0x0F, 0x59, 0xC1,
            0xF3, 0x48, 0x0F, 0x2C, 0xC8,
            0x90,
            0x90, 0x90, 0x90, 0x90, 0x90
        },
        ReAction.Config.EnableFractionality);

    // mov eax, 1000f
    // movd xmm0, eax
    // mulss xmm1, xmm0
    // cvttss2si rcx, xmm1
    // mov [rbx+58h], ecx
    // jmp
    public static readonly AsmPatch waitCommandDecimalPatch = new("F3 0F 58 0D ?? ?? ?? ?? F3 48 0F 2C C1 69 C8",
        new byte[] {
            0xB8, 0x00, 0x00, 0x7A, 0x44,
            0x66, 0x0F, 0x6E, 0xC0,
            0xF3, 0x0F, 0x59, 0xC8,
            0xF3, 0x48, 0x0F, 0x2C, 0xC9,
            0x90,
            0x89, 0x4B, 0x58,
            0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
            0xEB // 0x1F
        },
        ReAction.Config.EnableFractionality);

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

    public delegate Bool UseActionDelegate(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget);
    [ClientStructs(typeof(FFXIVClientStructs.FFXIV.Client.Game.ActionManager.MemberFunctionPointers))]
    public static Hook<UseActionDelegate> UseActionHook;
    private static Bool UseActionDetour(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget) =>
        ActionStackManager.OnUseAction(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);

    [Signature("E8 ?? ?? ?? ?? 8B 4F 44 33 D2")]
    public static delegate* unmanaged<ActionManager*, uint, uint, int> fpGetAdditionalRecastGroup;
    [Signature("E8 ?? ?? ?? ?? 84 C0 74 12 48 83 FF 0F")]
    public static delegate* unmanaged<ActionManager*, uint, Bool> fpCanUseActionAsCurrentClass;
    public static Bool CanQueueActionDetour(ActionManager* actionManager, uint actionType, uint actionID) => Modules.QueueAdjustments.OnCanQueueAction(actionManager, actionType, actionID);

    private static (string Name, uint DataID) focusTargetInfo = (null, 0);
    public delegate void SetFocusTargetByObjectIDDelegate(TargetSystem* targetSystem, long objectID);
    [Signature("E8 ?? ?? ?? ?? BA 0C 00 00 00 48 8D 0D")]
    public static Hook<SetFocusTargetByObjectIDDelegate> SetFocusTargetByObjectIDHook;
    private static void SetFocusTargetByObjectIDDetour(TargetSystem* targetSystem, long objectID)
    {
        SetFocusTargetByObjectIDHook.Original(targetSystem, objectID);
        focusTargetInfo = DalamudApi.TargetManager.FocusTarget is { } o ? (o.Name.ToString(), o.DataId) : (null, 0);
    }

    private delegate GameObject* ResolvePlaceholderDelegate(PronounModule* pronounModule, string text, Bool defaultToTarget, Bool allowPlayerNames);
    [Signature("E8 ?? ?? ?? ?? 48 8B 5C 24 30 EB 0C")]
    private static Hook<ResolvePlaceholderDelegate> ResolvePlaceholderHook;
    private static GameObject* ResolvePlaceholderDetour(PronounModule* pronounModule, string text, Bool defaultToTarget, Bool allowPlayerNames) =>
        ResolvePlaceholderHook.Original(pronounModule, text, defaultToTarget, allowPlayerNames || ReAction.Config.EnablePlayerNamesInCommands);

    private static GameObject* GetGameObjectFromPronounIDDetour(PronounModule* pronounModule, PronounID pronounID)
    {
        var ret = Common.getGameObjectFromPronounID.Original(pronounModule, pronounID);
        return (ret != null || !PronounManager.CustomPronouns.TryGetValue((uint)pronounID, out var pronoun)) ? ret : pronoun.GetGameObject();
    }

    private delegate uint GetTextCommandParamIDDelegate(PronounModule* pronounModule, nint* text, int len); // Probably not an issue, but this function doesn't get called if the length is > 31
    [Signature("48 89 5C 24 10 48 89 6C 24 18 56 48 83 EC 20 48 83 79 18 00")] // E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? E8 ?? ?? ?? ?? CC CC (lol)
    private static Hook<GetTextCommandParamIDDelegate> GetTextCommandParamIDHook;
    private static uint GetTextCommandParamIDDetour(PronounModule* pronounModule, nint* bytePtrPtr, int len)
    {
        var ret = GetTextCommandParamIDHook.Original(pronounModule, bytePtrPtr, len);
        return (ret != 0 || !PronounManager.CustomPlaceholders.TryGetValue(Marshal.PtrToStringAnsi(*bytePtrPtr, len), out var pronoun)) ? ret : pronoun.ID;
    }

    public static void RefocusTarget()
    {
        if (focusTargetInfo.Name == null || DalamudApi.TargetManager.FocusTarget != null) return;

        var foundTarget = DalamudApi.ObjectTable.FirstOrDefault(o => o.DataId == focusTargetInfo.DataID && o.Name.ToString() == focusTargetInfo.Name);
        if (foundTarget == null) return;

        DalamudApi.TargetManager.FocusTarget = foundTarget;
    }

    private delegate void ExecuteMacroDelegate(RaptureShellModule* raptureShellModule, RaptureMacroModule.Macro* macro);
    [ClientStructs(typeof(RaptureShellModule.MemberFunctionPointers))]
    private static Hook<ExecuteMacroDelegate> ExecuteMacroHook;
    private static void ExecuteMacroDetour(RaptureShellModule* raptureShellModule, RaptureMacroModule.Macro* macro)
    {
        if (ReAction.Config.EnableMacroQueue)
            queueACCommandPatch.Enable();
        else
            queueACCommandPatch.Disable();
        ExecuteMacroHook.Original(raptureShellModule, macro);
    }

    private static bool hotbarPressed = false;
    public delegate void CheckHotbarBindingsDelegate(nint a1, byte a2);
    [Signature("48 89 4C 24 08 53 41 55 41 57"), SignatureEx(EnableHook = false)]
    public static Hook<CheckHotbarBindingsDelegate> CheckHotbarBindingsHook;
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

    private static Bool IsInputIDPressedDetour(InputData* inputData, uint id)
    {
        var ret = inputData->IsInputIDHeld(id);
        if (ret)
            hotbarPressed = true;
        return ret;
    }

    public static void Initialize()
    {
        if (Common.ActionManager == null || !Common.getGameObjectFromPronounID.IsValid || !ActionManager.canUseActionOnGameObject.IsValid || !ActionManager.canQueueAction.IsValid)
            throw new ApplicationException("Failed to find core signatures!");

        Common.getGameObjectFromPronounID.CreateHook(GetGameObjectFromPronounIDDetour);
        ActionManager.canQueueAction.CreateHook(CanQueueActionDetour);
        InputData.isInputIDPressed.CreateHook(IsInputIDPressedDetour, false);
    }

    public static void Dispose()
    {

    }
}