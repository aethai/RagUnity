// Synced from reference/port/src/RoClassicFx/FxRuntime.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>Services the effect engine obtains from the rest of the client.</summary>
    public interface IFxHost
    {
        /// <summary>
        /// CTexMgr::GetTexture (0x54f5c0): resolve a texture (path relative to data	exture).
        /// Returns false if missing. Image size is the bitmap size, surface size the power-of-two
        /// D3D surface the client allocates (UVs are scaled by image/surface).
        /// </summary>
        bool TryGetTexture(string name, out uint handle, out int imageWidth, out int imageHeight, out int surfaceWidth, out int surfaceHeight);

        /// <summary>PlaySound3D (0x5e5070): position is relative to the player (x, y, z as passed by the client).</summary>
        void PlaySound3D(string wav, float x, float y, float z, int maxDist, int minDist, float factor, int loop);
    }

    /// <summary>
    /// A host that loads a texture with CTexMgr::GetTexture's second argument set: the STR layer textures (FUN_006c7940
    /// calls FUN_0054f5c0(name, 1, 0, 1, 0); every other caller passes 0). The flag is taken as the black colour key:
    /// pure black texels are transparent (melt.str's shield.bmp has a black background and ONE / ZERO keys, the client
    /// shows no black square; RagnarokRebuild imports STR textures keyed on black). A host without this interface gets
    /// the plain texture.
    /// </summary>
    public interface IFxBlackKeyTextureHost
    {
        bool TryGetTexture(string name, bool blackKey, out uint handle, out int imageWidth, out int imageHeight, out int surfaceWidth, out int surfaceHeight);
    }

    /// <summary>
    /// Global state of the classic effect engine: memory, CRT rand, renderer, host services and
    /// the client globals the engine reads (tick count, /mineffect, object id counter).
    /// </summary>
    public sealed partial class FxRuntime
    {
        public const uint VtCGameObject = 0x00ffdeec;
        public const uint VtCRenderObject = 0x00ffdf04;
        public const uint VtCInstantVar = 0x00ffdee4;
        public const uint VtCEffectPrim = 0x00ff745c;
        public const uint VtCRagEffect = 0x00ff75f8;

        public readonly FxMemory M;
        public readonly MsvcRand Rand;
        public readonly FxRenderer Renderer;
        public IFxHost Host;

        /// <summary>DAT_01555ec4 — logic ticks to run this frame (0 = render-only frame).</summary>
        public int TicksThisFrame = 1;

        /// <summary>
        /// The client runs one process pass on a frame without a whole tick and some effect / prim types (the hit
        /// effects 0x31..0x35, type 0 / 0x10 prims flagged at +0x3e8 …) advance on it: at the client's ~60 rendered
        /// frames a second that is about one step a tick, a host rendering faster plays them faster (a 200 fps host:
        /// a fire hit over in a third of its time). False: a pass without a tick advances nothing.
        /// </summary>
        public bool ProcessOnTicklessFrames = true;

        /// <summary>FUN_00d02630 — DAT_011a60d0 && DAT_011a60d4 == 2 (screen Y flip mode); off in normal play.</summary>
        public bool FlipScreenY;

        /// <summary>CRagEffect::fn_b98ab0 — visibility of an effect (hidden owners, /effect off …). Defaults to visible.</summary>
        public System.Func<RagEffect, bool> IsEffectVisibleImpl = e => true;
        public bool IsEffectVisible(RagEffect e) => IsEffectVisibleImpl(e);

        /// <summary>DAT_01552d7c — /mineffect.</summary>
        public bool MinEffect;

        /// <summary>DAT_01181bc0/bc4 — 64-bit game object id counter.</summary>
        public ulong ObjectIdCounter;

        /// <summary>World shim: mode object whose +0xcc points to a world with a render-object list at +0x8.</summary>
        public readonly uint Mode;
        public readonly uint World;

        public FxRuntime(FxMemory memory, MsvcRand rand = null, FxRenderer renderer = null)
        {
            M = memory;
            Rand = rand ?? new MsvcRand();
            Renderer = renderer ?? new FxRenderer(memory);
            Mode = M.Alloc(0x100);
            World = M.Alloc(0x100);
            Player = M.Alloc(0x100);
            View = M.Alloc(0x100);
            M.U32(Mode + 0xcc, World);
            M.U32(Mode + 0xd0, View);
            FxMatrix.Identity(M, View + 0x98);
            M.U32(World + 0x2c, Player);
            InitList(World + 0x8);
            DefaultEffectAct = AllocDefaultEffectAct();   // effects-10: the default ACT of CRagEffects (+0x108)
        }

        /// <summary>CView (mode +0xd0); the 4x3 view matrix used by effects is at +0x98.</summary>
        public readonly uint View;

        /// <summary>FUN_00b0dc10: TL-vertex init + ProjectVertex of the point at <paramref name="point"/>.</summary>
        public void ProjectTl(uint matrix, uint point, out float sx, out float sy, out float sz)
            => Renderer.ProjectVertex(M, matrix, M.F32(point), M.F32(point + 4), M.F32(point + 8), out sx, out sy, out sz, out _);

        /// <summary>Local player object (world +0x2c); position at +0x10/+0x14/+0x18.</summary>
        public readonly uint Player;

        public void SetPlayerPosition(float x, float y, float z)
        {
            M.F32(Player + 0x10, x); M.F32(Player + 0x14, y); M.F32(Player + 0x18, z);
        }

        /// <summary>The client's idiom: PlaySound3D(wav, x - player.x, y, z - player.z, ...).</summary>
        public void PlaySound3DRelative(string wav, float x, float y, float z, int maxDist, int minDist, float factor, int loop)
        {
            uint player = M.U32(M.U32(Mode + 0xcc) + 0x2c);
            Host?.PlaySound3D(wav, x - M.F32(player + 0x10), y, z - M.F32(player + 0x18), maxDist, minDist, factor, loop);
        }

        readonly System.Collections.Generic.Dictionary<string, uint> textures = new System.Collections.Generic.Dictionary<string, uint>(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>Snapshot of runtime-side caches (texture objects) so a test can rewind them with the heap.</summary>
        public object CaptureCaches() => new System.Collections.Generic.Dictionary<string, uint>(textures, System.StringComparer.OrdinalIgnoreCase);

        public void RestoreCaches(object state)
        {
            textures.Clear();
            foreach (var kv in (System.Collections.Generic.Dictionary<string, uint>)state) textures[kv.Key] = kv.Value;
        }

        /// <summary>Texture object layout: +0 host handle, +0xc/+0x10 surface w/h, +0x14/+0x18 image w/h.</summary>
        public const uint TextureObjectSize = 0x20;

        /// <summary>GetTexture (0x54f5c0) shim: returns a cached CTexture-like object address (0 if missing).</summary>
        public uint GetTexture(string name) => GetTexture(name, false);

        /// <summary>GetTexture (0x54f5c0) with its second argument (<see cref="IFxBlackKeyTextureHost"/>): a keyed texture is a separate object.</summary>
        public uint GetTexture(string name, bool blackKey)
        {
            string key = blackKey ? name + "\u0001" : name;
            if (textures.TryGetValue(key, out uint t)) return t;
            t = 0;
            bool found;
            uint handle;
            int iw, ih, sw, sh;
            if (blackKey && Host is IFxBlackKeyTextureHost keyed)
                found = keyed.TryGetTexture(name, true, out handle, out iw, out ih, out sw, out sh);
            else if (Host != null)
                found = Host.TryGetTexture(name, out handle, out iw, out ih, out sw, out sh);
            else
            {
                found = false;
                handle = 0; iw = ih = sw = sh = 0;
            }
            if (found)
            {
                t = M.Alloc(TextureObjectSize);
                M.U32(t, handle);
                M.I32(t + 0xc, sw); M.I32(t + 0x10, sh);
                M.I32(t + 0x14, iw); M.I32(t + 0x18, ih);
            }
            textures[key] = t;
            return t;
        }

        float sinCosAngle, sinCosSin, sinCosCos;
        bool sinCosValid;

        /// <summary>
        /// (float)Math.Sin / (float)Math.Cos of <paramref name="rad"/>, memoised on the exact bits of the last angle (the
        /// new-system particles all turn by the camera longitude): the same values as computing them each time.
        /// </summary>
        public void SinCosMemo(float rad, out float sin, out float cos)
        {
            if (!sinCosValid || BitConverter.SingleToInt32Bits(rad) != BitConverter.SingleToInt32Bits(sinCosAngle))
            {
                sinCosSin = (float)Math.Sin(rad);
                sinCosCos = (float)Math.Cos(rad);
                sinCosAngle = rad;
                sinCosValid = true;
            }
            sin = sinCosSin;
            cos = sinCosCos;
        }

        /// <summary>FUN_0054f5b0 — texture used when a layer frame index is out of range.</summary>
        public uint FallbackTexture => GetTexture("effect\\1.bmp");

        /// <summary>
        /// FUN_006c7940 (thiscall on an STR layer): lazily resolves layer texture <paramref name="index"/>
        /// as "effect\\" + name (".bmp" appended when the name has no extension), cached at layer+8+4i.
        /// </summary>
        public uint GetLayerTexture(uint layer, int index)
        {
            if (index >= M.I32(layer)) return FallbackTexture;
            uint cached = M.U32(layer + 8 + (uint)index * 4);
            if (cached != 0) return cached;
            string name = "effect\\" + ReadCString(M.U32(layer + 0x1c0 + (uint)index * 4));
            if (name.IndexOf('.') < 0) name += ".bmp";
            uint tex = GetTexture(name, blackKey: true);   // FUN_0054f5c0(name, 1, 0, 1, 0)
            M.U32(layer + 8 + (uint)index * 4, tex);
            return tex;
        }

        /// <summary>Length of the C string at <paramref name="a"/> (bytes before the terminator).</summary>
        public uint CStringLength(uint a)
        {
            uint n = 0;
            while (M.U8(a + n) != 0) n++;
            return n;
        }

        /// <summary>Whether the C string at <paramref name="a"/> equals the byte string <paramref name="bytes"/> (one char per byte), without reading it into a string.</summary>
        public bool CStringEquals(uint a, string bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = M.U8(a + (uint)i);
                if (b == 0 || b != bytes[i]) return false;
            }
            return M.U8(a + (uint)bytes.Length) == 0;
        }

        public string ReadCString(uint a)
        {
            var sb = new System.Text.StringBuilder();
            for (byte b; (b = M.U8(a)) != 0; a++) sb.Append((char)b);
            return sb.ToString();
        }

        /// <summary>operator new[] (0xd496f5) with the client's size-overflow clamp.</summary>
        public uint NewArray(ulong bytes) => M.Alloc(bytes > 0xffffffffUL ? 0xffffffffu : (uint)bytes);

        /// <summary>
        /// CRagEffect::fn_b5c890 — the per-effect STR layer player (ported: <see cref="RagEffect.StrPlay"/>).
        /// Overridable so scenario tests can isolate a handler from STR playback.
        /// </summary>
        public System.Func<RagEffect, float, float, float, bool> StrPlayerImpl;

        public bool StrPlayer(RagEffect effect, float dx, float dy, float dz)
            => StrPlayerImpl != null ? StrPlayerImpl(effect, dx, dy, dz) : effect.StrPlay(this, dx, dy, dz);

        /// <summary>Initialises an MSVC std::list {head, size} at <paramref name="list"/> with a fresh 0xc sentinel.</summary>
        public void InitList(uint list)
        {
            uint head = M.Alloc(0xc);
            M.U32(head, head);
            M.U32(head + 4, head);
            M.U32(list, head);
            M.U32(list + 4, 0);
        }

        /// <summary>std::list::push_back (as in 0x783f90 / inlined in LaunchEffectPrim): allocates a 0xc node.</summary>
        public void ListPushBack(uint list, uint value)
        {
            uint head = M.U32(list);
            if (M.U32(list + 4) == 0x15555555) throw new InvalidOperationException("list too long");
            uint node = M.Alloc(0xc);
            M.U32(node + 8, value);
            M.U32(list + 4, M.U32(list + 4) + 1);
            uint prev = M.U32(head + 4);
            M.U32(node, head);
            M.U32(node + 4, prev);
            M.U32(head + 4, node);
            M.U32(prev, node);
        }

        /// <summary>Removes the first node holding <paramref name="value"/> from an MSVC std::list {head, size}.</summary>
        public void ListRemove(uint list, uint value)
        {
            uint head = M.U32(list);
            for (uint node = M.U32(head); node != head; node = M.U32(node))
            {
                if (M.U32(node + 8) != value) continue;
                uint next = M.U32(node), prev = M.U32(node + 4);
                M.U32(prev, next);
                M.U32(next + 4, prev);
                M.U32(list + 4, M.U32(list + 4) - 1);
                M.Free(node);
                return;
            }
        }

        /// <summary>Fields Init (0xbd3980) zeroes, in its order (static: no array per constructed object).</summary>
        static readonly uint[] RenderObjectInitZero = { 0x10, 0x14, 0x18, 0x34, 0x38, 0x40, 0x3c, 0x44, 0x4c, 0x6c, 0x70, 0x78, 0x7c, 0x80, 0x48, 0xa8, 0x90, 0xfc, 0x110, 0xc8, 0xd8, 0xdc, 0xe8, 0x120, 0x124, 0x128, 0x12c };

        /// <summary>
        /// CRenderObject::CRenderObject (0xbcd750) + Init (0xbd3980), minus resource loading and
        /// timeGetTime (engine-side state that effect logic never reads). Shared verbatim by the
        /// oracle so both sides agree byte-for-byte.
        /// </summary>
        public void ConstructRenderObject(uint o)
        {
            M.U32(o, VtCGameObject);
            ObjectIdCounter++;
            M.U32(o + 0x8, (uint)ObjectIdCounter);
            M.U32(o + 0xc, (uint)(ObjectIdCounter >> 32));
            M.U32(o, VtCRenderObject);
            M.U32(o + 0x10, 0); M.U32(o + 0x14, 0); M.U32(o + 0x18, 0); M.U32(o + 0x1c, 0);
            M.U32(o + 0x2c, 0); M.U32(o + 0x30, 0xf);
            M.U8(o + 0x1c, 0);
            M.U32(o + 0x118, VtCInstantVar);
            M.U32(o + 0x11c, 0); M.U32(o + 0x120, 0);
            uint node = M.Alloc(0x20);
            M.U32(node, node); M.U32(node + 4, node); M.U32(node + 8, node);
            M.U16(node + 0xc, 0x101);
            M.U32(o + 0x11c, node);
            M.U32(o + 0x104, 0); M.U32(o + 0x108, 0);

            // Init (0xbd3980)
            foreach (uint off in RenderObjectInitZero)
                M.U32(o + off, 0);
            M.F32(o + 0x50, 1f); M.F32(o + 0x54, 1f); M.F32(o + 0x58, 1f);
            M.F32(o + 0x5c, 1f); M.F32(o + 0x60, 1f); M.F32(o + 0x64, 1f);
            M.U16(o + 0xa0, 0x101);
            M.U32(o + 0x98, 0xffffffff);
            M.U8(o + 0xa2, 0);
            M.U16(o + 0xa4, 0);
            M.U16(o + 0x9d, 0);
            M.U32(o + 0x88, 0);            // timeGetTime() in the client
            M.U8(o + 0x9f, 0); M.U8(o + 0x9c, 0); M.U8(o + 0xa3, 0);
            M.U32(o + 0x84, 0xffff0000);
            M.U16(o + 0xf4, 0); M.U8(o + 0xf6, 0); M.U16(o + 0xf8, 0); M.U8(o + 0xfa, 0);
            M.U8(o + 0x100, 0); M.U8(o + 0x114, 0);
            M.U8(o + 0xc2, 10);
            M.U32(o + 0xec, 1);
            M.U16(o + 0xf0, 0); M.U8(o + 0xf2, 0);
            M.U8(o + 0xb4, 1);
        }
    }
}
