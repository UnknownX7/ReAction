using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace ReAction;

public interface IGamePronoun
{
    public string Name { get; }
    public string Placeholder { get; }
    public uint ID { get; }
    public unsafe GameObject* GetGameObject();
}

public class HardTargetPronoun : IGamePronoun
{
    public string Name => "Target";
    public string Placeholder => "<hard>";
    public uint ID => 10_000;
    public unsafe GameObject* GetGameObject() => (GameObject*)DalamudApi.TargetManager.Target?.Address;
}

public class SoftTargetPronoun : IGamePronoun
{
    public string Name => "Soft Target";
    public string Placeholder => "<soft>";
    public uint ID => 10_001;
    public unsafe GameObject* GetGameObject() => (GameObject*)DalamudApi.TargetManager.SoftTarget?.Address;
}

public class UITargetPronoun : IGamePronoun
{
    public string Name => "UI Target";
    public string Placeholder => "<ui>";
    public uint ID => 10_002;
    public unsafe GameObject* GetGameObject() => Common.UITarget;
}

public class FieldTargetPronoun : IGamePronoun
{
    public string Name => "Field Target";
    public string Placeholder => "<field>";
    public uint ID => 10_003;
    public unsafe GameObject* GetGameObject() => (GameObject*)DalamudApi.TargetManager.MouseOverTarget?.Address;
}

public class LowestHPPronoun : IGamePronoun
{
    public string Name => "Lowest HP Party Member";
    public string Placeholder => "<lowhp>";
    public uint ID => 10_010;
    public unsafe GameObject* GetGameObject()
    {
        static float GetHPPercent(nint address) => (float)((Character*)address)->Health / ((Character*)address)->MaxHealth;
        static uint GetHP(nint address) => ((Character*)address)->Health;
        var members = Common.GetPartyMembers().Where(address => GetHPPercent(address) is > 0 and < 1);
        return members.Any() ? (GameObject*)members.MinBy(GetHP) : null;
    }
}

public class LowestHPPPronoun : IGamePronoun
{
    public string Name => "Lowest HPP Party Member";
    public string Placeholder => "<lowhpp>";
    public uint ID => 10_011;
    public unsafe GameObject* GetGameObject()
    {
        static float GetHPPercent(nint address) => (float)((Character*)address)->Health / ((Character*)address)->MaxHealth;
        var members = Common.GetPartyMembers().Where(address => GetHPPercent(address) is > 0 and < 1);
        return members.Any() ? (GameObject*)members.MinBy(GetHPPercent) : null;
    }
}

public class KardionPronoun : IGamePronoun
{
    public string Name => "Kardion Target";
    public string Placeholder => "<kt>";
    public uint ID => 10_100;
    public unsafe GameObject* GetGameObject() => (GameObject*)Common.GetPartyMembers().FirstOrDefault(address => ((Character*)address)->GetStatusManager()->HasStatus(2605, DalamudApi.ClientState.LocalPlayer!.ObjectId));
}

public static class PronounManager
{
    public static Dictionary<uint, IGamePronoun> CustomPronouns { get; set; } = new();
    public static Dictionary<string, IGamePronoun> CustomPlaceholders { get; set; } = new();
    public static List<uint> OrderedIDs { get; set; } = new()
    {
        10_000, // Target
        10_001, // SoftTarget
        (uint)PronounID.FocusTarget,
        10_002, // UITarget
        10_003, // FieldTarget
        (uint)PronounID.TargetsTarget,
        (uint)PronounID.LastTarget,
        (uint)PronounID.LastEnemy,
        (uint)PronounID.LastAttacker,
        (uint)PronounID.Me,
        (uint)PronounID.P2,
        (uint)PronounID.P3,
        (uint)PronounID.P4,
        (uint)PronounID.P5,
        (uint)PronounID.P6,
        (uint)PronounID.P7,
        (uint)PronounID.P8,
        (uint)PronounID.Companion,
        (uint)PronounID.Pet
    };

    private static readonly Dictionary<PronounID, string> formalPronounIDName = new()
    {
        [PronounID.FocusTarget] = "Focus Target",
        [PronounID.TargetsTarget] = "Target's Target",
        [PronounID.LastTarget] = "Last Target",
        [PronounID.LastEnemy] = "Last Enemy",
        [PronounID.LastAttacker] = "Last Attacker",
        [PronounID.Me] = "Self"
    };

    public static void Initialize()
    {
        foreach (var t in Util.Assembly.GetTypes<IGamePronoun>())
        {
            var pronoun = (IGamePronoun)Activator.CreateInstance(t);
            if (pronoun == null) continue;

            if (pronoun.ID < 10_000)
                throw new ApplicationException("Custom pronoun IDs must be above 10000");

            CustomPronouns.Add(pronoun.ID, pronoun);
            CustomPlaceholders.Add(pronoun.Placeholder, pronoun);
            if (!OrderedIDs.Contains(pronoun.ID))
                OrderedIDs.Add(pronoun.ID);
        }
    }

    public static string GetPronounName(uint id) => id >= 10_000 && CustomPronouns.TryGetValue(id, out var pronoun)
        ? pronoun.Name
        : formalPronounIDName.TryGetValue((PronounID)id, out var name) ? name : ((PronounID)id).ToString();

    public static unsafe GameObject* GetGameObjectFromID(uint id) => id >= 10_000 && CustomPronouns.TryGetValue(id, out var pronoun) ? pronoun.GetGameObject() : Common.GetGameObjectFromPronounID((PronounID)id);
}