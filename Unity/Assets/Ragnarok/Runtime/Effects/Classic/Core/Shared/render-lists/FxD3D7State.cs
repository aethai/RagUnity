// Synced from reference/port/src/RoClassicFx/Shared/render-lists/FxD3D7State.cs by sync-to-unity.ps1 — edit the source, not this copy.
using System;
using System.Collections.Generic;

namespace Ragnarok.Effects.Classic
{
    /// <summary>The device state at one RP draw: everything Flush changes between draws.</summary>
    public struct FxRenderDrawState
    {
        public uint ZEnable, ZWrite, AlphaTest, AlphaRef, AlphaFunc, AlphaBlend, SrcBlend, DestBlend, Fog, Cull, ColorKey;
        /// <summary>Stage 0 D3DTSS_ADDRESS (1 wrap, 3 clamp).</summary>
        public uint Address;
        /// <summary>Stage 0 / 1 colour and alpha ops and arguments, stage 1 texture-coordinate index.</summary>
        public uint ColorOp0, ColorArg1_0, ColorArg2_0, AlphaOp0, AlphaArg1_0, AlphaArg2_0;
        public uint ColorOp1, ColorArg1_1, ColorArg2_1, AlphaOp1, AlphaArg1_1, TexCoord1;
        /// <summary>The surface bound on stage 0 / stage 1.</summary>
        public uint Surface0, Surface1;
    }

    /// <summary>One RP draw in Flush order, with the state it is drawn with.</summary>
    public struct FxRenderDraw
    {
        public int List;
        public int Rp;
        public FxRenderDrawState State;
    }

    /// <summary>
    /// A D3D7 device-state tracker for <see cref="FxRenderLists.Flush"/>: answers GetRenderState, records every draw
    /// with its state (<see cref="Draws"/>), optionally logs every call (<see cref="Log"/>). <see cref="DeviceDefaults"/>
    /// is the state the client sets when it creates the device (FUN_00528b80); Flush leaves the state at its end
    /// for the next frame, so keep one tracker per device across frames.
    /// </summary>
    public sealed class FxD3D7State : IFxD3D7Device
    {
        public readonly uint[] Rs = new uint[256];
        public readonly uint[,] Tss = new uint[8, 32];
        public readonly uint[] Surface = new uint[8];
        public readonly List<FxRenderDraw> Draws = new List<FxRenderDraw>();
        public Action<string> Log;

        /// <summary>FUN_00528b80 (CRenderSystemDX7, after device creation): the defaults the client sets.</summary>
        public static FxD3D7State DeviceDefaults()
        {
            var s = new FxD3D7State();
            s.Rs[D3Drs.CullMode] = 1; s.Rs[D3Drs.ZFunc] = 4; s.Rs[4] = 1 /* TEXTUREPERSPECTIVE */;
            s.Rs[D3Drs.AlphaFunc] = 7; s.Rs[D3Drs.AlphaRef] = 0xf0; s.Rs[D3Drs.ColorKeyEnable] = 1;
            s.Rs[D3Drs.ZEnable] = 1; s.Rs[D3Drs.ZWriteEnable] = 1; s.Rs[D3Drs.SrcBlend] = 5; s.Rs[D3Drs.DestBlend] = 6;
            s.Tss[0, D3Dtss.TexCoordIndex] = 0; s.Tss[0, D3Dtss.ColorOp] = 4; s.Tss[0, D3Dtss.ColorArg1] = 2; s.Tss[0, D3Dtss.ColorArg2] = 0;
            s.Tss[0, D3Dtss.AlphaOp] = 4; s.Tss[0, D3Dtss.AlphaArg1] = 2; s.Tss[0, D3Dtss.AlphaArg2] = 0;
            s.Tss[1, D3Dtss.TexCoordIndex] = 1; s.Tss[1, D3Dtss.ColorArg1] = 2; s.Tss[1, D3Dtss.ColorOp] = 0x12; s.Tss[1, D3Dtss.ColorArg2] = 1;
            s.Tss[1, D3Dtss.AlphaOp] = 1; s.Tss[2, D3Dtss.ColorOp] = 1; s.Tss[2, D3Dtss.AlphaOp] = 1;
            s.Tss[0, D3Dtss.MinFilter] = 2; s.Tss[0, D3Dtss.MagFilter] = 2; s.Tss[1, D3Dtss.MinFilter] = 2; s.Tss[1, D3Dtss.MagFilter] = 2;
            s.Tss[0, D3Dtss.Address] = 3; s.Tss[1, D3Dtss.Address] = 4; s.Tss[1, 15] = 0xff000000 /* BORDERCOLOR */;
            return s;
        }

        // the state of the last draw, rebuilt only after a call that changed a value (Flush sets most states to the
        // value they already have; a snapshot per draw read ~30 array slots for each of ~10 000 draws a frame)
        FxRenderDrawState last;
        bool lastValid;

        public void SetRenderState(int state, uint value) { Log?.Invoke($"RS {state} {value:x}"); if (Rs[state] != value) { Rs[state] = value; lastValid = false; } }

        public uint GetRenderState(int state) { Log?.Invoke($"GRS {state}"); return Rs[state]; }

        public void SetTextureStageState(int stage, int type, uint value) { Log?.Invoke($"TSS {stage} {type} {value:x}"); if (Tss[stage, type] != value) { Tss[stage, type] = value; lastValid = false; } }

        public void SetTexture(int stage, uint surface) { Log?.Invoke($"TEX {stage} {surface:x}"); if (Surface[stage] != surface) { Surface[stage] = surface; lastValid = false; } }

        public void Draw(int list, int rp, in FxRenderPrimitive p)
        {
            if (Log != null)
            {
                if (p.IndexCount != 0) Log($"DIP {p.PrimitiveType} 1c4 {p.Vertices:x} {p.VertexCount} {p.Indices:x} {p.IndexCount}");
                else Log($"DP {p.PrimitiveType} 1c4 {p.Vertices:x} {p.VertexCount}");
            }
            if (!lastValid) { last = Snapshot(); lastValid = true; }
            Draws.Add(new FxRenderDraw { List = list, Rp = rp, State = last });
        }

        public FxRenderDrawState Snapshot() => new FxRenderDrawState
        {
            ZEnable = Rs[D3Drs.ZEnable], ZWrite = Rs[D3Drs.ZWriteEnable], AlphaTest = Rs[D3Drs.AlphaTestEnable],
            AlphaRef = Rs[D3Drs.AlphaRef], AlphaFunc = Rs[D3Drs.AlphaFunc], AlphaBlend = Rs[D3Drs.AlphaBlendEnable],
            SrcBlend = Rs[D3Drs.SrcBlend], DestBlend = Rs[D3Drs.DestBlend], Fog = Rs[D3Drs.FogEnable], Cull = Rs[D3Drs.CullMode],
            ColorKey = Rs[D3Drs.ColorKeyEnable], Address = Tss[0, D3Dtss.Address],
            ColorOp0 = Tss[0, D3Dtss.ColorOp], ColorArg1_0 = Tss[0, D3Dtss.ColorArg1], ColorArg2_0 = Tss[0, D3Dtss.ColorArg2],
            AlphaOp0 = Tss[0, D3Dtss.AlphaOp], AlphaArg1_0 = Tss[0, D3Dtss.AlphaArg1], AlphaArg2_0 = Tss[0, D3Dtss.AlphaArg2],
            ColorOp1 = Tss[1, D3Dtss.ColorOp], ColorArg1_1 = Tss[1, D3Dtss.ColorArg1], ColorArg2_1 = Tss[1, D3Dtss.ColorArg2],
            AlphaOp1 = Tss[1, D3Dtss.AlphaOp], AlphaArg1_1 = Tss[1, D3Dtss.AlphaArg1], TexCoord1 = Tss[1, D3Dtss.TexCoordIndex],
            Surface0 = Surface[0], Surface1 = Surface[1],
        };
    }
}
