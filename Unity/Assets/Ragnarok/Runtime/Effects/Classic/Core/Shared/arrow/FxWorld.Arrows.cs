// Synced from reference/port/src/RoClassicFx/Shared/arrow/FxWorld.Arrows.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Collections.Generic;

namespace Ragnarok.Effects.Classic
{
    /// <summary>
    /// Basic-attack projectiles owned by the world: CArrowEffect objects (Shared/arrow/ArrowEffect.cs). The client
    /// creates them from the attacker's SetAction (FUN_00bfb110: players with a bow, archer monsters, the Doram Soul
    /// Attack) and from AC_SHOWER (FUN_00bfb1c0), links them into the world's render-object list and processes / draws
    /// them with everything else in it. See packages/arrow/NOTES.md for the host contract.
    /// </summary>
    public sealed partial class FxWorld
    {
        readonly List<ArrowEffect> arrows = new List<ArrowEffect>();

        public int ArrowCount => arrows.Count;
        public IReadOnlyList<ArrowEffect> Arrows => arrows;

        /// <summary>
        /// FUN_00bfb110 (thiscall on the game mode (float* src, aid, float release, int class), RET 0x10): an arrow from
        /// (<paramref name="srcX"/>, <paramref name="srcY"/>, <paramref name="srcZ"/>) — the attacker's position, RO world
        /// units; the arrow starts 10 units above it — to the actor <paramref name="targetAid"/> (a registered actor,
        /// <see cref="EnsureActor"/>, or the local player). It stays invisible for <paramref name="releaseTicks"/> 24 ms
        /// ticks of <see cref="FxRuntime.TimeGetTimeImpl"/> after this call (the client passes the attacker's frame delay ×
        /// hit frame, before the +8 / fd arrow addition, and calls this when the attack motion starts), then flies toward
        /// where the target is at that moment: 8 ticks (192 ms) from 40 units on, one 5-unit step per tick closer, and
        /// disappears. <paramref name="spriteClass"/>: 0 for player arrows, the attacker's (render) job for monsters and
        /// for the Doram Soul Attack (it picks the bullet sprite, <see cref="ArrowEffect.SpriteNames"/>).
        /// Returns the arrow (object address), or 0 when the client creates none (target id 0) or the sprite files are
        /// missing (the client would fault).
        /// </summary>
        public uint LaunchArrow(float srcX, float srcY, float srcZ, uint targetAid, float releaseTicks, int spriteClass = 0)
        {
            if (targetAid == 0) return 0;
            ArrowEffect.SpriteNames(spriteClass, out string spr, out string act);
            if (!SpriteFileAvailable(spr) || !SpriteFileAvailable(act)) return 0;
            var arrow = NewArrow();
            arrow.MsgLaunchToActor(Rt, srcX, srcY, srcZ, targetAid, releaseTicks, spriteClass);
            return arrow.A;
        }

        /// <summary>
        /// <see cref="LaunchArrow(float, float, float, uint, float, int)"/> from the current position of the actor
        /// <paramref name="sourceAid"/> (a registered actor or the local player). Returns 0 when the source is unknown.
        /// </summary>
        public uint LaunchArrow(uint sourceAid, uint targetAid, float releaseTicks, int spriteClass = 0)
        {
            uint source;
            if (actorsByAid.TryGetValue(sourceAid, out uint actor)) source = actor;
            else if (sourceAid != 0 && sourceAid == Rt.PlayerAid) source = Rt.Player;
            else return 0;
            var M = Rt.M;
            return LaunchArrow(M.F32(source + 0x10), M.F32(source + 0x14), M.F32(source + 0x18), targetAid, releaseTicks, spriteClass);
        }

        /// <summary>
        /// FUN_00bfb1c0 (thiscall on the game mode (float* src, float* dst, float release), RET 0xc): an arrow from one
        /// point to another (both lifted by 10 units), standard arrow sprite — AC_SHOWER's nine arrows (release 18 ticks).
        /// Returns the arrow, or 0 when the sprite files are missing.
        /// </summary>
        public uint LaunchArrowToPoint(float srcX, float srcY, float srcZ, float dstX, float dstY, float dstZ, float releaseTicks)
        {
            if (!SpriteFileAvailable(ArrowEffect.ArrowSpr) || !SpriteFileAvailable(ArrowEffect.ArrowAct)) return 0;
            var arrow = NewArrow();
            arrow.MsgLaunchToPoint(Rt, srcX, srcY, srcZ, dstX, dstY, dstZ, releaseTicks);
            return arrow.A;
        }

        /// <summary>operator new(0x170) + CArrowEffect ctor (0xd37790), pushed to the world's render-object list (0x783f90).</summary>
        ArrowEffect NewArrow()
        {
            var M = Rt.M;
            var arrow = new ArrowEffect(M, M.Alloc(ArrowEffect.SizeOf));
            arrow.Construct(Rt);
            Rt.ListPushBack(Rt.World + 0x8, arrow.A);
            arrows.Add(arrow);
            return arrow;
        }

        /// <summary>Whether CResMgr would find the file (cached, or readable through <see cref="FxRuntime.LoadDataFile"/>); allocates nothing.</summary>
        bool SpriteFileAvailable(string name)
            => Rt.TryFindResource(name, out _) || Rt.LoadDataFile?.Invoke(FxRuntime.ResourcePath(name)) != null;

        public bool IsArrowAlive(uint handle)
        {
            if (handle == 0) return false;
            for (int i = 0; i < arrows.Count; i++)
                if (arrows[i].A == handle) return true;
            return false;
        }

        /// <summary>Removes every arrow now (host cleanup, e.g. map change).</summary>
        public void ClearArrows()
        {
            foreach (var a in arrows) DestroyArrow(a);
            arrows.Clear();
        }

        /// <summary>World pass part: CArrowEffect::OnProcess (vf01) for each arrow; dead ones are deleted (vf00).</summary>
        void ProcessArrows()
        {
            for (int i = 0; i < arrows.Count; i++)
            {
                if (arrows[i].OnProcess(Rt)) continue;
                DestroyArrow(arrows[i]);
                arrows.RemoveAt(i--);
            }
        }

        void RenderArrows(uint matrix)
        {
            for (int i = 0; i < arrows.Count; i++) arrows[i].Render(Rt, matrix);
        }

        void DestroyArrow(ArrowEffect a)
        {
            Rt.ListRemove(Rt.World + 0x8, a.A);
            a.Destroy(Rt);
        }
    }
}
