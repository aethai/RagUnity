using Ragnarok.Network;

namespace Ragnarok.UI {
    internal sealed class RoShellProfileState {
        private RoServerProfile profile;

        public RoShellProfileState(RoServerProfile initialProfile) {
            profile = Normalize(initialProfile);
        }

        public RoServerProfile Profile => profile;

        public bool Matches(RoServerProfile value) {
            return profile.Equals(Normalize(value));
        }

        public void Set(RoServerProfile value) {
            profile = Normalize(value);
        }

        private static RoServerProfile Normalize(RoServerProfile value) {
            return value.PacketVersion == 0
                ? RoServerProfile.LocalRathena20200401()
                : value;
        }
    }
}
