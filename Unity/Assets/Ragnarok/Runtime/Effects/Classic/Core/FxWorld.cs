// Synced from reference/port/src/RoClassicFx/FxWorld.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Ragnarok.Effects.Classic
{
    /// <summary>Where STR resources come from (data\texture\effect\&lt;name&gt; in the GRFs).</summary>
    public interface IFxResourceProvider
    {
        byte[] LoadEffectFile(string strName);
    }

    /// <summary>Steps <see cref="FxWorld.Profiler"/> reports.</summary>
    public enum FxProfileKind
    {
        EffectProcess,
        PrimProcess,
        NewSystemProcess,
        EffectRender,
        PrimRender,
        NewSystemRender,
    }

    /// <summary>
    /// The classic effect world: owns CRagEffect instances and their CEffectPrims, advances them in
    /// fixed 16 ms logic ticks (FUN_00ba4560: 1000/60 truncated) and renders them on demand.
    /// Only effect ids / prim types that have been ported (and oracle-verified) are accepted.
    /// </summary>
    public sealed partial class FxWorld
    {
        public const int TickMilliseconds = 1000 / 60;

        public readonly FxRuntime Rt;
        public readonly IFxResourceProvider Resources;
        readonly List<RagEffect> effects = new List<RagEffect>();
        readonly List<Prim> prims = new List<Prim>();
        readonly List<Prim> newPrimScratch = new List<Prim>();
        readonly Dictionary<string, uint> strCache = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        double accumulatorMs;

        /// <summary>
        /// Optional profiler, null in normal play (tooling only, does not change what the world does): called after
        /// every timed step with its <see cref="FxProfileKind"/>, the effect id / prim type (-1 for whole-system steps)
        /// and the <see cref="System.Diagnostics.Stopwatch"/> ticks it took.
        /// </summary>
        public Action<FxProfileKind, int, long> Profiler;

        public FxWorld(FxRuntime runtime, IFxResourceProvider resources)
        {
            Rt = runtime;
            Resources = resources;
            Rt.EnsureNewEffectSystem();
        }

        public int EffectCount => effects.Count;
        public int PrimCount => prims.Count;
        public IReadOnlyList<RagEffect> Effects => effects;
        public IReadOnlyList<Prim> Prims => prims;

        /// <summary>
        /// Effect ids the port can show: a ported CRagEffect OnProcess case / pure STR effect (<see cref="Launch"/>) or a
        /// ported class of the newer CEffect system (<see cref="IsNewSystem"/>, launched with <see cref="LaunchNew"/>).
        /// </summary>
        public static bool IsPorted(int efId) => IsClassicPorted(efId) || IsNewSystem(efId);

        /// <summary>Ids <see cref="Launch"/> (CAbleToMakeEffect::LaunchEffect + CRagEffect) accepts.</summary>
        public static bool IsClassicPorted(int efId) => FxDispatch.IsEffectPorted(efId) || ClassicEffectTables.PureStr.ContainsKey(efId);

        /// <summary>
        /// Ids the client hands to its newer effect system first (CEffectMgr, FUN_00a4d090) and whose class is ported.
        /// Wherever the client launches an effect through FUN_00a4d090 (skill hits, heals, throws, Storm Kick) the
        /// new system's version is what it shows, even for ids the old engine also knows (312, 298, 435 …):
        /// launch them with <see cref="LaunchNew"/>.
        /// </summary>
        public static bool IsNewSystem(int efId) => FxRuntime.IsNewSystemEffect(efId);

        /// <summary>
        /// FUN_00a4d090 — launches a new-system effect on the actor shim <paramref name="owner"/> (its account id at
        /// +0x110; 0 or a throw-away owner = no owner: the effect stays where it starts), starting at the owner's
        /// position, aimed at the actor <paramref name="targetAid"/> (thrown items fly to it; must be a registered
        /// actor, <see cref="EnsureActor"/>, or the local player; <see cref="NoTarget"/> = none). The effect refers to
        /// actors only by account id, so a throw-away owner can be freed right after the call.
        /// Returns the CEffect (0 when the id is not a new-system id). EF 989 throws the owner's spirit spheres
        /// (<see cref="SetOwnerSpheres"/>); it is refused for the local player, whose object (world +0x2c) has no such fields.
        /// </summary>
        public uint LaunchNew(int efId, uint owner, uint targetAid = NoTarget)
        {
            if (!IsNewSystem(efId)) return 0;
            var M = Rt.M;
            uint ownerAid = owner != 0 ? M.U32(owner + 0x110) : 0;
            if (efId == 989 && ownerAid != 0 && ownerAid == Rt.PlayerAid) return 0;
            float x = owner != 0 ? M.F32(owner + 0x10) : 0f, y = owner != 0 ? M.F32(owner + 0x14) : 0f, z = owner != 0 ? M.F32(owner + 0x18) : 0f;
            return Rt.LaunchNewEffect_a4d090(ownerAid, efId, x, y, z, targetAid);
        }

        /// <summary>FUN_00a4d090's "no target" account id (-1).</summary>
        public const uint NoTarget = 0xffffffff;

        /// <summary>Live (not yet retired) new-system effects.</summary>
        public int NewEffectCount => Rt.LiveNewEffectCount;

        /// <summary>True while a new-system effect returned by <see cref="LaunchNew"/> is still running.</summary>
        public bool IsNewEffectAlive(uint effect) => Rt.IsNewEffectLive(effect);

        /// <summary>
        /// Map change (FUN_00a4db00): retires and destroys every new-system effect. Call it when the host changes map.
        /// </summary>
        public void ClearNewEffects() => Rt.ClearNewEffects_a4db00();

        /// <summary>
        /// Creates a game-object shim the effect can follow (position at +0x10/+0x14/+0x18, RO world units). It is as
        /// big as the CPc fields effects and the actor renderer read (spirit spheres at +0x3cc/+0x3d0, zero = none; footprint
        /// state +0x45c..+0x468, Shared/actorfx).
        /// </summary>
        public uint CreateOwner(float x, float y, float z)
        {
            uint o = Rt.M.Alloc(0x480);
            Rt.ConstructRenderObject(o);
            SetOwnerPosition(o, x, y, z);
            return o;
        }

        public void SetOwnerPosition(uint owner, float x, float y, float z)
        {
            Rt.M.F32(owner + 0x10, x); Rt.M.F32(owner + 0x14, y); Rt.M.F32(owner + 0x18, z);
        }

        /// <summary>
        /// The owner's spirit spheres (CPc +0x3cc kind: 1 water, 2 stone, 3 fire, 4 lightning; +0x3d0 count), read by EF 989
        /// when it is launched: one thrown orb per sphere.
        /// </summary>
        public void SetOwnerSpheres(uint owner, int kind, int count)
        {
            Rt.M.I32(owner + 0x3cc, kind); Rt.M.I32(owner + 0x3d0, count);
        }

        /// <summary>Heading of a game object (CRenderObject +0x4c, degrees: 0 = towards -z, 90 = +x), read by effects launched on it (+0x14c).</summary>
        public void SetOwnerHeading(uint owner, float degrees) => Rt.M.F32(owner + 0x4c, degrees);

        readonly Dictionary<uint, uint> actorsByAid = new Dictionary<uint, uint>();

        /// <summary>
        /// The game-object shim standing for the actor with account id <paramref name="aid"/>, created on first use and
        /// registered in the world's actor list (world +0x10; CWorld::GetGameActorByAID walks it, homing prims and
        /// LaunchEffect-on-actor find it). Returns 0 for aid 0.
        /// </summary>
        public uint EnsureActor(uint aid, float x, float y, float z)
        {
            if (aid == 0) return 0;
            if (actorsByAid.TryGetValue(aid, out uint actor))
            {
                SetOwnerPosition(actor, x, y, z);
                return actor;
            }
            uint list = Rt.World + 0x10;
            if (Rt.M.U32(list) == 0) Rt.InitList(list);
            actor = CreateOwner(x, y, z);
            Rt.M.U32(actor + 0x110, aid);
            Rt.ListPushBack(list, actor);
            actorsByAid[aid] = actor;
            return actor;
        }

        /// <summary>
        /// Forgets an actor (left the view); its shim is unlinked from the world list and freed. Like the client's actor
        /// destructor (CAbleToMakeEffect::~CAbleToMakeEffect 0xbcdc60 → effect msg 0x18), every effect still on it loses
        /// its owner first (<see cref="ReleaseOwner"/>), so none writes into the freed shim; they die on the next pass.
        /// </summary>
        public void RemoveActor(uint aid)
        {
            if (!actorsByAid.TryGetValue(aid, out uint actor)) return;
            actorsByAid.Remove(aid);
            DetachMsgEffects(actor);   // ~CGameActor: its damage numbers stay where they are (Shared/msgeffect)
            ReleaseOwner(actor);
            Rt.ListRemove(Rt.World + 0x10, actor);
            Rt.M.Free(actor);
        }

        public bool TryGetActor(uint aid, out uint actor) => actorsByAid.TryGetValue(aid, out actor);

        /// <summary>
        /// Installs the map's GAT as the world's attribute object (world +0x30) for FUN_006c32a0 GetHeight:
        /// +0x110 width, +0x114 height, +0x118 cell size (5), +0x11c cells of 0x14 bytes (four corner heights in
        /// RO units, y down, then the cell type). The client centres the map on the origin: cell (cx, cz) covers
        /// RO x in [(cx - w/2)·5, (cx + 1 - w/2)·5) and likewise z — hosts must map their coordinates the same way.
        /// </summary>
        public void SetGround(int width, int height, float[] cornerHeights, uint[] cellTypes = null)
        {
            if (cornerHeights.Length != width * height * 4) throw new ArgumentException("cornerHeights must hold 4 values per cell", nameof(cornerHeights));
            var M = Rt.M;
            uint old = M.U32(Rt.World + 0x30);
            if (old != 0) { M.Free(M.U32(old + 0x11c)); M.Free(old); }
            uint attr = M.Alloc(0x120);
            M.I32(attr + 0x110, width); M.I32(attr + 0x114, height); M.I32(attr + 0x118, 5);
            uint cells = M.Alloc((uint)(width * height) * 0x14);
            for (int i = 0; i < width * height; i++)
            {
                uint c = cells + (uint)i * 0x14;
                for (uint k = 0; k < 4; k++) M.F32(c + k * 4, cornerHeights[i * 4 + (int)k]);
                M.U32(c + 0x10, cellTypes != null ? cellTypes[i] : 0u);
            }
            M.U32(attr + 0x11c, cells);
            M.U32(Rt.World + 0x30, attr);
        }

        /// <summary>CAbleToMakeEffect::LaunchEffect (0xbd1f40) + CRagEffect::Init (0xb422b0). Returns null when not ported.</summary>
        public RagEffect? Launch(int efId, uint owner)
        {
            if (!IsClassicPorted(efId)) return null;
            var M = Rt.M;
            var e = new RagEffect(M, M.Alloc(RagEffect.SizeOf));
            e.Construct(Rt);
            // Init preamble
            M.I32(e.A + 0x168, efId);
            M.U32(e.A + 0x138, owner);
            e.m_pos_x = M.F32(owner + 0x10); e.m_pos_y = M.F32(owner + 0x14); e.m_pos_z = M.F32(owner + 0x18);
            M.U32(e.A + 0x14c, M.U32(owner + 0x4c));
            e.f_17c = ClassicEffectTables.DefaultDuration(efId);   // vf18 SetDuration(FUN_00b3c420(id))
            var str = ClassicEffectTables.ResolveStr(efId, Rt.MinEffect, Rt.Rand);
            if (str.HasValue)
            {
                if (str.Value.Repeat >= 0) M.I32(e.A + 0x11c80, str.Value.Repeat);
                e.AttachStr(LoadStr(str.Value.File));
            }
            effects.Add(e);
            return e;
        }

        uint LoadStr(string name)
        {
            if (strCache.TryGetValue(name, out uint res)) return res;
            byte[] file = Resources?.LoadEffectFile(name);
            res = file == null ? 0u : FxStrResource.Load(Rt.M, file);
            strCache[name] = res;
            return res;
        }

        /// <summary>
        /// Advances by real time like CGameMode::OnUpdate: ticks = elapsed / 16 ms (FUN_00ba4560); a frame with
        /// no whole tick still runs one process pass with DAT_01555ec4 = 0 (ids/types flagged to process
        /// without a tick advance then), otherwise N passes with DAT_01555ec4 = N.
        /// </summary>
        public int Advance(double deltaMs)
        {
            accumulatorMs += deltaMs;
            int ticks = (int)(accumulatorMs / TickMilliseconds);
            accumulatorMs -= ticks * TickMilliseconds;
            Rt.TicksThisFrame = ticks;
            if (ticks == 0) Pass();
            else for (int i = 0; i < ticks; i++) Pass();
            return ticks;
        }

        /// <summary>Exactly one logic tick (DAT_01555ec4 = 1).</summary>
        public void Tick()
        {
            Rt.TicksThisFrame = 1;
            Pass();
        }

        /// <summary>
        /// One world process pass as in CGameMode::OnUpdate: on a real tick the new-system effects first
        /// (FUN_00a63060), then the world pass: actor flag-word resets (Shared/actorfx), CRagEffects, then prims (world
        /// object order). Retired new-system effects are destroyed right away (see <see cref="FxRuntime.DestroyDeadEffects_a4db00"/>).
        /// </summary>
        void Pass()
        {
            SyncPlayerObject();
            var profiler = Profiler;
            long t = profiler != null ? Stopwatch.GetTimestamp() : 0;
            if (Rt.TicksThisFrame > 0)
            {
                Rt.UpdateNewEffects_a63060();
                Rt.DestroyDeadEffects_a4db00();
                if (profiler != null) t = Lap(profiler, FxProfileKind.NewSystemProcess, -1, t);
            }
            ProcessActorPrologues();
            ProcessEffectMakers();   // RSW CEffectMaker objects, first in the world list (Shared/mapfx)
            for (int i = 0; i < effects.Count; i++)
            {
                RagEffect e = effects[i];
                int id = profiler != null ? e.m_type : 0;
                bool alive = ProcessEffect(e);
                if (!alive)
                {
                    DestroyEffect(e);
                    effects.RemoveAt(i--);
                }
                if (profiler != null) t = Lap(profiler, FxProfileKind.EffectProcess, id, t);
            }
            for (int i = 0; i < prims.Count; i++)
            {
                Prim p = prims[i];
                int type = profiler != null ? p.m_type : 0;
                bool alive = ProcessPrim(p);
                if (!alive)
                {
                    DestroyPrim(p);
                    prims.RemoveAt(i--);
                }
                if (profiler != null) t = Lap(profiler, FxProfileKind.PrimProcess, type, t);
            }
            ProcessGroundScopes();   // CGroundScope objects of the world list (Shared/castvis)
            ProcessArrows();         // CArrowEffect objects of the world list (Shared/arrow)
            ProcessMsgEffects();     // CMsgEffect damage numbers / messages (Shared/msgeffect)
        }

        static long Lap(Action<FxProfileKind, int, long> profiler, FxProfileKind kind, int id, long start)
        {
            long now = Stopwatch.GetTimestamp();
            profiler(kind, id, now - start);
            return Stopwatch.GetTimestamp();   // the profiler's own time is not charged to the next step
        }

        bool ProcessEffect(RagEffect e)
        {
            int before = e.PrimListCount;
            bool alive = e.OnProcess(Rt);
            TrackNewPrims(e, before);
            return alive;
        }

        void TrackNewPrims(RagEffect e, int before)
        {
            int count = e.PrimListCount;
            if (count <= before) return;
            // new prims are at the tail of the effect's list
            uint head = Rt.M.U32(e.A + 0x1a4);
            uint node = Rt.M.U32(head + 4);
            var added = newPrimScratch;
            added.Clear();
            for (int i = before; i < count; i++, node = Rt.M.U32(node + 4)) added.Add(new Prim(Rt.M, Rt.M.U32(node + 8)));
            for (int i = added.Count - 1; i >= 0; i--) prims.Add(added[i]);   // list order (the walk went tail first)
            added.Clear();
        }

        bool ProcessPrim(Prim p) => p.OnProcess(Rt);

        void DestroyEffect(RagEffect e)
        {
            // prims survive their effect only until their next OnProcess (master cleared by msg 0x18)
            foreach (var p in prims) if (p.m_master_ptr == e.A) p.m_master_ptr = 0;
            Rt.M.Free(e.A);
        }

        void DestroyPrim(Prim p)
        {
            uint master = p.m_master_ptr;
            if (master != 0) new RagEffect(Rt.M, master).RemovePrim(Rt, p.A);
            Rt.ListRemove(Rt.World + 0x8, p.A);
            if (p.f_144 != 0) Rt.M.Free((uint)p.f_144);
            Rt.M.Free(p.A);
        }

        /// <summary>Renders every effect's STR layers and every prim. Call once per rendered frame.</summary>
        public void Render()
        {
            uint matrix = Rt.View + 0x98;
            Rt.Renderer.BeginFrame();
            var profiler = Profiler;
            if (profiler == null)
            {
                for (int i = 0; i < effects.Count; i++) effects[i].Render(Rt, matrix);   // renders may launch effects (prim 0x78, 0x6d)
                for (int i = 0; i < prims.Count; i++) prims[i].Render(Rt, matrix);
                RenderGroundScopes(matrix);   // CGroundScope::Render (Shared/castvis)
                RenderArrows(matrix);         // CArrowEffect::Render (Shared/arrow)
                RenderMsgEffects(matrix);     // CMsgEffect::Render (Shared/msgeffect)
                Rt.RenderNewEffects_a63170();                                           // after the world objects (CView::OnRender)
                return;
            }

            long t = Stopwatch.GetTimestamp();
            for (int i = 0; i < effects.Count; i++)
            {
                effects[i].Render(Rt, matrix);
                t = Lap(profiler, FxProfileKind.EffectRender, effects[i].m_type, t);
            }
            for (int i = 0; i < prims.Count; i++)
            {
                prims[i].Render(Rt, matrix);
                t = Lap(profiler, FxProfileKind.PrimRender, prims[i].m_type, t);
            }
            RenderGroundScopes(matrix);
            RenderArrows(matrix);
            RenderMsgEffects(matrix);
            Rt.RenderNewEffects_a63170();
            Lap(profiler, FxProfileKind.NewSystemRender, -1, t);
        }

        /// <summary>
        /// The client's player object (world +0x2c) is the local player's actor: when the host registered an actor
        /// shim for <see cref="FxRuntime.PlayerAid"/>, its position and heading are mirrored into the player object
        /// (effects on the local player follow it, the new system measures its 130-unit processing range from it).
        /// </summary>
        void SyncPlayerObject()
        {
            if (Rt.PlayerAid == 0 || !actorsByAid.TryGetValue(Rt.PlayerAid, out uint actor)) return;
            var M = Rt.M;
            Rt.SetPlayerPosition(M.F32(actor + 0x10), M.F32(actor + 0x14), M.F32(actor + 0x18));
            M.U32(Rt.Player + 0x4c, M.U32(actor + 0x4c));
        }
    }

    public readonly partial struct RagEffect
    {
        public int PrimListCount => (int)M.U32(A + 0x1a8);

        /// <summary>vf16 msg 0x19: unlink a prim from this effect's list.</summary>
        public void RemovePrim(FxRuntime rt, uint prim) => rt.ListRemove(A + 0x1a4, prim);
    }

    public readonly partial struct Prim
    {
        public uint m_master_ptr { get => M.U32(A + 0x140); set => M.U32(A + 0x140, value); }
    }
}
