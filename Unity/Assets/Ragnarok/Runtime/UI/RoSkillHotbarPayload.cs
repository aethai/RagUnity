namespace Ragnarok.UI {
    public readonly struct RoSkillHotbarPayload {
        public RoSkillHotbarPayload(string type, string from, int skillId, int level, int selectedLevel, string displayName) {
            Type = type ?? string.Empty;
            From = from ?? string.Empty;
            SkillId = skillId;
            Level = level;
            SelectedLevel = selectedLevel;
            DisplayName = displayName ?? string.Empty;
        }

        public string Type { get; }
        public string From { get; }
        public int SkillId { get; }
        public int Level { get; }
        public int SelectedLevel { get; }
        public string DisplayName { get; }
        public bool IsSkill => Type == "skill" && SkillId > 0;
    }
}
