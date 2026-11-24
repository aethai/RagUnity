using System;
using System.Text;

namespace Ragnarok.UI {
    internal static class RoShellTextEncoding {
        public static Encoding Resolve(int codePage) {
            try {
                return Encoding.GetEncoding(codePage);
            } catch (ArgumentException) {
                return Encoding.ASCII;
            } catch (NotSupportedException) {
                return Encoding.ASCII;
            }
        }
    }
}
