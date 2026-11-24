// Synced from reference/port/src/RoClassicFx/Shared/prims-sprites/FxRenderer.Sprites.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// One call of CRenderObject::AddSprite (FUN_00bd7930, thiscall, RET 0x18) — a screen-space sprite
    /// quad. See packages/prims-sprites/NOTES.md ("sprite-draw contract") for how to render it.
    /// </summary>
    public struct FxSpriteDraw
    {
        /// <summary>ECX: the CRenderObject submitting (the prim); AddSprite ORs its +0x124 into the flags.</summary>
        public uint Object;
        /// <summary>Rect (screen pixels, y down): rect+0 top, +4 left, +8 bottom, +0xc right. Left &gt; right = mirrored.</summary>
        public float Top, Left, Bottom, Right;
        /// <summary>rect+0x10: rhw of the bottom edge, rect+0x14: rhw of the top edge.</summary>
        public float RhwBottom, RhwTop;
        /// <summary>rect+0x18: vertex colour 0xAARRGGBB.</summary>
        public uint Argb;
        /// <summary>rect+0x1c: rotation in integer degrees (as float) about the rect centre; 0 = none.</summary>
        public float Angle;
        /// <summary>rect+0x2c: SprImg* (FxMemory address of the image header inside a CSprRes).</summary>
        public uint Image;
        /// <summary>Arg 2: CSprRes +0x110 — the ARGB1555 palette used to build a palette image's texture.</summary>
        public uint Palette;
        /// <summary>Arg 3: render flags (the prim's f_1c4); 5 selects blend preset 2 instead of 6 (see notes).</summary>
        public uint Flags;
        /// <summary>Args 4/5: offsets added to the rotation centre (x, y); only used when Angle != 0.</summary>
        public float OffsetX, OffsetY;
        /// <summary>Arg 6: depth bias (z += bias * 1e-6, rhw += bias * 2e-4).</summary>
        public int DepthBias;

        /// <summary>Serialised form used by the oracle tests (field order as above).</summary>
        public byte[] ToBytes()
        {
            var b = new byte[4 * 15];
            void Put(int i, uint v) => BitConverter.GetBytes(v).CopyTo(b, i * 4);
            void PutF(int i, float v) => Put(i, (uint)BitConverter.SingleToInt32Bits(v));
            Put(0, Object);
            PutF(1, Top); PutF(2, Left); PutF(3, Bottom); PutF(4, Right);
            PutF(5, RhwBottom); PutF(6, RhwTop);
            Put(7, Argb); PutF(8, Angle);
            Put(9, Image); Put(10, Palette); Put(11, Flags);
            PutF(12, OffsetX); PutF(13, OffsetY); Put(14, (uint)DepthBias);
            return b;
        }
    }

    public sealed partial class FxRenderer
    {
        /// <summary>AddSprite (0xbd7930) submissions.</summary>
        public Action<FxSpriteDraw> SpriteSubmitted;

        /// <summary>FUN_00bd7930 — CRenderObject::AddSprite: hands the sprite quad to the host renderer.</summary>
        public void AddSprite(in FxSpriteDraw draw) => SpriteSubmitted?.Invoke(draw);

        /// <summary>Renderer +0x28: viewport width in pixels (sprite horizontal culling).</summary>
        public int ScreenWidth { get => M.I32(Address + 0x28); set => M.I32(Address + 0x28, value); }

        /// <summary>Renderer +0x2c: viewport height in pixels (sprite vertical culling).</summary>
        public int ScreenHeight { get => M.I32(Address + 0x2c); set => M.I32(Address + 0x2c, value); }
    }
}
