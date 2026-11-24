// Synced from reference/port/src/RoClassicFx/Shared/mapfx/FxWorld.Emitter.cs by sync-to-unity.ps1 — edit the source, not this copy.
namespace Ragnarok.Effects.Classic
{
    /// <summary>The map emitters (EF 974, CEmitterEffect) as the map's EffectTool script configures them. Package mapfx.</summary>
    public sealed partial class FxWorld
    {
        /// <summary>SetEmitterInfo message ids (effecttool\effectmsgid: MSG_SET_*) that CEmitterEffect understands.</summary>
        public static class EmitterMsg
        {
            public const int Init = 0, SpawnDir1 = 1, SpawnDir2 = 2, Gravity = 3, Pos = 4, EmitRadius = 5, Color = 6,
                EmitRate = 7, Size = 8, LifeTime = 9, Texture = 10, Speed = 11, SrcMode = 12, DestMode = 13,
                MaxParticle = 14, ZEnable = 43, BillboardOff = 45, RotateAngle = 46, Eternity = 51, Scale = 52;
        }

        /// <summary>Map emitters (EF 974) created so far on this map, in creation order (= RSW record order) — the indices of <see cref="SetEmitterInfo(int, int, double[])"/>.</summary>
        public int MapEmitterCount => Rt.EffectCountById(FxRuntime.EmitterEffectId);

        /// <summary>
        /// The Lua C function <c>SetEmitterInfo(index, msg, values…)</c> (FUN_00aa7640) as the EffectTool's
        /// <c>ReadEmitterInfoz</c> calls it: <paramref name="args"/> are the numeric values after the message, exactly as
        /// lua_tonumber returns them (nil = 0; missing ones are 0). Writes the index-th 974 effect of the map (creation order =
        /// RSW record order, counting from 0); ReadEmitterInfoz passes the emitterInfo table key as is. Returns false when
        /// there is no 974 effect with that index (the call does nothing). ReadEmitterInfoz sends, per entry (pairs order):
        /// MAXPARTICLE, SPAWNDIR1, SPAWNDIR2, GRAVITY, EMITRADIUS, COLOR, EMITRATE, SIZE, LIFETIME, TEXTURE, SPEED, SRCMODE,
        /// DESTMODE, ZENABLE, then BILLBOARD_OFF / ROTATE_ANGLE / ETERNITY / SCALE when present, and INIT last. MAXPARTICLE
        /// (when the count changes) and INIT re-initialise particles and draw rand(): keep the client's call order.
        /// An emitter that has retired (only an owned one or one with a lifetime can) is skipped and false returned: the
        /// client writes into its graveyard copy harmlessly, but FxWorld destroys retired effects after every tick.
        /// </summary>
        public bool SetEmitterInfo(int index, int msg, params double[] args)
        {
            if (!EmitterUsable(index)) return false;
            var stack = new double[2 + (args?.Length ?? 0)];
            stack[0] = index;
            stack[1] = msg;
            if (args != null) args.CopyTo(stack, 2);
            return Rt.SetEmitterInfo_aa7640(stack);
        }

        /// <summary>
        /// <see cref="SetEmitterInfo(int, int, double[])"/> with a string third argument (MSG_SET_TEXTURE: the texture name,
        /// a byte string, e.g. "smoke2.bmp"; looked up through GetTexture as given).
        /// </summary>
        public bool SetEmitterInfo(int index, int msg, string text)
            => EmitterUsable(index) && Rt.SetEmitterInfo_aa7640(new double[] { index, msg }, text);

        bool EmitterUsable(int index)
        {
            uint e = Rt.EffectByIdIndex_a4de70(FxRuntime.EmitterEffectId, (uint)index);
            return e != 0 && Rt.IsNewEffectLive(e);
        }
    }
}
