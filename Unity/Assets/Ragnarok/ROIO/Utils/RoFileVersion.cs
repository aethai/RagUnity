namespace ROIO.Utils {
    public struct RoFileVersion {
        public int Major { get; }
        public int Minor { get; }
        public string Text {
            get { return Major + "." + Minor; }
        }

        public RoFileVersion(int major, int minor) {
            Major = major;
            Minor = minor;
        }

        public bool IsAtLeast(int major, int minor) {
            return Major > major || (Major == major && Minor >= minor);
        }

        public bool IsGreaterThan(int major, int minor) {
            return Major > major || (Major == major && Minor > minor);
        }

        public bool IsLessThan(int major, int minor) {
            return Major < major || (Major == major && Minor < minor);
        }

        public bool IsLessThanOrEqual(int major, int minor) {
            return Major < major || (Major == major && Minor <= minor);
        }

        public override string ToString() {
            return Text;
        }

        public static RoFileVersion ReadMajorMinor(MemoryStreamReader data) {
            return new RoFileVersion(data.ReadByte(), data.ReadByte());
        }

        public static RoFileVersion ReadMinorMajor(MemoryStreamReader data) {
            int minor = data.ReadByte();
            int major = data.ReadByte();
            return new RoFileVersion(major, minor);
        }
    }
}
