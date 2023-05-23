using System;
using System.Linq;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using Hypostasis.Game.Structures;
using Camera = FFXIVClientStructs.FFXIV.Client.Game.Camera;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace ReAction;

[HypostasisInjection]
public static unsafe class Game
{
    public const uint InvalidObjectID = 0xE0000000;

    public static readonly AsmPatch queueGroundTargetsPatch = new("74 20 81 FD F5 0D 00 00", new byte?[] { 0xEB }, ReAction.Config.EnableGroundTargetQueuing);

    // test byte ptr [r15+39], 04
    // jnz A7h
    public static readonly AsmPatch spellAutoAttackPatch = new("41 B0 01 41 0F B6 D0 E9 ?? ?? ?? ?? 41 B0 01", new byte?[] { 0x41, 0xF6, 0x47, 0x39, 0x04, 0x0F, 0x85, 0xA7, 0x00, 0x00, 0x00, 0x90 }, ReAction.Config.EnableSpellAutoAttacks && ReAction.Config.EnableSpellAutoAttacksOutOfCombat);

    public static readonly AsmPatch allowUnassignableActionsPatch = new("75 07 32 C0 E9 ?? ?? ?? ?? 48 8B 00", new byte?[] { 0xEB }, ReAction.Config.EnableUnassignableActions);

    // mov eax, 1000f
    // movd xmm1, eax
    // mulss xmm0, xmm1
    // cvttss2si rcx, xmm0
    public static readonly AsmPatch waitSyntaxDecimalPatch = new("F3 0F 58 05 ?? ?? ?? ?? F3 48 0F 2C C0 69 C8",
        new byte?[] {
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
        new byte?[] {
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

    public static readonly AsmPatch queueACCommandPatch = new("02 00 00 00 41 8B D7 89", new byte?[] { 0x64 }, ReAction.Config.EnableMacroQueue);

    public static long GetObjectID(GameObject* o)
    {
        if (o == null) return InvalidObjectID;

        var id = o->GetObjectID();
        return (id.Type * 0x1_0000_0000) | id.ObjectID;
    }

    [HypostasisSignatureInjection("E8 ?? ?? ?? ?? 44 0F B6 C3 48 8B D0")]
    public static delegate* unmanaged<long, GameObject*> fpGetGameObjectFromObjectID;
    public static GameObject* GetGameObjectFromObjectID(long id) => fpGetGameObjectFromObjectID(id);

    // The game is dumb and I cannot check LoS easily because not facing the target will override it
    public static bool IsActionOutOfRange(uint actionID, GameObject* o) => DalamudApi.ClientState.LocalPlayer is { } p && o != null
        && FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetActionInRangeOrLoS(actionID, (GameObject*)p.Address, o) is 566; // Returns the log message (562 = LoS, 565 = Not Facing Target, 566 = Out of Range)

    public static GameObject* GetMouseOverObject(GameObjectArray* array)
    {
        if (array->Length == 0) return null;

        var targetSystem = TargetSystem.Instance();
        var camera = (Camera*)Common.CameraManager->worldCamera;
        if (targetSystem == null || camera == null || targetSystem->MouseOverTarget == null) return null;

        // Nameplates fucking suck (I am aware nameplates aren't restricted to the objects in the array)
        var nameplateTarget = targetSystem->MouseOverNameplateTarget;
        if (nameplateTarget != null)
        {
            for (int i = 0; i < array->Length; i++)
            {
                if (array->Objects[i] == (nint)nameplateTarget)
                    return nameplateTarget;
            }
        }

        return targetSystem->GetMouseOverObject(Common.InputData->GetAxisInput(0), Common.InputData->GetAxisInput(1), array, camera);
    }

    [HypostasisSignatureInjection("E8 ?? ?? ?? ?? 4C 39 6F 08")]
    private static delegate* unmanaged<HotBarSlot*, UIModule*, byte, uint, void> fpSetHotbarSlot;
    public static void SetHotbarSlot(int hotbar, int slot, byte type, uint id)
    {
        if (fpSetHotbarSlot == null || hotbar is < 0 or > 17 || (hotbar < 10 ? slot is < 0 or > 11 : slot is < 0 or > 15)) return;
        var raptureHotbarModule = Framework.Instance()->GetUiModule()->GetRaptureHotbarModule();
        fpSetHotbarSlot(raptureHotbarModule->HotBar[hotbar]->Slot[slot], raptureHotbarModule->UiModule, type, id);
    }

    public delegate Bool UseActionDelegate(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget);
    [HypostasisClientStructsInjection(typeof(FFXIVClientStructs.FFXIV.Client.Game.ActionManager.MemberFunctionPointers))]
    public static Hook<UseActionDelegate> UseActionHook;
    private static Bool UseActionDetour(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget) =>
        ActionStackManager.OnUseAction(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);

    public static (string Name, uint DataID) FocusTargetInfo { get; private set; } = (null, 0);
    public delegate void SetFocusTargetByObjectIDDelegate(TargetSystem* targetSystem, long objectID);
    [HypostasisSignatureInjection("E8 ?? ?? ?? ?? BA 0C 00 00 00 48 8D 0D")]
    public static Hook<SetFocusTargetByObjectIDDelegate> SetFocusTargetByObjectIDHook;
    private static void SetFocusTargetByObjectIDDetour(TargetSystem* targetSystem, long objectID)
    {
        if (ReAction.Config.AutoFocusTargetID == 0 || DalamudApi.TargetManager.FocusTarget == DalamudApi.ObjectTable.FirstOrDefault(o => o.DataId == FocusTargetInfo.DataID && o.Name.ToString() == FocusTargetInfo.Name))
            SetFocusTargetByObjectIDHook.Original(targetSystem, objectID);
        FocusTargetInfo = DalamudApi.TargetManager.FocusTarget is { } o ? (o.Name.ToString(), o.DataId) : (null, 0);
    }

    public static void RefocusTarget()
    {
        if (FocusTargetInfo.Name == null) return;
        DalamudApi.TargetManager.FocusTarget = DalamudApi.ObjectTable.FirstOrDefault(o => o.DataId == FocusTargetInfo.DataID && o.Name.ToString() == FocusTargetInfo.Name);
    }

    private delegate GameObject* ResolvePlaceholderDelegate(PronounModule* pronounModule, string text, Bool defaultToTarget, Bool allowPlayerNames);
    [HypostasisSignatureInjection("E8 ?? ?? ?? ?? 48 8B 5C 24 30 EB 0C")]
    private static Hook<ResolvePlaceholderDelegate> ResolvePlaceholderHook;
    private static GameObject* ResolvePlaceholderDetour(PronounModule* pronounModule, string text, Bool defaultToTarget, Bool allowPlayerNames) =>
        ResolvePlaceholderHook.Original(pronounModule, text, defaultToTarget, allowPlayerNames || ReAction.Config.EnablePlayerNamesInCommands);

    private static GameObject* GetGameObjectFromPronounIDDetour(PronounModule* pronounModule, PronounID pronounID)
    {
        var ret = Common.getGameObjectFromPronounID.Original(pronounModule, pronounID);
        return (ret != null || !PronounManager.CustomPronouns.TryGetValue((uint)pronounID, out var pronoun)) ? ret : pronoun.GetGameObject();
    }

    private delegate uint GetTextCommandParamIDDelegate(PronounModule* pronounModule, nint* text, int len); // Probably not an issue, but this function doesn't get called if the length is > 31
    [HypostasisSignatureInjection("48 89 5C 24 10 48 89 6C 24 18 56 48 83 EC 20 48 83 79 18 00")] // E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? E8 ?? ?? ?? ?? CC CC (lol)
    private static Hook<GetTextCommandParamIDDelegate> GetTextCommandParamIDHook;
    private static uint GetTextCommandParamIDDetour(PronounModule* pronounModule, nint* bytePtrPtr, int len)
    {
        var ret = GetTextCommandParamIDHook.Original(pronounModule, bytePtrPtr, len);
        return (ret != 0 || !PronounManager.CustomPlaceholders.TryGetValue((*bytePtrPtr).ReadCString(len), out var pronoun)) ? ret : pronoun.ID;
    }

    private delegate void ExecuteMacroDelegate(RaptureShellModule* raptureShellModule, RaptureMacroModule.Macro* macro);
    [HypostasisClientStructsInjection(typeof(RaptureShellModule.MemberFunctionPointers))]
    private static Hook<ExecuteMacroDelegate> ExecuteMacroHook;
    private static void ExecuteMacroDetour(RaptureShellModule* raptureShellModule, RaptureMacroModule.Macro* macro)
    {
        if (ReAction.Config.EnableMacroQueue)
            queueACCommandPatch.Enable();
        else
            queueACCommandPatch.Disable();
        ExecuteMacroHook.Original(raptureShellModule, macro);
    }

    public static void Initialize()
    {
        if (Common.ActionManager == null)
            throw new ApplicationException("ActionManager is not initialized!");
        Common.getGameObjectFromPronounID.CreateHook(GetGameObjectFromPronounIDDetour);
    }

    public static void Dispose() { }
}