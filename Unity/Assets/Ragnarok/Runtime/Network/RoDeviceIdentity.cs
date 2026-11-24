using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Ragnarok.Network {
    /// <summary>
    /// The device identifier Custom's game.exe sends as CZ_HWID 0x0be3 (code in its .xdiff section, 0x233bfb0): a
    /// connect() hook starts a thread for the socket to port 5121 that sleeps 5 s, reads HKLM\SOFTWARE\Microsoft\Cryptography
    /// MachineGuid (KEY_QUERY_VALUE | KEY_WOW64_64KEY into a 40-byte buffer; when that fails, the value is not REG_SZ or it is
    /// empty, GetComputerNameA into the same buffer) and the volume serial of the Windows directory's drive
    /// (GetVolumeInformationA on "C:\", 0 when unavailable), then sends {0x0be3, 48, serial.L, guid[40]} once per process.
    /// The server stores md5("Custom-HWID-v1" + serial + guid[40]), so every byte, including the zeros after the GUID,
    /// has to match the original client's for both clients to count as the same device.
    /// </summary>
    public static class RoDeviceIdentity {
        public const int IdentifierLength = 40;

        public static bool TryRead(out uint volumeSerial, out byte[] identifier) {
            volumeSerial = 0;
            identifier = null;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try {
                var buffer = new byte[IdentifierLength];
                if (!TryReadMachineGuid(buffer)) {
                    Array.Clear(buffer, 0, buffer.Length);
                    uint size = IdentifierLength;
                    GetComputerNameA(buffer, ref size);
                }

                if (buffer[0] == 0) {
                    return false;
                }

                volumeSerial = ReadWindowsVolumeSerial();
                identifier = buffer;
                return true;
            } catch (Exception) {
                // a missing export or a sandbox without the registry: no identifier, the server then simply has none
                volumeSerial = 0;
                identifier = null;
                return false;
            }
#else
            return false;
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static readonly UIntPtr HkeyLocalMachine = new UIntPtr(0x80000002u);
        private const int KeyQueryValueWow64Key = 0x0101;
        private const int RegSz = 1;

        private static bool TryReadMachineGuid(byte[] buffer) {
            if (RegOpenKeyExA(HkeyLocalMachine, "SOFTWARE\\Microsoft\\Cryptography", 0, KeyQueryValueWow64Key, out IntPtr key) != 0) {
                return false;
            }

            try {
                uint size = IdentifierLength;
                return RegQueryValueExA(key, "MachineGuid", IntPtr.Zero, out int type, buffer, ref size) == 0 &&
                    type == RegSz &&
                    buffer[0] != 0;
            } finally {
                RegCloseKey(key);
            }
        }

        private static uint ReadWindowsVolumeSerial() {
            var directory = new StringBuilder(260);
            uint length = GetWindowsDirectoryA(directory, 260);
            if (length < 3 || directory[1] != ':') {
                return 0;
            }

            string root = directory.ToString(0, 3);
            return GetVolumeInformationA(root, IntPtr.Zero, 0, out uint serial, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0)
                ? serial
                : 0;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Ansi)]
        private static extern int RegOpenKeyExA(UIntPtr key, string subKey, int options, int samDesired, out IntPtr result);

        [DllImport("advapi32.dll", CharSet = CharSet.Ansi)]
        private static extern int RegQueryValueExA(IntPtr key, string valueName, IntPtr reserved, out int type, byte[] data, ref uint size);

        [DllImport("advapi32.dll")]
        private static extern int RegCloseKey(IntPtr key);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern bool GetComputerNameA(byte[] buffer, ref uint size);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern uint GetWindowsDirectoryA(StringBuilder buffer, uint size);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern bool GetVolumeInformationA(
            string rootPathName,
            IntPtr volumeNameBuffer,
            uint volumeNameSize,
            out uint volumeSerialNumber,
            IntPtr maximumComponentLength,
            IntPtr fileSystemFlags,
            IntPtr fileSystemNameBuffer,
            uint fileSystemNameSize);
#endif
    }
}
