using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Logging;

namespace ReAction
{
    public static class Memory
    {
        public class Replacer : IDisposable
        {
            public IntPtr Address { get; private set; } = IntPtr.Zero;
            private readonly byte[] newBytes;
            private readonly byte[] oldBytes;
            public bool IsEnabled { get; private set; } = false;
            public bool IsValid => Address != IntPtr.Zero;
            public string ReadBytes => !IsValid ? string.Empty : oldBytes.Aggregate(string.Empty, (current, b) => current + (b.ToString("X2") + " "));

            public Replacer(IntPtr addr, byte[] bytes, bool startEnabled = false)
            {
                if (addr == IntPtr.Zero) return;

                Address = addr;
                newBytes = bytes;
                SafeMemory.ReadBytes(addr, bytes.Length, out oldBytes);
                createdReplacers.Add(this);

                if (startEnabled)
                    Enable();
            }

            public Replacer(string sig, byte[] bytes, bool startEnabled = false)
            {
                var addr = IntPtr.Zero;
                try { addr = DalamudApi.SigScanner.ScanModule(sig); }
                catch { PluginLog.LogError($"Failed to find signature {sig}"); }
                if (addr == IntPtr.Zero) return;

                Address = addr;
                newBytes = bytes;
                SafeMemory.ReadBytes(addr, bytes.Length, out oldBytes);
                createdReplacers.Add(this);

                if (startEnabled)
                    Enable();
            }

            public void Enable()
            {
                if (!IsValid) return;
                SafeMemory.WriteBytes(Address, newBytes);
                IsEnabled = true;
            }

            public void Disable()
            {
                if (!IsValid) return;
                SafeMemory.WriteBytes(Address, oldBytes);
                IsEnabled = false;
            }

            public void Toggle()
            {
                if (!IsEnabled)
                    Enable();
                else
                    Disable();
            }

            public void Dispose()
            {
                if (IsEnabled)
                    Disable();
            }
        }

        private static readonly List<Replacer> createdReplacers = new();

        public static void Dispose()
        {
            foreach (var rep in createdReplacers)
                rep.Dispose();
        }
    }
}
