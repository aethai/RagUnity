// Synced from reference/port/src/RoClassicFx/Rag/effects-4/RagEffect.SightBlaster.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    public readonly partial struct RagEffect
    {
        /// <summary>
        /// Case 601 (0x259, vf15 inline) — Sight Blaster: follows the owner, screen anchor via FUN_00b0dc10; on
        /// even ticks one Sight sprite (type 5) on a circle of radius 15 at angle -5·t (turning around the owner).
        /// </summary>
        [FxEffectProcess(0x259)]
        public FxCaseResult Case_SightBlaster(FxRuntime rt)
        {
            FollowMaster();
            ProjectMasterToF154(rt);
            if ((m_stateCnt & 1) != 0) return FxCaseResult.Continue;
            int ang = FxOps.Cvtt((float)m_stateCnt * -5.0f);
            float s = FxMath.GetSin(ang);
            float c = FxMath.GetCos(ang);
            Prim p = LaunchLast(rt, 5);
            p.m_duration = 0x14;
            p = m_lastPrim;
            p.f_394 = s * 15.0f;
            p.f_398 = -20.0f;
            p.f_39c = c * -15.0f;
            p.f_3b0 = 0.1f;
            p = m_lastPrim; p.f_1e8 = 3;
            p = m_lastPrim; p.f_318 = 2.5f;
            p = m_lastPrim; p.f_31c = -0.1f;
            p = m_lastPrim; p.f_324 = 50.0f;
            p = m_lastPrim; p.f_328 = -3.0f;
            rt.SetSprName(m_lastPrim.A, "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\Sight.spr");
            rt.SetActName(m_lastPrim.A, "\u00c0\u00cc\u00c6\u00d1\u00c6\u00ae\\Sight.act");
            m_lastPrim.SnapshotTrail_ad58d0();
            return FxCaseResult.Continue;
        }
    }
}
