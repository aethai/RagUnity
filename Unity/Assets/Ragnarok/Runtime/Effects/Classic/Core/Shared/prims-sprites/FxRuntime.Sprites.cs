// Synced from reference/port/src/RoClassicFx/Shared/prims-sprites/FxRuntime.Sprites.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Collections.Generic;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Sprite resource subsystem (package prims-sprites): the client's resource manager as far as
    /// SPR/ACT resources go, CRenderObject::SetSprName / SetActName, and the ACT queries the
    /// sprite prims use. The resource manager and the queries are shims shared verbatim with the
    /// oracle (tests/Packages/prims-sprites/SpriteOracleHooks.cs), so both sides see identical
    /// resource objects in FxMemory.
    /// </summary>
    public sealed partial class FxRuntime
    {
        /// <summary>
        /// Reads a client data file, given its path relative to the client root in the client's own
        /// form (e.g. "data\\sprite\\이팩트\\particle1.spr"). Null = missing (the resource load fails).
        /// </summary>
        public Func<string, byte[]> LoadDataFile;

        /// <summary>Value the resource manager stores as timeGetTime() in resource timestamps (+8).</summary>
        public uint ResourceClock;

        /// <summary>FUN_00a1de30 — CResMgr singleton (&amp;DAT_014f0d58). Only ever passed back to the shims.</summary>
        public const uint ResMgrAddress = 0x014f0d58;

        readonly Dictionary<string, uint> spriteResCache = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<uint, FxSprite> sprModels = new Dictionary<uint, FxSprite>();
        readonly Dictionary<uint, FxAct> actModels = new Dictionary<uint, FxAct>();
        uint spriteStatics;

        /// <summary>Decoded pixels/palette of the CSprRes at <paramref name="res"/> (host rendering).</summary>
        public FxSprite GetSpriteModel(uint res) => res != 0 && sprModels.TryGetValue(res, out var s) && s.Address == res ? s : null;

        public FxAct GetActModel(uint res) => res != 0 && actModels.TryGetValue(res, out var a) && a.Address == res ? a : null;

        /// <summary>
        /// Host helper: the decoded image a sprite draw refers to (draw.Palette − 0x110 is the CSprRes; draw.Image
        /// one of its SprImg headers). Returns null when the resource is unknown.
        /// </summary>
        public FxSprImage ResolveSpriteImage(in FxSpriteDraw draw, out FxSprite sprite)
        {
            sprite = GetSpriteModel(draw.Palette - 0x110);
            if (sprite == null) return null;
            uint res = sprite.Address;
            for (int type = 0; type < 2; type++)
            {
                uint vec = res + 0x510 + (uint)type * 0xc;
                uint begin = M.U32(vec), end = M.U32(vec + 4);
                for (uint p = begin, i = 0; p < end; p += 4, i++)
                    if (M.U32(p) == draw.Image) return sprite.Get(type, (int)i);
            }
            return null;
        }

        /// <summary>FUN_00a1de30 (GetResMgr).</summary>
        public uint GetResMgr() => ResMgrAddress;

        /// <summary>
        /// Resource path the manager builds for a name: SPR/ACT live under "data\\sprite\\"
        /// (names passed by effects are relative, e.g. "이팩트\\particle1.spr").
        /// </summary>
        public static string ResourcePath(string name)
        {
            string n = name.Replace('/', '\\');
            if (n.StartsWith("data\\", StringComparison.OrdinalIgnoreCase)) return n;
            return "data\\sprite\\" + n;
        }

        /// <summary>
        /// A still-valid cached resource for <paramref name="name"/> (validated against the heap, so a
        /// test harness that rewinds the heap simply makes the entry disappear).
        /// </summary>
        public bool TryFindResource(string name, out uint res)
        {
            res = 0;
            if (name == null || !spriteResCache.TryGetValue(name, out uint r)) return false;
            uint vt = IsStrName(name) ? VtCEZeffectRes : IsActName(name) ? FxSpriteResources.VtCActRes : FxSpriteResources.VtCSprRes;
            if (!FxSpriteResources.IsRes(M, r, vt, name)) { spriteResCache.Remove(name); return false; }
            res = r;
            return true;
        }

        static bool IsActName(string name) => name.EndsWith(".act", StringComparison.OrdinalIgnoreCase);

        static bool IsStrName(string name) => name.EndsWith(".str", StringComparison.OrdinalIgnoreCase);

        /// <summary>CEZeffectRes — the STR animation resource (both engines play it; the new system asks CResMgr for it).</summary>
        public const uint VtCEZeffectRes = 0x00f8327c;

        /// <summary>
        /// CResMgr::Get (FUN_00a1b030) for SPR/ACT/STR resources: cached → timestamp refresh; otherwise
        /// load (CSprRes / CActRes / CEZeffectRes vf05), timestamp, cache. No AddRef (callers do that). 0 if
        /// missing, unreadable or not an SPR/ACT/STR name.
        /// </summary>
        public uint ResGet(string name)
        {
            if (name == null) return 0;
            if (TryFindResource(name, out uint res))
            {
                M.U32(res + 8, ResourceClock);
                return res;
            }
            if (IsStrName(name))
            {
                // CResMgr resolves STR names ("Pneuma1.str") under data\texture\effect\; CEZeffectRes::vf05 layout as FxStrResource
                byte[] strFile = LoadDataFile?.Invoke(@"data\texture\effect\" + name);
                if (strFile == null) return 0;
                res = FxStrResource.Load(M, strFile);
                FxSpriteResources.InitRes(M, res, VtCEZeffectRes, name);
                M.U32(res + 8, ResourceClock);
                spriteResCache[name] = res;
                return res;
            }
            bool act = IsActName(name);
            if (!act && !name.EndsWith(".spr", StringComparison.OrdinalIgnoreCase)) return 0;
            byte[] file = LoadDataFile?.Invoke(ResourcePath(name));
            if (file == null) return 0;
            if (act)
            {
                res = FxSpriteResources.LoadAct(M, name, file, ResGet, out var model);
                if (res != 0) actModels[res] = model;
            }
            else
            {
                res = FxSpriteResources.LoadSpr(M, name, file, out var model);
                if (res != 0) sprModels[res] = model;
            }
            if (res == 0) return 0;
            M.U32(res + 8, ResourceClock);
            spriteResCache[name] = res;
            return res;
        }

        /// <summary>FUN_00a1c390 — CRes::AddRef (lock count +4).</summary>
        public void ResAddRef(uint res) => M.I32(res + 4, M.I32(res + 4) + 1);

        /// <summary>FUN_00a1d3f0 — CRes::Release: timestamp = timeGetTime(), lock count − 1.</summary>
        public void ResRelease(uint res)
        {
            M.U32(res + 8, ResourceClock);
            M.I32(res + 4, M.I32(res + 4) - 1);
        }

        /// <summary>CRenderObject::SetSprName (0xbe6c80): sprite resource at object +0x104.</summary>
        public void SetSprName(uint obj, string name)
        {
            uint old = M.U32(obj + 0x104);
            if (old != 0) ResRelease(old);
            GetResMgr();
            uint res = ResGet(name);
            M.U32(obj + 0x104, res);
            if (res != 0) ResAddRef(res);
        }

        /// <summary>
        /// CRenderObject::SetActName (0xbe3a70): action resource at object +0x108. The client AddRefs the
        /// result unconditionally (a missing .act faults on address 4; so does this port).
        /// </summary>
        public void SetActName(uint obj, string name)
        {
            uint old = M.U32(obj + 0x108);
            if (old != 0) ResRelease(old);
            GetResMgr();
            uint res = ResGet(name);
            M.U32(obj + 0x108, res);
            ResAddRef(res);
        }

        /// <summary>FUN_00bd1c00 — CRenderObject sprite resource (+0x104).</summary>
        public uint GetSprRes(uint obj) => M.U32(obj + 0x104);

        /// <summary>FUN_00bd0720 — CRenderObject action resource (+0x108).</summary>
        public uint GetActRes(uint obj) => M.U32(obj + 0x108);

        // ---- ACT queries -----------------------------------------------------------------

        /// <summary>
        /// Heap home of the client's static fallback objects (empty CAction 0x11a8ff8, empty CMotion 0x11a8d28,
        /// default CSprClip 0x114e070). Layout: +0 magic, +4 self, +8 action, +0x14 motion, +0x58 clip.
        /// </summary>
        public uint SpriteStatics
        {
            get
            {
                uint s = spriteStatics;
                if (s != 0 && M.Contains(s, 0x80) && s < M.BumpPointer && M.U32(s) == StaticsMagic && M.U32(s + 4) == s) return s;
                s = M.Alloc(0x80);
                M.U32(s, StaticsMagic);
                M.U32(s + 4, s);
                uint clip = s + 0x58;
                M.I32(clip + 8, -1);
                M.U32(clip + 0x10, 0xffffffff);
                M.F32(clip + 0x14, 1f);
                M.F32(clip + 0x18, 1f);
                spriteStatics = s;
                return s;
            }
        }

        const uint StaticsMagic = 0x53505346; // "FSPS"

        public uint EmptyAction => SpriteStatics + 8;
        public uint EmptyMotion => SpriteStatics + 0x14;
        public uint DefaultClip => SpriteStatics + 0x58;

        /// <summary>FUN_006c15a0 (thiscall on CActRes) — action vector for <paramref name="action"/>, else the empty static.</summary>
        public uint ActGetAction(uint actRes, uint action)
        {
            uint begin = M.U32(actRes + 0x110);
            int count = (int)(M.U32(actRes + 0x114) - begin) / (int)FxSpriteResources.ActionSize;
            if (action < (uint)count) return begin + action * FxSpriteResources.ActionSize;
            return EmptyAction;
        }

        /// <summary>
        /// FUN_006c1790 (thiscall on CActRes) — motion <paramref name="motion"/> of <paramref name="action"/>;
        /// out of range → the action's first motion, or the empty static motion.
        /// </summary>
        public uint ActGetMotion(uint actRes, uint action, uint motion)
        {
            uint act = ActGetAction(actRes, action);
            uint begin = M.U32(act);
            uint end = M.U32(act + 4);
            if (motion < (uint)((int)(end - begin) / (int)FxSpriteResources.MotionSize)) return begin + motion * FxSpriteResources.MotionSize;
            if (begin != end) return begin;
            return EmptyMotion;
        }

        /// <summary>FUN_006c1990 (thiscall on CActRes) — motion count of an action (at least 1).</summary>
        public int ActMotionCount(uint actRes, uint action)
        {
            uint act = ActGetAction(actRes, action);
            int n = (int)(M.U32(act + 4) - M.U32(act)) / (int)FxSpriteResources.MotionSize;
            return n == 0 ? 1 : n;
        }

        /// <summary>FUN_006c1670 (thiscall on CMotion) — clip <paramref name="index"/>, else the default static clip.</summary>
        public uint MotionGetClip(uint motion, uint index)
        {
            uint begin = M.U32(motion + 0x20);
            if (index < (uint)((int)(M.U32(motion + 0x24) - begin) / (int)FxSpriteResources.ClipSize)) return begin + index * FxSpriteResources.ClipSize;
            return DefaultClip;
        }

        // ---- other client services used by the sprite prims ----------------------------------

        /// <summary>
        /// FUN_006c32a0 (thiscall on the world's GAT attribute object, world +0x30) — bilinear cell height.
        /// Attr layout: +0x110 width, +0x114 height, +0x118 cell size (int), +0x11c cells (0x14 each, 4 corner heights).
        /// The client has no null check; here a missing attr object yields 0.
        /// </summary>
        public float GatGetHeight_6c32a0(uint attr, float x, float z)
        {
            if (attr == 0) return 0f;
            int w = M.I32(attr + 0x110), h = M.I32(attr + 0x114);
            float cell = (float)M.I32(attr + 0x118);
            float px = (float)w * cell * 0.5f + x;
            float pz = (float)h * cell * 0.5f + z;
            int cx = FxOps.Cvtt(px / cell);
            int cz = FxOps.Cvtt(pz / cell);
            if (cx < 0 || cx >= w || cz < 0 || cz >= h) return 0f;
            uint c = M.U32(attr + 0x11c) + (uint)((w * cz + cx) * 0x14);
            if (c == 0) return 0f;
            pz = pz - (float)cz * cell;
            float a = (M.F32(c + 8) - M.F32(c)) * pz / cell + M.F32(c);
            px = px - (float)cx * cell;
            float b = (M.F32(c + 0xc) - M.F32(c + 4)) * pz / cell + M.F32(c + 4);
            return px * (b - a) / cell + a;
        }

        /// <summary>
        /// FUN_00bd0b10 (thiscall on a CRenderObject/actor) — scales <paramref name="scale"/> for actors with
        /// body-state flag 0x10 (+0xe8) by a sine of the +0xf0 counter (sin table read by byte offset).
        /// </summary>
        public void ActorScale_bd0b10(uint obj, ref float scale)
        {
            if ((M.U32(obj + 0xe8) & 0x10) == 0) return;
            uint b = M.U8(obj + 0xf0);
            uint e = b * 4 + 0x5a;
            if (e > 0x10e) { scale = scale * 0.5f; return; }
            int index = e >= 0x5a ? (int)(b * 4 + 0x5a) : 0x5a;   // byte offset (b*0x10 + 0x168) / 4
            scale = ((FxMath.SinTableAt(index) + 1.0f) * 0.25f + 0.5f) * scale;
        }
    }

    public static partial class FxMath
    {
        /// <summary>
        /// Direct read of the client's sine table (DAT_011a4a28[index]); negative indices run back into
        /// the preceding tables exactly like the original's unchecked array reads.
        /// </summary>
        public static float SinTableAt(int index) => Tables[SinBase + index];

        /// <summary>Direct read of the client's cosine table (DAT_011a5570[index]).</summary>
        public static float CosTableAt(int index) => Tables[CosBase + index];
    }
}
