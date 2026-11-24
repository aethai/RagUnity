namespace Ragnarok.Assets {
    /// <summary>
    /// A sprite layer's base path. Usually one base for both files (<c>base.spr</c> + <c>base.act</c>); a garment whose
    /// .spr is shared by every job (<c>sprite/로브/{name}/{name}.spr</c>) while its .act is per job carries both bases:
    /// <c>{act base}|spr={spr base}</c>. '|' cannot appear in an archive path, so a plain base path is never split.
    /// </summary>
    public static class RoSpritePairPath {
        private const string SpriteSeparator = "|spr=";

        /// <summary>One base path for an .act and an .spr that live under different names.</summary>
        public static string Compose(string actionBasePath, string spriteBasePath) {
            if (string.IsNullOrEmpty(spriteBasePath) ||
                string.Equals(actionBasePath, spriteBasePath, System.StringComparison.OrdinalIgnoreCase)) {
                return actionBasePath;
            }

            return actionBasePath + SpriteSeparator + spriteBasePath;
        }

        public static string SpriteBasePath(string basePath) {
            int split = IndexOfSeparator(basePath);
            return split < 0 ? basePath : basePath.Substring(split + SpriteSeparator.Length);
        }

        public static string ActionBasePath(string basePath) {
            int split = IndexOfSeparator(basePath);
            return split < 0 ? basePath : basePath.Substring(0, split);
        }

        public static string SpritePath(string basePath) {
            return SpriteBasePath(basePath) + ".spr";
        }

        public static string ActionPath(string basePath) {
            return ActionBasePath(basePath) + ".act";
        }

        private static int IndexOfSeparator(string basePath) {
            return string.IsNullOrEmpty(basePath)
                ? -1
                : basePath.IndexOf(SpriteSeparator, System.StringComparison.Ordinal);
        }
    }
}
