using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace ROIO.Loaders
{
    /// <summary>
    /// BMP / TGA bytes → <see cref="Texture2D"/> through <see cref="RoFastImageDecoder"/>: pixels are decoded straight
    /// into the texture's memory (no per-byte stream reads, no Color32[] copy, no SetPixels32). The texture matches
    /// what <c>BMPImage.ToTexture2D</c> / <c>TGAImage.ToTexture2D</c> build (RGBA32, sRGB, no mips, readable).
    /// </summary>
    public static unsafe class RoFastImageTexture
    {
        /// <summary>
        /// The image's pixels in the texture's layout (RGBA32, bottom row first: <see cref="Texture2D.SetPixels32(Color32[])"/>
        /// gives the texture <see cref="TryCreate"/> makes), with no Unity object: callable from worker threads.
        /// False when the image needs the old loader (format outside the fast set).
        /// </summary>
        public static bool TryDecodePixels(byte[] data, bool tga, out int width, out int height, out Color32[] pixels)
        {
            width = height = 0;
            pixels = null;
            RoFastImageDecoder.ImageInfo info;
            bool parsed = tga ? RoFastImageDecoder.TryParseTga(data, out info) : RoFastImageDecoder.TryParseBmp(data, out info);
            if (!parsed)
            {
                return false;
            }

            var decoded = new Color32[info.Width * info.Height];
            fixed (Color32* rgba = decoded)
            {
                if (!RoFastImageDecoder.Decode(data, info, (byte*)rgba))
                {
                    return false;   // the main-thread loader logs the bad palette index
                }
            }

            width = info.Width;
            height = info.Height;
            pixels = decoded;
            return true;
        }

        /// <summary>The texture, or null when the image needs the old loader (format outside the fast set).</summary>
        public static Texture2D TryCreate(byte[] data, bool tga)
        {
            RoFastImageDecoder.ImageInfo info;
            bool parsed = tga ? RoFastImageDecoder.TryParseTga(data, out info) : RoFastImageDecoder.TryParseBmp(data, out info);
            if (!parsed)
            {
                return null;
            }

            var texture = new Texture2D(info.Width, info.Height, TextureFormat.RGBA32, false, linear: false);
            NativeArray<byte> pixels = texture.GetRawTextureData<byte>();
            if (pixels.Length < info.Width * info.Height * 4)
            {
                Object.Destroy(texture);
                return null;
            }

            if (!RoFastImageDecoder.Decode(data, info, (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(pixels)))
            {
                Debug.LogWarning("Indexed bitmap has indices greater than it's color palette");
            }

            texture.Apply();
            return texture;
        }
    }
}
