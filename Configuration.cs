using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ReAction;

public class Configuration : PluginConfiguration<Configuration>, IPluginConfiguration
{
    public class Action
    {
        public uint ID = 0;
        public bool UseAdjustedID = false;
    }

    public class ActionStackItem
    {
        public uint ID = 0;

        [Obsolete]
        public uint Target
        {
            set
            {
                TargetID = value switch
                {
                    0 => 10_000,
                    1 => 10_001,
                    2 => (uint)PronounID.FocusTarget,
                    3 => 10_002,
                    4 => 10_003,
                    5 => (uint)PronounID.TargetsTarget,
                    6 => (uint)PronounID.Me,
                    7 => (uint)PronounID.LastTarget,
                    8 => (uint)PronounID.LastEnemy,
                    9 => (uint)PronounID.LastAttacker,
                    10 => (uint)PronounID.P2,
                    11 => (uint)PronounID.P3,
                    12 => (uint)PronounID.P4,
                    13 => (uint)PronounID.P5,
                    14 => (uint)PronounID.P6,
                    15 => (uint)PronounID.P7,
                    16 => (uint)PronounID.P8,
                    17 => 10_010,
                    _ => 10_000
                };
            }
        }

        public uint TargetID = 10_000;
    }

    public class ActionStack
    {
        public string Name = string.Empty;
        public List<Action> Actions = new();
        public List<ActionStackItem> Items = new();
        public uint ModifierKeys = 0u;
        public bool BlockOriginal = false;
        public bool CheckRange = false;
        public bool CheckCooldown = false;
    }

    public class StackSerializer : DefaultSerializationBinder
    {
        private static readonly Type actionStackType = typeof(ActionStack);
        private static readonly Type actionStackItemType = typeof(ActionStackItem);
        private static readonly Type actionType = typeof(Action);
        private const string actionStackShortName = "s";
        private const string actionStackItemShortName = "i";
        private const string actionShortName = "a";
        private static readonly Dictionary<string, Type> types = new()
        {
            [actionStackType.FullName!] = actionStackType,
            [actionStackShortName] = actionStackType,
            [actionStackItemType.FullName!] = actionStackItemType,
            [actionStackItemShortName] = actionStackItemType,
            [actionType.FullName!] = actionType,
            [actionShortName] = actionType
        };
        private static readonly Dictionary<Type, string> typeNames = new()
        {
            [actionStackType] = actionStackShortName,
            [actionStackItemType] = actionStackItemShortName,
            [actionType] = actionShortName
        };

        public override Type BindToType(string assemblyName, string typeName)
            => types.TryGetValue(typeName, out var t) ? t : base.BindToType(assemblyName, typeName);

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = null;
            if (typeNames.TryGetValue(serializedType, out var name))
                typeName = name;
            else
                base.BindToName(serializedType, out assemblyName, out typeName);
        }
    }

    public override int Version { get; set; }

    public List<ActionStack> ActionStacks = new();
    public bool EnableEnhancedAutoFaceTarget = false;
    public bool EnableAutoDismount = false;
    public bool EnableGroundTargetQueuing = false;
    public bool EnableInstantGroundTarget = false;
    public bool EnableBlockMiscInstantGroundTargets = false;
    public bool EnableAutoCastCancel = false;
    public bool EnableAutoTarget = false;
    public bool EnableAutoChangeTarget = false;
    public bool EnableSpellAutoAttacks = false;
    public bool EnableSpellAutoAttacksOutOfCombat = false;
    public bool EnableCameraRelativeDashes = false;
    public bool EnableNormalBackwardDashes = false;
    public bool EnableQueuingMore = false;
    [Obsolete] public bool EnableFPSAlignment { internal get; set; } // TODO: Remove in 6.4
    public bool EnableFrameAlignment = false;
    public bool EnableAutoRefocusTarget = false;
    public bool EnableMacroQueue = false;
    public bool EnableFractionality = false;
    public bool EnablePlayerNamesInCommands = false;
    public bool EnableQueueAdjustments = false;
    public bool EnableRequeuing = false;
    public bool EnableSlidecastQueuing = false;
    public bool EnableGCDAdjustedQueueThreshold = false;
    public float QueueThreshold = 0.5f;
    public float QueueLockThreshold = 0.5f;
    public float QueueActionLockout = 0f;
    public bool EnableTurboHotbars = false;
    public int TurboHotbarInterval = 400;
    public int InitialTurboHotbarInterval = 0;
    public bool EnableTurboHotbarsOutOfCombat = false;
    public bool EnableCameraRelativeDirectionals = false;
    public bool EnableUnassignableActions = false;
    public uint AutoFocusTargetID = 0;

    public bool EnableDecomboMeditation = false;
    public bool EnableDecomboBunshin = false;
    public bool EnableDecomboWanderersMinuet = false;
    public bool EnableDecomboLiturgy = false;
    public bool EnableDecomboEarthlyStar = false;
    public bool EnableDecomboMinorArcana = false;
    public bool EnableDecomboGeirskogul = false;

    public override void Initialize()
    {
        if (EnableFPSAlignment)
            EnableFrameAlignment = true;
    }

    private static readonly StackSerializer serializer = new ();

    private const string exportPrefix = "RE_";

    public static string ExportActionStack(ActionStack stack)
        => Util.CompressString(JsonConvert.SerializeObject(stack, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            SerializationBinder = serializer
        }), exportPrefix);

    public static ActionStack ImportActionStack(string import)
        => JsonConvert.DeserializeObject<ActionStack>(Util.DecompressString(import, exportPrefix), new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects,
            SerializationBinder = serializer
        });
}