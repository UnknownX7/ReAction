using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Dalamud.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ReAction
{
    public class Configuration : IPluginConfiguration
    {
        public class Action
        {
            public uint ID = 0;
            public bool UseAdjustedID = false;
        }

        public class ActionStackItem
        {
            public uint ID = 0;
            public ActionStackManager.TargetType Target = ActionStackManager.TargetType.Target;
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

        public int Version { get; set; }

        public List<ActionStack> ActionStacks = new();
        public bool EnableEnhancedAutoFaceTarget = false;
        public bool EnableAutoDismount = false;
        public bool EnableGroundTargetQueuing = false;
        public bool EnableInstantGroundTarget = false;
        public bool EnableAutoCastCancel = false;
        public bool EnableAutoTarget = false;
        public bool EnableAutoChangeTarget = false;
        public bool EnableSpellAutoAttacks = false;
        public bool EnableCameraRelativeDashes = false;
        public bool EnableNormalBackwardDashes = false;
        public bool EnableQueuingMore = false;
        public bool EnableFPSAlignment = false;
        public bool EnableDecomboMeditation = false;
        public bool EnableAutoRefocusTarget = false;

        public void Initialize() { }

        public void Save() => DalamudApi.PluginInterface.SavePluginConfig(this);

        private static readonly StackSerializer serializer = new ();

        public static string ExportActionStack(ActionStack stack)
            => CompressString(JsonConvert.SerializeObject(stack, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                SerializationBinder = serializer
            }));

        public static ActionStack ImportActionStack(string import)
            => JsonConvert.DeserializeObject<ActionStack>(DecompressString(import), new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                SerializationBinder = serializer
            });

        private const string exportPrefix = "RE_";

        public static string CompressString(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            using var mso = new MemoryStream();
            using (var gs = new GZipStream(mso, CompressionMode.Compress))
            {
                gs.Write(bytes, 0, bytes.Length);
            }
            return exportPrefix + Convert.ToBase64String(mso.ToArray());
        }

        public static string DecompressString(string s)
        {
            if (!s.StartsWith(exportPrefix))
                throw new ApplicationException("This is not a ReAction export.");
            var data = Convert.FromBase64String(s[exportPrefix.Length..]);
            var lengthBuffer = new byte[4];
            Array.Copy(data, data.Length - 4, lengthBuffer, 0, 4);
            var uncompressedSize = BitConverter.ToInt32(lengthBuffer, 0);

            var buffer = new byte[uncompressedSize];
            using (var ms = new MemoryStream(data))
            {
                using var gzip = new GZipStream(ms, CompressionMode.Decompress);
                gzip.Read(buffer, 0, uncompressedSize);
            }
            return Encoding.UTF8.GetString(buffer);
        }
    }
}
