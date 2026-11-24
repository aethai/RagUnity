// Synced from reference/port/src/RoClassicFx/FxDispatchAttributes.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Outcome of one CRagEffect::OnProcess switch case (0xb527f0 / vf14 / vf15).
    /// Continue = fall through to the shared epilogue (m_stateCnt++, alive while m_stateCnt &lt; duration);
    /// Dead = return false immediately; AliveNoIncrement = return true without the epilogue.
    /// </summary>
    public enum FxCaseResult { Continue, Dead, AliveNoIncrement }

    /// <summary>
    /// Marks a RagEffect method as the port of the OnProcess case(s) for these effect ids.
    /// Signature: <c>public FxCaseResult Name(FxRuntime rt)</c>. Collected by scripts/gen_dispatch.js.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class FxEffectProcessAttribute : Attribute
    {
        public readonly int[] Ids;
        public FxEffectProcessAttribute(params int[] ids) { Ids = ids; }
    }

    /// <summary>
    /// Marks a Prim method as the port of CEffectPrim::OnProcess's main-switch case(s) (0xad5a30).
    /// Signature: <c>public FxCaseResult Name(FxRuntime rt)</c>; Continue = shared epilogue (age++, alive while age &lt;= duration).
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class FxPrimProcessAttribute : Attribute
    {
        public readonly int[] Types;
        public FxPrimProcessAttribute(params int[] types) { Types = types; }
    }

    /// <summary>
    /// Marks a Prim method as the port of CEffectPrim::Render's switch case(s) (0xaf4ce0).
    /// Signature: <c>public void Name(FxRuntime rt, uint viewMatrix)</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class FxPrimRenderAttribute : Attribute
    {
        public readonly int[] Types;
        public FxPrimRenderAttribute(params int[] types) { Types = types; }
    }
}
