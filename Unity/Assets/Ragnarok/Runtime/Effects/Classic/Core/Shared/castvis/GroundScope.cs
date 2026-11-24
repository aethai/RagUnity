// Synced from reference/port/src/RoClassicFx/Shared/castvis/GroundScope.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// CGroundScope (vtable 0x0100a764, 0x178 bytes; CAbleToMakeEffect &gt; CRenderObject &gt; CGameObject) — the
    /// rotating magic circle the client lays on the ground while a place skill (Storm Gust, Meteor Storm, LoV,
    /// Safety Wall, …) is being cast. Not a CRagEffect id: created by the ZC_USESKILL_ACK handler
    /// (FUN_00c7f900 @0x00c7f963, ground target = packet target AID 0, only when delaytime &gt; 0) and pushed
    /// into the world's render-object list (world +0x8), which processes (vf01) and renders (vf03) it.
    ///
    /// Fields: +0x158 caster AID (msg 0x16), +0x15c/+0x160 centre cell x/y (msg 0xe), +0x164 end time
    /// (timeGetTime() + delaytime, msg 0x73), +0x168 cells per side n = FUN_00d38400(skid) + 2 (msg 0x75),
    /// +0x16c rotation angle (radians), +0x170 angle step 0.5 / n, +0x174 skill id.
    /// See packages/cast-visuals/CAST_VISUALS.md.
    /// </summary>
    public readonly partial struct GroundScope
    {
        public const uint SizeOf = 0x178;
        public const uint VtCGroundScope = 0x0100a764;
        public const uint VtCAbleToMakeEffect = 0x00ffdf40;

        public const int MsgPosition = 0xe, MsgCasterAid = 0x16, MsgEndTime = 0x73, MsgSkill = 0x75;

        public readonly FxMemory M;
        public readonly uint A;

        public GroundScope(FxMemory m, uint address) { M = m; A = address; }

        public uint CasterAid { get => M.U32(A + 0x158); set => M.U32(A + 0x158, value); }
        public int CellX { get => M.I32(A + 0x15c); set => M.I32(A + 0x15c, value); }
        public int CellY { get => M.I32(A + 0x160); set => M.I32(A + 0x160, value); }
        /// <summary>timeGetTime() value up to which the scope is alive (unsigned compare, inclusive).</summary>
        public uint EndTime { get => M.U32(A + 0x164); set => M.U32(A + 0x164, value); }
        /// <summary>Cells per side of the drawn square (FUN_00d38400(skid) + 2); 0 draws nothing.</summary>
        public int Cells { get => M.I32(A + 0x168); set => M.I32(A + 0x168, value); }
        public float Angle { get => M.F32(A + 0x16c); set => M.F32(A + 0x16c, value); }
        public float AngleStep { get => M.F32(A + 0x170); set => M.F32(A + 0x170, value); }
        public int SkillId { get => M.I32(A + 0x174); set => M.I32(A + 0x174, value); }

        /// <summary>
        /// CGroundScope::CGroundScope (0x00d37a40) with the inlined CAbleToMakeEffect ctor (0x00bcd310):
        /// CRenderObject ctor (shim), an empty effect std::list at +0x144 (0xc sentinel), then the scope defaults.
        /// </summary>
        public void Construct(FxRuntime rt)
        {
            rt.ConstructRenderObject(A);                 // 0x00bcd750
            M.U32(A, VtCAbleToMakeEffect);
            M.U32(A + 0x144, 0);
            M.U32(A + 0x148, 0);
            uint head = M.Alloc(0xc);                    // operator new(0xc): list sentinel
            M.U32(head, head);
            M.U32(head + 4, head);
            M.U32(A + 0x144, head);
            M.U32(A + 0x138, 0xffffffff);
            M.U32(A + 0x13c, 0);
            M.U32(A + 0x140, 0);
            M.U32(A + 0x14c, 0);
            M.U32(A + 0x150, 0);
            M.U8(A + 0x130, 0);
            // CGroundScope part
            M.U32(A, VtCGroundScope);
            M.U32(A + 0x164, 0);
            M.I32(A + 0x168, 1);
            M.U32(A + 0x16c, 0);
            M.I32(A + 0x174, -1);
        }

        /// <summary>
        /// CGroundScope::SendMsg (vf02, 0x00d3e270) for the four messages the client sends it (message high
        /// dword 0): 0xe position (p1 = cell x, p2 = cell y), 0x16 caster AID, 0x73 end time, 0x75 skill id
        /// (size via FUN_00d38400). Any other message goes to CAbleToMakeEffect::SendMsg (0x00bd7fc0) in the
        /// client, which is not ported: returns false and changes nothing.
        /// </summary>
        public bool SendMsg(FxRuntime rt, int msg, int p1, int p2 = 0)
        {
            switch (msg)
            {
                case MsgPosition:
                    M.I32(A + 0x15c, p1);
                    M.I32(A + 0x160, p2);
                    return true;
                case MsgCasterAid:
                    M.I32(A + 0x158, p1);
                    return true;
                case MsgEndTime:
                    M.I32(A + 0x164, p1);
                    return true;
                case MsgSkill:
                {
                    int n = rt.GroundScopeSize_d38400(p1) + 2;
                    M.I32(A + 0x168, n);
                    M.I32(A + 0x174, p1);
                    // movd/cvtdq2ps; divss 0.5 / n
                    M.F32(A + 0x170, n != 0 ? 0.5f / (float)n : 0f);
                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary>CGroundScope::OnProcess (vf01, 0x00d39880): alive while EndTime &gt;= timeGetTime() (unsigned).</summary>
        public bool OnProcess(FxRuntime rt) => M.U32(A + 0x164) >= rt.TimeGetTimeImpl();

        /// <summary>
        /// CGroundScope::Render (vf03, 0x00d3d4f0). Lays an n×n block of GAT cells centred on (CellX, CellY)
        /// (first cell CellX - n/2, C division) and maps one texture across the whole block, rotated about
        /// the block centre by <see cref="Angle"/>: cell (i, j) gets the UVs of its four corners
        /// ((i|i+1) − n/2, (j|j+1) − n/2) rotated by (sin, cos) and shifted by n/2, divided by n. Each cell is
        /// drawn by FUN_009f2510 (terrain-hugging GAT-cell quad, SRCALPHA/INVSRCALPHA, AddQuad flags 1).
        /// Vertex colour: white, alpha = 0x80 + (uint)(40·sin(angle)) wrapped to a byte (__ftoui3: 0x7f once
        /// 40·sin ≤ −1). Texture by skill: 0x7f0 poison-smoke circle (/mineffect variant), 0x8a5 comet rune,
        /// 0x149e competentia rune (alpha × 0.6), else effect\magic_target.tga. Afterwards, on frames that ran
        /// at least one logic tick, Angle += AngleStep.
        /// </summary>
        public void Render(FxRuntime rt, uint viewMatrix)
        {
            int cx = M.I32(A + 0x15c);
            int cy = M.I32(A + 0x160);
            uint world = M.U32(rt.Mode + 0xcc);          // FUN_00a03ba0() twice
            uint ground = M.U32(world + 0x28);
            int n = M.I32(A + 0x168);
            float half = (float)n * 0.5f;
            int x0 = cx - n / 2;
            int y0 = cy - n / 2;
            float angle = M.F32(A + 0x16c);
            float s = (float)Math.Sin((double)angle);    // FUN_00534e60: fld/fsin/fstp dword
            float c = (float)Math.Cos((double)angle);    // FUN_00534e50: fld/fcos/fstp dword
            uint pulse = FxOps.FloatToUInt_d48d60(s * 40.0f);
            uint color = (pulse << 24) - 0x7f000001u;

            Span<float> uv = stackalloc float[8];
            // GetTexture is idempotent per name: resolve the (literal) name once per scope instead of once per cell
            string resolvedName = null;
            uint resolvedTex = 0;
            for (int j = 0; j < M.I32(A + 0x168); j++)
            {
                float a0 = (float)j - half;
                float a1 = (float)(j + 1) - half;
                float a0c = a0 * c, a0s = a0 * s;
                float a1c = a1 * c, a1s = a1 * s;
                for (int i = 0; i < M.I32(A + 0x168); i++)
                {
                    float p0 = (float)i - half;
                    float p1 = (float)(i + 1) - half;
                    float p0c = p0 * c, p0s = p0 * s;
                    float p1c = p1 * c, p1s = p1 * s;
                    float nf = (float)M.I32(A + 0x168);
                    uv[0] = ((a0s + p0c) + half) / nf;
                    uv[1] = ((a0c - p0s) + half) / nf;
                    uv[2] = ((p1c + a0s) + half) / nf;
                    uv[3] = ((a0c - p1s) + half) / nf;
                    uv[4] = ((a1s + p0c) + half) / nf;
                    uv[5] = ((a1c - p0s) + half) / nf;
                    uv[6] = ((a1s + p1c) + half) / nf;
                    uv[7] = ((a1c - p1s) + half) / nf;

                    uint cellColor = color;
                    string name;
                    switch (M.I32(A + 0x174))
                    {
                        case 0x7f0:
                            name = rt.MinEffect
                                ? "effect\\mineffect\\new_poisonsmoke\\new_poisonsmoke_cast\\poi_magic_circle_1.tga"
                                : "effect\\new_poisonsmoke\\new_poisonsmoke_cast\\poi_magic_circle_1.tga";
                            break;
                        case 0x8a5:
                            name = "effect\\new_comet\\new_comet_cast_magicrune\\new_comet_cast_magicrune.tga";
                            break;
                        case 0x149e:
                        {
                            float alpha = (float)(double)(color >> 24) * 0.6f;
                            cellColor = ((uint)(byte)FxOps.Cvtt(alpha) << 24) | 0xffffffu;
                            name = "effect\\competentia\\competentia_cast_bottom\\com_rune_01.tga";
                            break;
                        }
                        default:
                            name = "effect\\magic_target.tga";
                            break;
                    }
                    if (!ReferenceEquals(name, resolvedName))
                    {
                        resolvedTex = rt.GetTexture(name);
                        resolvedName = name;
                    }
                    uint tex = resolvedTex;
                    rt.RenderGroundCell_9f2510(ground, viewMatrix, x0 + i, y0 + j, cellColor, tex, uv);
                }
            }
            if (rt.TicksThisFrame != 0)
                M.F32(A + 0x16c, M.F32(A + 0x170) + M.F32(A + 0x16c));
        }
    }
}
