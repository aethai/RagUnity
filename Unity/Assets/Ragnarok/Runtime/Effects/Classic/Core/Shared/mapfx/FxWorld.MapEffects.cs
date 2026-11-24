// Synced from reference/port/src/RoClassicFx/Shared/mapfx/FxWorld.MapEffects.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Collections.Generic;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// One RSW effect record (object type 4; C3dWorldRes::Load 0x6db4d0 reads it as 0x74 raw bytes in every RSW
    /// version): name[80] (+0x00, unused by the client), position (+0x50, the client's world frame: x / z centred on
    /// the map like actors, y down), effect id (+0x5c), emit speed (+0x60), four params (+0x64..+0x70).
    /// </summary>
    public readonly struct RswEffectRecord
    {
        /// <summary>The record's name as a byte string (one char per byte, CP949 not decoded).</summary>
        public readonly string Name;
        public readonly float X, Y, Z;
        public readonly int Id;
        public readonly float EmitSpeed;
        public readonly float P0, P1, P2, P3;

        public RswEffectRecord(string name, float x, float y, float z, int id, float emitSpeed, float p0, float p1, float p2, float p3)
        {
            Name = name ?? ""; X = x; Y = y; Z = z; Id = id; EmitSpeed = emitSpeed; P0 = p0; P1 = p1; P2 = p2; P3 = p3;
        }

        public RswEffectRecord(int id, float x, float y, float z, float emitSpeed, float p0 = 0f, float p1 = 0f, float p2 = 0f, float p3 = 0f)
            : this("", x, y, z, id, emitSpeed, p0, p1, p2, p3) { }
    }

    /// <summary>The object section of an RSW file as C3dWorldRes::Load (vf05 0x006db4d0) reads it.</summary>
    public static class RswMapEffects
    {
        /// <summary>
        /// The effect records of an RSW file, in file order (the order the client creates their emitters in).
        /// Header fields are skipped exactly as the client reads them per version (1.x … 2.7); versions above 2.7
        /// are refused by the client ("Unsupported Version") and throw here. An unknown object type ends the
        /// object loop without reading (the client's switch has no default and would then misread the rest).
        /// </summary>
        public static List<RswEffectRecord> Parse(byte[] rsw)
        {
            if (rsw == null || rsw.Length < 6 || rsw[0] != 'G' || rsw[1] != 'R' || rsw[2] != 'S' || rsw[3] != 'W')
                throw new FormatException("Invalid Format");
            int major = rsw[4], minor = rsw[5];
            if (major > 2 || (major == 2 && minor > 7)) throw new FormatException($"Unsupported Version {major}.{minor}");
            bool Ge(int ma, int mi) => major > ma || (major == ma && minor >= mi);
            int p = 6;
            uint build = 0;
            if (major == 2 && minor > 4) { build = BitConverter.ToUInt32(rsw, p); p += 4; }
            if (major == 2 && minor > 1) p += 1;
            p += 40;                                     // ini
            p += 40;                                     // gnd
            if (Ge(1, 4)) p += 40;                       // gat
            p += 40;                                     // scr
            if (!(major == 2 && minor >= 6))
            {
                if (Ge(1, 3)) p += 4;                    // water level
                if (Ge(1, 8)) p += 16;                   // water type, wave height / speed / pitch
                if (Ge(1, 9)) p += 4;                    // water animation speed
            }
            if (Ge(1, 5)) p += 8 + 12 + 12;              // light longitude, latitude, diffuse, ambient
            if (Ge(1, 7)) p += 4;                        // shadow opacity
            if (Ge(1, 6)) p += 16;                       // ground bounds
            if (Ge(2, 7) && build > 0xdc) p += 4 + BitConverter.ToInt32(rsw, p) * 4;   // FUN_006dc630(2.7) <= 0
            int count = BitConverter.ToInt32(rsw, p); p += 4;
            var list = new List<RswEffectRecord>();
            for (int i = 0; i < count; i++)
            {
                int type = BitConverter.ToInt32(rsw, p); p += 4;
                switch (type)
                {
                    case 1:
                        if (Ge(1, 3))
                        {
                            p += 40 + 12;                // name, anim type, anim speed, block type
                            if (major >= 3 || (major == 2 && minor >= 6 && build >= 0xba)) p += 1;
                            if (Ge(2, 7)) p += 4;
                        }
                        p += 0xc4;
                        break;
                    case 2: p += 0x6c; break;
                    case 3: p += major < 2 ? 0xbc : 0xc0; break;
                    case 4:
                    {
                        int n = 0;
                        while (n < 80 && rsw[p + n] != 0) n++;
                        var chars = new char[n];
                        for (int k = 0; k < n; k++) chars[k] = (char)rsw[p + k];
                        list.Add(new RswEffectRecord(new string(chars),
                            BitConverter.ToSingle(rsw, p + 0x50), BitConverter.ToSingle(rsw, p + 0x54), BitConverter.ToSingle(rsw, p + 0x58),
                            BitConverter.ToInt32(rsw, p + 0x5c), BitConverter.ToSingle(rsw, p + 0x60),
                            BitConverter.ToSingle(rsw, p + 0x64), BitConverter.ToSingle(rsw, p + 0x68),
                            BitConverter.ToSingle(rsw, p + 0x6c), BitConverter.ToSingle(rsw, p + 0x70)));
                        p += 0x74;
                        break;
                    }
                    default:
                        return list;
                }
            }
            return list;
        }
    }

    /// <summary>What <see cref="FxWorld.LoadMapEffects"/> did with a map's records.</summary>
    public sealed class MapEffectsLoadResult
    {
        /// <summary>CEffectMaker objects created (classic ids; ported or not — an unported id launches nothing).</summary>
        public int Makers;
        /// <summary>New-system effects launched once through FUN_00a4d090 (ported classes).</summary>
        public int NewSystemLaunched;
        /// <summary>Records whose id is a new-system id with no ported class: the client would launch them, the port can't.</summary>
        public readonly List<int> NewSystemSkippedIds = new List<int>();
        /// <summary>Maker ids that <see cref="FxWorld.IsClassicPorted"/> does not know (their emitter runs, nothing shows).</summary>
        public readonly List<int> UnportedMakerIds = new List<int>();
    }

    /// <summary>
    /// RSW map effects: the effect part of the world builder FUN_009f9770 and the CEffectMaker objects it leaves in the
    /// world's render-object list. See packages/mapfx/NOTES.md.
    /// </summary>
    public sealed partial class FxWorld
    {
        readonly List<EffectMaker> effectMakers = new List<EffectMaker>();
        Func<uint, int, uint> makerLaunch;

        public int EffectMakerCount => effectMakers.Count;
        public IReadOnlyList<EffectMaker> EffectMakers => effectMakers;

        /// <summary>FUN_00a62d10 on the client's full registry: true when the client hands a record with this id to the new system.</summary>
        public static bool IsClientNewSystemId(int efId) => Array.BinarySearch(ClientNewSystemIds, efId) >= 0;

        /// <summary>
        /// The effect part of the world builder FUN_009f9770 (map load, after the RSW is read), record by record in file
        /// order: an id of the client's new-system registry (<see cref="IsClientNewSystemId"/>) is launched once,
        /// ownerless, at the record's position — <c>FUN_00a4d090(aid 0, id, pos, target 0, −1, 0, 0)</c> — and the record
        /// is otherwise forgotten (no params, no emit speed); every other id gets a <see cref="EffectMaker"/>
        /// (operator new(0x178), ctor FUN_00d378a0, SendMsg 0xe with the record, pushed to the world's render-object
        /// list). No option gates either branch (unlike the sound records' SOUNDMODE). Call it after the map's ground /
        /// player are set up; call <see cref="ClearMapEffects"/> (and <see cref="ClearNewEffects"/>) on the next map change.
        /// </summary>
        public MapEffectsLoadResult LoadMapEffects(IReadOnlyList<RswEffectRecord> records)
        {
            var result = new MapEffectsLoadResult();
            if (records == null) return result;
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                if (IsClientNewSystemId(r.Id))
                {
                    if (IsNewSystem(r.Id))
                    {
                        Rt.LaunchNewEffect_a4d090(0, r.Id, r.X, r.Y, r.Z, 0);
                        result.NewSystemLaunched++;
                    }
                    else result.NewSystemSkippedIds.Add(r.Id);
                    continue;
                }
                AddEffectMaker(r);
                result.Makers++;
                if (!IsClassicPorted(r.Id)) result.UnportedMakerIds.Add(r.Id);
            }
            return result;
        }

        /// <summary>
        /// One CEffectMaker for <paramref name="record"/> (whatever its id — the new-system routing is
        /// <see cref="LoadMapEffects"/>'s): ctor + SendMsg 0xe (consumes one or two rand() calls) + push to the world's
        /// render-object list. Returns the maker's address.
        /// </summary>
        public uint AddEffectMaker(in RswEffectRecord record)
        {
            var M = Rt.M;
            var maker = new EffectMaker(M, M.Alloc(EffectMaker.SizeOf));
            maker.Construct(Rt);
            uint rec = M.Alloc(EffectMaker.RecordSize);   // the client reads the record in C3dWorldRes; a copy here
            WriteRecord(M, rec, record);
            maker.SendMsg(Rt, 0xe, rec);
            M.Free(rec);
            Rt.ListPushBack(Rt.World + 0x8, maker.A);
            effectMakers.Add(maker);
            return maker.A;
        }

        /// <summary>Writes the 0x74-byte RSW record layout (name bytes, position, id, emit speed, params) at <paramref name="at"/>.</summary>
        public static void WriteRecord(FxMemory M, uint at, in RswEffectRecord r)
        {
            string name = r.Name ?? "";
            for (int k = 0; k < 80; k++) M.U8(at + (uint)k, k < name.Length && k < 79 ? (byte)name[k] : (byte)0);
            M.F32(at + 0x50, r.X); M.F32(at + 0x54, r.Y); M.F32(at + 0x58, r.Z);
            M.I32(at + 0x5c, r.Id);
            M.F32(at + 0x60, r.EmitSpeed);
            M.F32(at + 0x64, r.P0); M.F32(at + 0x68, r.P1); M.F32(at + 0x6c, r.P2); M.F32(at + 0x70, r.P3);
        }

        /// <summary>
        /// Map change (the world's teardown deleting its render objects): every maker through vf00(1) = 0xd38170 →
        /// CAbleToMakeEffect dtor (0xbcdc60: effect msg 0x18 to each effect it launched — they lose their owner and
        /// die on their next pass, see <see cref="ReleaseOwner"/>) → free. Effects already running are not removed here
        /// (the client deletes them with the rest of the world; the host's own map-change cleanup covers them).
        /// </summary>
        public void ClearMapEffects()
        {
            foreach (var m in effectMakers) DestroyEffectMaker(m);
            effectMakers.Clear();
        }

        /// <summary>
        /// The world teardown FUN_009faa40 (map exit through FUN_009fa3c0, and the same-map warp FUN_00c0c750 —
        /// ZC_NPCACK_MAPMOVE to the RSW already loaded — which then rebuilds the map effects with FUN_009f9770):
        /// every object of the world's render-object list deleted through vf00(1) in list order (makers first, so
        /// their effects lose the owner, then every CRagEffect and CEffectPrim), then the new-system map change
        /// FUN_00a4db00. Here: <see cref="ClearMapEffects"/>, all classic effects and prims, ground scopes, arrows,
        /// damage numbers, <see cref="ClearNewEffects"/>. Actor shims stay (the host owns them: <see cref="RemoveActor"/>).
        /// </summary>
        public void ClearWorld()
        {
            ClearMapEffects();
            foreach (var p in prims)
            {
                p.m_master_ptr = 0;
                DestroyPrim(p);
            }
            prims.Clear();
            foreach (var e in effects) Rt.M.Free(e.A);
            effects.Clear();
            ClearGroundScopes();
            ClearArrows();
            ClearMsgEffects();
            ClearNewEffects();
        }

        void DestroyEffectMaker(EffectMaker m)
        {
            var M = Rt.M;
            ReleaseOwner(m.A);
            Rt.ListRemove(Rt.World + 0x8, m.A);
            uint head = M.U32(m.A + 0x144);
            if (head != 0) M.Free(head);
            M.Free(m.A);
        }

        /// <summary>
        /// World pass part (FUN_009fa630 walks the render-object list, makers first — they are created at map load):
        /// CEffectMaker::OnProcess for every maker. A launched effect is appended to the world list and gets its first
        /// OnProcess later in the same pass, as in the client.
        /// </summary>
        void ProcessEffectMakers()
        {
            if (effectMakers.Count == 0) return;
            var launch = makerLaunch ?? (makerLaunch = LaunchFromMaker);
            for (int i = 0; i < effectMakers.Count; i++) effectMakers[i].OnProcess(Rt, launch);
        }

        /// <summary>CAbleToMakeEffect::LaunchEffect (0xbd1f40) on a maker: the new effect's address, 0 when the id is not ported.</summary>
        uint LaunchFromMaker(uint maker, int efId)
        {
            var e = Launch(efId, maker);
            return e.HasValue ? e.Value.A : 0u;
        }
    }
}
