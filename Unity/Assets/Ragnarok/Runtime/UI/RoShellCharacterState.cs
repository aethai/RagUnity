using System;
using Ragnarok.Data;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal sealed class RoShellCharacterState {
        private string selectedCharacterName = string.Empty;
        private RoLoginShellCharacter[] characters = Array.Empty<RoLoginShellCharacter>();
        private RoHcAcceptEnter2 slotLimits;
        private RoHcNotifyCharList pageInfo;
        private int characterListTotalSlots;

        public string SelectedCharacterName => selectedCharacterName;
        public RoHcAcceptEnter2 SlotLimits => slotLimits;
        public RoHcNotifyCharList PageInfo => pageInfo;
        public int CharacterListTotalSlots => characterListTotalSlots;

        public RoLoginShellCharacter[] SnapshotCharacters() {
            return (RoLoginShellCharacter[]) characters.Clone();
        }

        public bool TryResolveSelectedCharacter(out RoLoginShellCharacter character) {
            for (int i = 0; i < characters.Length; i++) {
                RoLoginShellCharacter candidate = characters[i];
                if (string.Equals(candidate.Name, selectedCharacterName, StringComparison.OrdinalIgnoreCase)) {
                    character = candidate;
                    return true;
                }
            }

            if (characters.Length > 0) {
                character = characters[0];
                return true;
            }

            character = default;
            return false;
        }

        public void Clear() {
            selectedCharacterName = string.Empty;
            characters = Array.Empty<RoLoginShellCharacter>();
            ClearSlotMetadata();
        }

        public void SetSelectedCharacterName(string characterName) {
            selectedCharacterName = characterName ?? string.Empty;
        }

        public void SetPendingCharacterName(string characterName) {
            selectedCharacterName = (characterName ?? string.Empty).Trim();
        }

        public void SelectFirstAvailable() {
            selectedCharacterName = characters.Length > 0 ? characters[0].Name : string.Empty;
        }

        public void ApplyLoadedCharacters(RoCharacterInfo[] source) {
            ClearSlotMetadata();
            characters = ConvertCharacters(source);
            selectedCharacterName = characters.Length > 0 ? characters[0].Name : string.Empty;
        }

        public void ApplyLoadedCharacters(RoInitialCharacterSnapshot snapshot) {
            if (snapshot == null) {
                ApplyLoadedCharacters((RoCharacterInfo[]) null);
                return;
            }

            ApplySlotMetadata(snapshot);
            characters = ConvertCharacters(snapshot.Characters);
            selectedCharacterName = characters.Length > 0 ? characters[0].Name : string.Empty;
        }

        public void ApplyCreatedCharacterResult(RoLoginCreateCharacterResult result, RoShellCharacterRequest request) {
            if (result == null) {
                Clear();
                return;
            }

            ApplySlotMetadata(result.InitialCharacters);
            RoLoginShellCharacter selectedCharacter = result.CharacterWasCreated
                ? CreateCreatedCharacter(result.SelectedCharacter, request)
                : new RoLoginShellCharacter(result.SelectedCharacter);
            characters = MergeSelectedCharacter(
                ConvertCharacters(result.InitialCharacters != null ? result.InitialCharacters.Characters : null),
                selectedCharacter);
            selectedCharacterName = string.IsNullOrWhiteSpace(selectedCharacter.Name)
                ? request.CharacterName
                : selectedCharacter.Name;
        }

        private void ApplySlotMetadata(RoInitialCharacterSnapshot snapshot) {
            if (snapshot == null) {
                ClearSlotMetadata();
                return;
            }

            slotLimits = snapshot.SlotLimits;
            pageInfo = snapshot.PageInfo;
            characterListTotalSlots = snapshot.CharacterList.TotalSlots;
        }

        private void ClearSlotMetadata() {
            slotLimits = default;
            pageInfo = default;
            characterListTotalSlots = 0;
        }

        private static RoLoginShellCharacter[] ConvertCharacters(RoCharacterInfo[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoLoginShellCharacter>();
            }

            var output = new RoLoginShellCharacter[source.Length];
            for (int i = 0; i < source.Length; i++) {
                output[i] = new RoLoginShellCharacter(source[i]);
            }

            return output;
        }

        private static RoLoginShellCharacter[] MergeSelectedCharacter(RoLoginShellCharacter[] existing, RoLoginShellCharacter selected) {
            if (existing == null || existing.Length == 0) {
                return string.IsNullOrWhiteSpace(selected.Name)
                    ? Array.Empty<RoLoginShellCharacter>()
                    : new[] { selected };
            }

            for (int i = 0; i < existing.Length; i++) {
                if (existing[i].CharacterId == selected.CharacterId ||
                    string.Equals(existing[i].Name, selected.Name, StringComparison.OrdinalIgnoreCase)) {
                    existing[i] = selected;
                    return existing;
                }
            }

            var output = new RoLoginShellCharacter[existing.Length + 1];
            Array.Copy(existing, output, existing.Length);
            output[output.Length - 1] = selected;
            return output;
        }

        private static RoLoginShellCharacter CreateCreatedCharacter(RoCharacterInfo source, RoShellCharacterRequest request) {
            string requestedName = request.CharacterName ?? string.Empty;
            return new RoLoginShellCharacter(
                source != null && source.CharacterId != 0
                    ? source.CharacterId
                    : GenerateTemporaryCharacterId(requestedName, request.CharacterSlot),
                string.IsNullOrWhiteSpace(requestedName)
                    ? (source != null ? source.Name : string.Empty)
                    : requestedName,
                ClampSlot(request.CharacterSlot),
                source != null && source.BaseLevel > 0 ? source.BaseLevel : (short) 1,
                source != null && source.JobLevel > 0 ? source.JobLevel : 1,
                source != null ? source.JobId : (short) request.StartJob,
                string.IsNullOrWhiteSpace(source != null ? source.MapName : null)
                    ? "prontera.gat"
                    : source.MapName,
                source != null ? source.Experience : 0,
                source != null ? source.JobExperience : 0,
                source != null && source.Hp > 0 ? source.Hp : 40,
                source != null && source.MaxHp > 0 ? source.MaxHp : 40,
                source != null && source.Sp > 0 ? source.Sp : 11,
                source != null && source.MaxSp > 0 ? source.MaxSp : 11,
                request.HairStyle > 0 ? (short) request.HairStyle : source != null ? source.Head : (short) 1,
                source != null ? source.Body : (short) 0,
                source != null ? source.Weapon : (short) 0,
                source != null ? source.Accessory : (short) 0,
                source != null ? source.Shield : (short) 0,
                source != null ? source.Accessory2 : (short) 0,
                source != null ? source.Accessory3 : (short) 0,
                source != null ? source.HeadPalette : (short) 0,
                source != null ? source.BodyPalette : (short) 0,
                request.HairColor <= byte.MaxValue
                    ? (byte) request.HairColor
                    : source != null ? source.HairColor : (byte) 0,
                source != null && source.Strength > 0 ? source.Strength : (byte) 1,
                source != null && source.Agility > 0 ? source.Agility : (byte) 1,
                source != null && source.Vitality > 0 ? source.Vitality : (byte) 1,
                source != null && source.Intelligence > 0 ? source.Intelligence : (byte) 1,
                source != null && source.Dexterity > 0 ? source.Dexterity : (byte) 1,
                source != null && source.Luck > 0 ? source.Luck : (byte) 1,
                request.Sex ?? (source != null ? source.Sex : (byte) 1));
        }

        private static byte ClampSlot(int slot) {
            if (slot <= 0) {
                return 0;
            }

            return slot >= byte.MaxValue ? byte.MaxValue : (byte) slot;
        }

        private static int GenerateTemporaryCharacterId(string name, int slot) {
            unchecked {
                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(name ?? string.Empty);
                return 0x40000000 | ((hash ^ slot) & 0x3fffffff);
            }
        }
    }
}
