// Synced from reference/port/src/RoClassicFx/Shared/castvis/FxWorld.GroundScope.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Collections.Generic;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Cast-time ground visuals owned by the world: CGroundScope objects (ground-target cast circles) plus the
    /// per-frame cell overlays (mouse-cursor grid, Custom /range). See packages/cast-visuals/CAST_VISUALS.md.
    /// </summary>
    public sealed partial class FxWorld
    {
        readonly List<GroundScope> groundScopes = new List<GroundScope>();
        bool groundGeometryFromHost;

        public int GroundScopeCount => groundScopes.Count;
        public IReadOnlyList<GroundScope> GroundScopes => groundScopes;

        /// <summary>
        /// Installs the C3dGround shim at world +0x28 that the cell quads (FUN_009f1880) read: +0x4 the GAT object
        /// (world +0x30, see <see cref="SetGround"/>), +0x8/+0xc the GND width/height in GND cells, +0x10 the GND
        /// cell size. Optional: without it the GND size is taken as half the GAT size and the cell size as 10,
        /// which is what every RO map uses. The x/z of a cell come from the GND grid ((gx − W/2)·size), the
        /// heights from the GAT, exactly as in the client.
        /// </summary>
        public void SetGroundGeometry(int gndWidth, int gndHeight, float cellSize = 10f)
        {
            groundGeometryFromHost = true;
            WriteGroundObject(gndWidth, gndHeight, cellSize);
        }

        uint EnsureGroundObject()
        {
            var M = Rt.M;
            uint attr = M.U32(Rt.World + 0x30);
            uint ground = M.U32(Rt.World + 0x28);
            if (ground != 0 && M.U32(ground + 4) == attr) return ground;
            if (ground != 0 && groundGeometryFromHost)
            {
                M.U32(ground + 4, attr);                 // the host replaced the GAT (map change)
                return ground;
            }
            if (attr == 0) return ground;
            return WriteGroundObject(M.I32(attr + 0x110) / 2, M.I32(attr + 0x114) / 2, 10f);
        }

        uint WriteGroundObject(int gndWidth, int gndHeight, float cellSize)
        {
            var M = Rt.M;
            uint ground = M.U32(Rt.World + 0x28);
            if (ground == 0)
            {
                ground = M.Alloc(FxRuntime.GroundObjectSize);
                M.U32(Rt.World + 0x28, ground);
            }
            M.U32(ground + 4, M.U32(Rt.World + 0x30));
            M.I32(ground + 8, gndWidth);
            M.I32(ground + 0xc, gndHeight);
            M.F32(ground + 0x10, cellSize);
            return ground;
        }

        /// <summary>
        /// ZC_USESKILL_ACK (0x13e / 0x7fb / 0xb1a) with target AID 0 — the ground branch of FUN_00c7f900: a new
        /// CGroundScope at cell (<paramref name="cellX"/>, <paramref name="cellY"/>) that lives until
        /// timeGetTime() + <paramref name="delayMs"/> (<see cref="FxRuntime.TimeGetTimeImpl"/>), sized and textured by
        /// <paramref name="skillId"/>. Returns its handle (object address), or 0 when the client creates none:
        /// delaytime 0 (FUN_00c7f710 returns before any cast visual). It is NOT removed by ZC_DISPEL or when the
        /// cast fails — only its end time (or <see cref="EndGroundScope"/> for host cleanup) ends it.
        /// </summary>
        public uint LaunchGroundScope(int cellX, int cellY, int skillId, int delayMs, uint casterAid = 0)
        {
            if (delayMs == 0) return 0;
            EnsureGroundObject();
            var M = Rt.M;
            var scope = new GroundScope(M, M.Alloc(GroundScope.SizeOf));   // operator new(0x178, nothrow)
            scope.Construct(Rt);
            scope.SendMsg(Rt, GroundScope.MsgCasterAid, (int)casterAid);
            scope.SendMsg(Rt, GroundScope.MsgPosition, (short)cellX, (short)cellY);   // movsx of the packet words
            scope.SendMsg(Rt, GroundScope.MsgEndTime, (int)(Rt.TimeGetTimeImpl() + (uint)delayMs));
            scope.SendMsg(Rt, GroundScope.MsgSkill, (ushort)skillId);
            Rt.ListPushBack(Rt.World + 0x8, scope.A);
            groundScopes.Add(scope);
            return scope.A;
        }

        public bool IsGroundScopeAlive(uint handle)
        {
            if (handle == 0) return false;
            for (int i = 0; i < groundScopes.Count; i++)
                if (groundScopes[i].A == handle) return true;
            return false;
        }

        /// <summary>Removes a ground scope now (host cleanup, e.g. map change). The client itself never does this.</summary>
        public bool EndGroundScope(uint handle)
        {
            for (int i = 0; i < groundScopes.Count; i++)
            {
                if (groundScopes[i].A != handle) continue;
                DestroyGroundScope(groundScopes[i]);
                groundScopes.RemoveAt(i);
                return true;
            }
            return false;
        }

        public void ClearGroundScopes()
        {
            foreach (var s in groundScopes) DestroyGroundScope(s);
            groundScopes.Clear();
        }

        /// <summary>World pass part: CGroundScope::OnProcess (vf01) for each scope; dead ones are deleted (vf00).</summary>
        void ProcessGroundScopes()
        {
            for (int i = 0; i < groundScopes.Count; i++)
            {
                if (groundScopes[i].OnProcess(Rt)) continue;
                DestroyGroundScope(groundScopes[i]);
                groundScopes.RemoveAt(i--);
            }
        }

        void RenderGroundScopes(uint matrix)
        {
            if (groundScopes.Count == 0) return;
            EnsureGroundObject();
            if (Rt.M.U32(Rt.World + 0x28) == 0) return;   // no map installed: the client always has a ground
            for (int i = 0; i < groundScopes.Count; i++) groundScopes[i].Render(Rt, matrix);
        }

        /// <summary>vf00(1) = 0x00d38260 → CAbleToMakeEffect dtor (0x00bcdc60; its effect list is empty) + free.</summary>
        void DestroyGroundScope(GroundScope s)
        {
            var M = Rt.M;
            Rt.ListRemove(Rt.World + 0x8, s.A);
            uint head = M.U32(s.A + 0x144);
            if (head != 0) M.Free(head);
            M.Free(s.A);
        }

        /// <summary>
        /// The mouse-cursor cell highlight (C3dGround::vf14 0x009f24c0, grid.tga), for hosts that draw it through
        /// the engine: call once per frame after <see cref="Render"/> with the colour from
        /// <see cref="FxRuntime.CursorGridColor"/> when it is not hidden and the grid option is on.
        /// </summary>
        public void RenderCursorCell(int cellX, int cellY, uint color)
        {
            uint ground = EnsureGroundObject();
            if (ground == 0) return;
            Rt.RenderCursorGrid_9f24c0(ground, Rt.View + 0x98, cellX, cellY, color);
        }

        /// <summary>
        /// The Custom /range overlay (skill range around the local player while a skill is armed); the player's
        /// position is world +0x2c (<see cref="FxRuntime.SetPlayerPosition"/>). Call once per frame after <see cref="Render"/>.
        /// </summary>
        public bool RenderSkillRange(bool rangeOption, int skillUseState, int range)
        {
            EnsureGroundObject();
            return Rt.RenderSkillRange_233a9e0(rangeOption, skillUseState, range);
        }
    }
}
