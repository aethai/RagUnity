using System.Collections.Generic;

namespace Ragnarok.UI {
    /// <summary>
    /// Single source of truth for the BasicInfoV4 icon row. Order matches
    /// roBrowserLegacy's <c>BasicInfoV4.html</c> after the <c>onAppend</c>
    /// hide list (battle/replay/tipbox/shortcut/agency removed). Adding a
    /// new gameplay window = create one icon file and add it here.
    /// </summary>
    public static class RoBasicInfoIconRegistry {
        private static readonly IRoBasicInfoIcon[] icons = {
            new RoBasicInfoIcon_Info(),
            new RoBasicInfoIcon_Equip(),
            new RoBasicInfoIcon_Item(),
            new RoBasicInfoIcon_Skill(),
            new RoBasicInfoIcon_Party(),
            new RoBasicInfoIcon_Guild(),
            new RoBasicInfoIcon_Quest(),
            new RoBasicInfoIcon_Map(),
            new RoBasicInfoIcon_Navigation(),
            new RoBasicInfoIcon_Option(),
            new RoBasicInfoIcon_Bank(),
            new RoBasicInfoIcon_Mail(),
            new RoBasicInfoIcon_Achievment(),
            new RoBasicInfoIcon_Attendance()
        };

        /// <summary>Icons in V4 menu order. Used by both runtime layout and parity tests.</summary>
        public static IReadOnlyList<IRoBasicInfoIcon> All => icons;

        /// <summary>Lookup by stable id. Returns null when the id is unknown.</summary>
        public static IRoBasicInfoIcon Find(string id) {
            if (string.IsNullOrEmpty(id)) {
                return null;
            }

            for (int i = 0; i < icons.Length; i++) {
                if (string.Equals(icons[i].Id, id, System.StringComparison.Ordinal)) {
                    return icons[i];
                }
            }

            return null;
        }
    }
}
