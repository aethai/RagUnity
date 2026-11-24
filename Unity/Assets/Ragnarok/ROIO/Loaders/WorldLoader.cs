using ROIO.Models.FileTypes;
using ROIO.Utils;
using System;
using System.Collections.Generic;

namespace ROIO.Loaders {
    /// <summary>
    /// Loaders for .rsw file
    /// 
    /// @author Guilherme Hernandez
    /// Based on ROBrowser by Vincent Thibault (robrowser.com)
    /// </summary>
    public class WorldLoader {
        public static RSW Load(MemoryStreamReader data, string file) {
            //read header
            string header = RoLoaderValidation.ReadBinaryString(data, 4, $"WorldLoader.Load header for {file}");
            RoLoaderValidation.RequireRemaining(data, 2, $"WorldLoader.Load version for {file}");
            RoFileVersion version = RoFileVersion.ReadMajorMinor(data);
            // C3dWorldRes::Load (0x6db4d0): the build number (int) from 2.5, one unknown byte from 2.2 (2.2 - 2.4 have only the byte)
            int buildNumber = version.IsAtLeast(2, 5) ? RoLoaderValidation.ReadInt(data, $"WorldLoader.Load build number for {file}") : 0;
            if (version.IsAtLeast(2, 2)) {
                RoLoaderValidation.ReadByte(data, $"WorldLoader.Load build type for {file}");
            }

            //check for valid .rsw file
            if (!string.Equals(header, RSW.Header)) {
                throw new Exception($"WorldLoader.Load: Header ({header}) is not \"GRSW\" at file {file}");
            }

            RSW rsw = new RSW(version.Text);

            //read sub files
            rsw.files.ini = RoLoaderValidation.ReadBinaryString(data, 40, $"WorldLoader.Load ini file for {file}");
            rsw.files.gnd = RoLoaderValidation.ReadBinaryString(data, 40, $"WorldLoader.Load gnd file for {file}");
            rsw.files.gat = RoLoaderValidation.ReadBinaryString(data, 40, $"WorldLoader.Load gat file for {file}");

            if (version.IsAtLeast(1, 4)) {
                rsw.files.src = RoLoaderValidation.ReadBinaryString(data, 40, $"WorldLoader.Load src file for {file}");
            }

            // RSW 2.6 moved water data out of this legacy water block.
            if (version.IsAtLeast(1, 3) && version.IsLessThan(2, 6)) {
                rsw.water.level = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load water level for {file}") / 5;

                if (version.IsAtLeast(1, 8)) {
                    rsw.water.type = RoLoaderValidation.ReadInt(data, $"WorldLoader.Load water type for {file}");
                    rsw.water.waveHeight = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load water wave height for {file}") / 5;
                    rsw.water.waveSpeed = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load water wave speed for {file}");
                    rsw.water.wavePitch = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load water wave pitch for {file}");

                    if (version.IsAtLeast(1, 9)) {
                        rsw.water.animSpeed = RoLoaderValidation.ReadInt(data, $"WorldLoader.Load water animation speed for {file}");
                    }
                }
            }

            //read lightmap
            if (version.IsAtLeast(1, 5)) {
                rsw.light.longitude = RoLoaderValidation.ReadInt(data, $"WorldLoader.Load light longitude for {file}");
                rsw.light.latitude = RoLoaderValidation.ReadInt(data, $"WorldLoader.Load light latitude for {file}");
                for (int i = 0; i < 3; i++) {
                    rsw.light.diffuse[i] = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load light diffuse[{i}] for {file}");
                }
                for (int i = 0; i < 3; i++) {
                    rsw.light.ambient[i] = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load light ambient[{i}] for {file}");
                }

                if (version.IsAtLeast(1, 7)) {
                    rsw.light.intensity = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load light intensity for {file}");
                }
            }

            // Read ground
            if (version.IsAtLeast(1, 6)) {
                rsw.ground.top = RoLoaderValidation.ReadInt(data, $"WorldLoader.Load ground top for {file}");
                rsw.ground.bottom = RoLoaderValidation.ReadInt(data, $"WorldLoader.Load ground bottom for {file}");
                rsw.ground.left = RoLoaderValidation.ReadInt(data, $"WorldLoader.Load ground left for {file}");
                rsw.ground.right = RoLoaderValidation.ReadInt(data, $"WorldLoader.Load ground right for {file}");
            }

            // 2.7 with a build above 0xdc: a block of count x 4 bytes the client skips
            if (version.IsAtLeast(2, 7) && buildNumber > 0xdc) {
                int skipped = RoLoaderValidation.ReadInt(data, $"WorldLoader.Load 2.7 block count for {file}");
                skipped = RoLoaderValidation.RequireCount(skipped, RoLoaderValidation.MaxCollectionCount, "2.7 block", $"WorldLoader.Load {file} (RSW {version.Text})");
                for (int i = 0; i < skipped; i++) {
                    RoLoaderValidation.ReadInt(data, $"WorldLoader.Load 2.7 block {i}/{skipped} for {file}");
                }
            }

            // Read Object
            int count = RoLoaderValidation.ReadInt(data, $"WorldLoader.Load object count for {file}");
            count = RoLoaderValidation.RequireCount(count, RoLoaderValidation.MaxCollectionCount, "object", $"WorldLoader.Load {file} (RSW {version.Text})");

            var models = rsw.modelDescriptors = new List<RSW.ModelDescriptor>(count);
            var lights = rsw.lights = new List<RSW.Light>(count);
            var sounds = rsw.sounds = new List<RSW.Sound>(count);
            var effects = rsw.effects = new List<RSW.Effect>(count);

            for (int i = 0; i < count; i++) {
                int objectTypeOffset = (int) data.Position;
                int objectType = RoLoaderValidation.ReadInt(data, $"WorldLoader.Load object type {i}/{count} for {file}");
                switch (objectType) {
                    case 1: //load model
                        var model = new RSW.ModelDescriptor();

                        model.name = version.IsAtLeast(1, 3) ? RoLoaderValidation.ReadBinaryString(data, 40, $"WorldLoader.Load model name {i}/{count} for {file}") : null;
                        model.animType = version.IsAtLeast(1, 3) ? RoLoaderValidation.ReadInt(data, $"WorldLoader.Load model animation type {i}/{count} for {file}") : 0;
                        model.animSpeed = version.IsAtLeast(1, 3) ? RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load model animation speed {i}/{count} for {file}") : 0f;
                        model.blockType = version.IsAtLeast(1, 3) ? RoLoaderValidation.ReadInt(data, $"WorldLoader.Load model block type {i}/{count} for {file}") : 0;
                        // after the block type: one byte from 2.6 with a build of 0xba or more (or major 3+), 4 bytes from 2.7
                        if (version.IsAtLeast(2, 6) && (buildNumber >= 0xba || version.IsAtLeast(3, 0))) {
                            model.misteryByte = RoLoaderValidation.ReadByte(data, $"WorldLoader.Load model mystery byte {i}/{count} for {file}");
                        }

                        if (version.IsAtLeast(2, 7)) {
                            RoLoaderValidation.ReadInt(data, $"WorldLoader.Load model 2.7 field {i}/{count} for {file}");
                        }

                        model.filename = RoLoaderValidation.ReadBinaryString(data, 80, $"WorldLoader.Load model filename {i}/{count} for {file}");
                        model.nodename = RoLoaderValidation.ReadBinaryString(data, 80, $"WorldLoader.Load model node name {i}/{count} for {file}");
                        model.position = new float[3];
                        for (int j = 0; j < model.position.Length; j++) {
                            model.position[j] = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load model position[{j}] {i}/{count} for {file}") / 5;
                        }
                        model.rotation = new float[3];
                        for (int j = 0; j < model.rotation.Length; j++) {
                            model.rotation[j] = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load model rotation[{j}] {i}/{count} for {file}");
                        }
                        model.scale = new float[3];
                        for (int j = 0; j < model.scale.Length; j++) {
                            model.scale[j] = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load model scale[{j}] {i}/{count} for {file}") / 5;
                        }
                        models.Add(model);
                        continue;
                    case 2: //load light
                        var light = new RSW.Light();
                        light.name = RoLoaderValidation.ReadBinaryString(data, 80, $"WorldLoader.Load light name {i}/{count} for {file}");
                        light.pos = new float[3];
                        for (int j = 0; j < light.pos.Length; j++) {
                            light.pos[j] = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load light position[{j}] {i}/{count} for {file}") / 5;
                        }
                        light.color = new float[3];
                        for (int j = 0; j < light.color.Length; j++) {
                            light.color[j] = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load light color[{j}] {i}/{count} for {file}");
                        }
                        light.range = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load light range {i}/{count} for {file}");
                        lights.Add(light);
                        continue;
                    case 3: //load sound
                        var sound = new RSW.Sound();
                        sound.name = RoLoaderValidation.ReadBinaryString(data, 80, $"WorldLoader.Load sound name {i}/{count} for {file}");
                        sound.file = "data/wav/" + RoLoaderValidation.ReadBinaryString(data, 80, $"WorldLoader.Load sound file {i}/{count} for {file}");
                        sound.pos = new float[3];
                        for (int j = 0; j < sound.pos.Length; j++) {
                            sound.pos[j] = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load sound position[{j}] {i}/{count} for {file}") / 5;
                        }
                        sound.vol = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load sound volume {i}/{count} for {file}");
                        sound.width = RoLoaderValidation.ReadInt(data, $"WorldLoader.Load sound width {i}/{count} for {file}");
                        sound.height = RoLoaderValidation.ReadInt(data, $"WorldLoader.Load sound height {i}/{count} for {file}");
                        sound.range = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load sound range {i}/{count} for {file}");
                        sound.cycle = version.IsAtLeast(2, 0) ? RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load sound cycle {i}/{count} for {file}") : 4f;   // the client reads 4 s for RSW < 2.0 (C3dWorldRes::Load 0x6db4d0)
                        sounds.Add(sound);
                        continue;
                    case 4: //load effect
                        var effect = new RSW.Effect();
                        effect.name = RoLoaderValidation.ReadBinaryString(data, 80, $"WorldLoader.Load effect name {i}/{count} for {file}");
                        effect.pos = new float[3];
                        for (int j = 0; j < effect.pos.Length; j++) {
                            effect.pos[j] = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load effect position[{j}] {i}/{count} for {file}") / 5;
                        }
                        effect.id = RoLoaderValidation.ReadInt(data, $"WorldLoader.Load effect id {i}/{count} for {file}");
                        effect.delay = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load effect delay {i}/{count} for {file}") * 10;
                        effect.param = new float[4];
                        for (int j = 0; j < effect.param.Length; j++) {
                            effect.param[j] = RoLoaderValidation.ReadFloat(data, $"WorldLoader.Load effect param[{j}] {i}/{count} for {file}");
                        }
                        effects.Add(effect);
                        continue;
                    default:
                        throw new Exception($"WorldLoader.Load: Unsupported RSW object type {objectType} at object {i}/{count}, offset {objectTypeOffset} in {file} (RSW {version.Text})");
                }
            }

            models.TrimExcess();
            sounds.TrimExcess();
            lights.TrimExcess();
            effects.TrimExcess();

            return rsw;
        }

        public static bool TryLoad(MemoryStreamReader data, string file, out RSW rsw, out string error) {
            try {
                rsw = Load(data, file);
                error = string.Empty;
                return true;
            } catch (Exception ex) {
                rsw = null;
                error = ex.Message;
                return false;
            }
        }

    }
}
