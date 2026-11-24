
using System;
using System.Text;

namespace ROIO.GRF
{
    public class GrfSupport
    {
        public const int KoreanPathCodePage = 949;

        public static uint getCStringLength(byte[] buffer, uint offset)
        {
            for (uint i = offset; i < buffer.Length; i++)
            {
                if (buffer[i] == 0)
                {
                    return i - offset;
                }
            }

            return 0;
        }

        /** Grabs a C string (null terminated) from a buffer
         *  
         *  @return string
         */
        public static string getCString(byte[] buffer, uint offset)
        {
            uint length = getCStringLength(buffer, offset);
            if (length == 0)
            {
                return string.Empty;
            }

            return GetKoreanPathEncoding().GetString(buffer, checked((int)offset), checked((int)length));
        }

        /** Endian support function.
         *
         * Grabs a uint32_t from a 4byte (or more) character array.
         *
         * @warning If the character array is less than 4 bytes long, this function
         *		will access memory outside of the array
         *
         * @param p A uint8_t (char) array holding the bytes
         * @return A uint32_t in Little Endian order
         */
        public static uint LittleEndian32(byte[] p, int offset)
        {
            if (offset + 4 > p.Length)
            {
                throw new Exception("OUT_OF_BOUNDS");
            }

            return (uint)((p[3 + offset] * 256 + p[2 + offset]) * 256 + p[1 + offset]) * 256 + p[offset];
        }

        /** Endian support function.
         *
         * Transforms a host uint32_t into a little-endian uint32_t
         *
         * @param hi A host uint32_t value
         * @return A uint32_t in Little Endian order
         */
        unsafe public static uint ToLittleEndian32(uint hi)
        {
            uint lei;
            byte* p = (byte*)&lei;
            p[0] = (byte)(hi & 0xFF);
            p[1] = (byte)((hi & 0xFF00) >> 8);
            p[2] = (byte)((hi & 0xFF0000) >> 16);
            p[3] = (byte)((hi & 0xFF000000) >> 24);

            return lei;
        }

        /** Function to hash a filename.
         *
         * @note This function hashes the exact same way that GRAVITY's GRF openers
         *		do. Enjoy ;-)
         *
         * @param name Filename to be hashed
         * @return The value of the hashed filename
         */
        public static uint GRF_NameHash(string name)
        { //IT HAS COLLISIONS
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            uint tmp;

            tmp = 0x1505;
            for (int i = 0; i < name.Length; i++)
            {
                tmp = tmp * 0x21 + name[i];
            }

            return tmp;
        }

        private static Encoding GetKoreanPathEncoding()
        {
            return Encoding.GetEncoding(KoreanPathCodePage);
        }
    }
}
