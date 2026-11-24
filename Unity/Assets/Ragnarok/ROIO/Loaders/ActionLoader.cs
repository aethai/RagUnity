using ROIO.Models.FileTypes;
using ROIO.Utils;
using System;
using System.Linq;
using UnityEngine;

namespace ROIO.Loaders
{
    public class ActionLoader
    {
        public static ACT Load(MemoryStreamReader data)
        {
            string header = RoLoaderValidation.ReadBinaryString(data, 2, "ActionLoader.Load header");
            if (!header.Equals(ACT.Header))
            {
                throw new Exception("ActionLoader.Load: Header \"" + header + "\" is not \"AC\"");
            }

            RoLoaderValidation.RequireRemaining(data, 2, "ActionLoader.Load version");
            RoFileVersion version = RoFileVersion.ReadMinorMajor(data);

            ACT act = new ACT();
            act.version = version.Text;

            ReadActions(act, data, version);

            if (version.IsAtLeast(2, 1))
            {
                //sounds
                var count = RoLoaderValidation.RequireCount(RoLoaderValidation.ReadInt(data, "ActionLoader.Load sound count"), RoLoaderValidation.MaxCollectionCount, "sound", "ActionLoader.Load");
                RoLoaderValidation.RequireRemaining(data, (long) count * 40, "ActionLoader.Load sound names");
                act.sounds = new string[count];

                for (int i = 0; i < count; i++)
                {
                    act.sounds[i] = data.ReadBinaryString(40);
                }

                //delay
                if (version.IsAtLeast(2, 2))
                {
                    for (int i = 0; i < act.actions.Length; i++)
                    {
                        // Ragnarok Research Lab measurements put the client sprite
                        // animation interval at 24 ms. roBrowser uses 25 ms, but that
                        // is a community approximation rather than the observed client
                        // timing.
                        act.actions[i].delay = RoLoaderValidation.ReadFloat(data, $"ActionLoader.Load action delay {i}/{act.actions.Length}") * 24;
                    }
                }
            }

            return act;
        }

        public static bool TryLoad(MemoryStreamReader data, out ACT act, out string error)
        {
            try
            {
                act = Load(data);
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                act = null;
                error = ex.Message;
                return false;
            }
        }

        private static void ReadActions(ACT act, MemoryStreamReader data, RoFileVersion version)
        {
            var count = RoLoaderValidation.RequireCount(RoLoaderValidation.ReadUShort(data, "ActionLoader.ReadActions action count"), RoLoaderValidation.MaxCollectionCount, "action", "ActionLoader.ReadActions");
            RoLoaderValidation.RequireRemaining(data, 10, "ActionLoader.ReadActions reserved action data");
            data.Seek(10, System.IO.SeekOrigin.Current);

            act.actions = new ACT.Action[count];
            for (int i = 0; i < count; i++)
            {
                act.actions[i] = new ACT.Action()
                {
                    frames = ReadMotions(act, data, version),
                    delay = 150f
                };
            }
        }

        private static ACT.Frame[] ReadMotions(ACT act, MemoryStreamReader data, RoFileVersion version)
        {
            var count = RoLoaderValidation.RequireCount(RoLoaderValidation.ReadUInt(data, "ActionLoader.ReadMotions motion count"), RoLoaderValidation.MaxCollectionCount, "motion", "ActionLoader.ReadMotions");
            var motions = new ACT.Frame[count];

            for (int i = 0; i < count; i++)
            {
                RoLoaderValidation.RequireRemaining(data, 32, $"ActionLoader.ReadMotions reserved motion data {i}/{count}");
                data.Seek(32, System.IO.SeekOrigin.Current);
                motions[i] = ReadLayers(act, data, version);
            }

            return motions;
        }

        private static ACT.Frame ReadLayers(ACT act, MemoryStreamReader data, RoFileVersion version)
        {
            var count = RoLoaderValidation.RequireCount(RoLoaderValidation.ReadUInt(data, "ActionLoader.ReadLayers layer count"), RoLoaderValidation.MaxCollectionCount, "layer", "ActionLoader.ReadLayers");
            // one size check per layer, then raw reads (per-field checks built a context string for every field)
            bool extended = version.IsGreaterThan(2, 0);
            bool scaleY = extended && !version.IsLessThanOrEqual(2, 3);
            bool size = extended && version.IsAtLeast(2, 5);
            int layerBytes = 16 + (extended ? 4 + 4 + (scaleY ? 4 : 0) + 8 : 0) + (size ? 8 : 0);
            var layers = new ACT.Layer[count];
            int kept = 0;
            for (int i = 0; i < count; i++)
            {
                RoLoaderValidation.RequireRemaining(data, layerBytes, "ActionLoader.ReadLayers layer");
                int x = data.ReadInt();
                int y = data.ReadInt();
                var layer = new ACT.Layer()
                {
                    pos = new Vector2Int(x, y),
                    index = data.ReadInt(),
                    isMirror = data.ReadInt() != 0,
                    scale = Vector2.one,
                    color = Color.white
                };

                // RoRebuild checks if only if it's greater
                if (extended)
                {
                    float r = data.ReadByte() / 255f, g = data.ReadByte() / 255f, bl = data.ReadByte() / 255f, al = data.ReadByte() / 255f;
                    layer.color = new Color(r, g, bl, al);
                    float sx = data.ReadFloat();
                    layer.scale = new Vector2(sx, scaleY ? data.ReadFloat() : sx);
                    layer.angle = data.ReadInt();
                    layer.sprType = data.ReadInt();

                    if (size)
                    {
                        layer.width = data.ReadInt();
                        // the v2.5 size fields are informational and nothing reads them; files carry garbage there
                        // (overlay's female head 54_여.act has a -46x0 layer), so a bad value must not drop the ACT
                        layer.height = data.ReadInt();
                    }
                }

                layers[i] = layer;
                if (layer.index >= 0) kept++;
            }

            var soundId = version.IsAtLeast(2, 0) ? RoLoaderValidation.ReadInt(data, "ActionLoader.ReadLayers sound id") : -1;
            Vector2Int[] pos = null;

            if (version.IsAtLeast(2, 3))
            {
                int posCount = RoLoaderValidation.RequireCount(RoLoaderValidation.ReadInt(data, "ActionLoader.ReadLayers attachment position count"), RoLoaderValidation.MaxCollectionCount, "attachment position", "ActionLoader.ReadLayers");
                pos = new Vector2Int[posCount];
                for (int i = 0; i < pos.Length; i++)
                {
                    RoLoaderValidation.RequireRemaining(data, 12, $"ActionLoader.ReadLayers attachment position {i}/{pos.Length}");
                    data.Seek(4, System.IO.SeekOrigin.Current);
                    pos[i] = new Vector2Int(data.ReadInt(), data.ReadInt());
                    data.Seek(4, System.IO.SeekOrigin.Current);
                }
            }

            // layers without a sprite (index < 0) are dropped
            var drawn = kept == layers.Length ? layers : new ACT.Layer[kept];
            if (drawn != layers)
            {
                for (int i = 0, k = 0; i < layers.Length; i++)
                {
                    if (layers[i].index >= 0) drawn[k++] = layers[i];
                }
            }

            return new ACT.Frame()
            {
                layers = drawn,
                soundId = soundId,
                pos = pos
            };
        }
    }
}
