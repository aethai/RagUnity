using System;
using System.Collections.Generic;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.Maps {
    public readonly struct RoMapEffectEmitter {
        public RoMapEffectEmitter(string name, int effectId, Vector3 localPosition, IReadOnlyList<float> parameters) {
            Name = name ?? string.Empty;
            EffectId = effectId;
            LocalPosition = localPosition;
            Parameters = parameters ?? Array.Empty<float>();
        }

        public string Name { get; }
        public int EffectId { get; }
        public Vector3 LocalPosition { get; }
        public IReadOnlyList<float> Parameters { get; }
    }

    public static class RoMapEffectPlanner {
        public const int SmokeEffectId = 44;

        public static RoMapEffectEmitter[] BuildSmokeEmitters(RSW world, GND.Mesh ground) {
            if (world?.effects == null || world.effects.Count == 0) {
                return Array.Empty<RoMapEffectEmitter>();
            }

            float widthOffset = ground != null ? ground.width - 0.5f : 0f;
            float heightOffset = ground != null ? ground.height - 0.5f : 0f;
            var emitters = new List<RoMapEffectEmitter>();
            for (int i = 0; i < world.effects.Count; i++) {
                RSW.Effect effect = world.effects[i];
                if (effect == null || !IsSmokeEffectId(effect.id) || effect.pos == null || effect.pos.Length < 3) {
                    continue;
                }

                emitters.Add(new RoMapEffectEmitter(
                    effect.name,
                    effect.id,
                    // roBrowser centers RSW effect coordinates back into map space and lifts vertical Y by one unit.
                    new Vector3(effect.pos[0] + widthOffset, -effect.pos[1] + 1f, effect.pos[2] + heightOffset),
                    CopyParameters(effect.param)
                ));
            }

            return emitters.ToArray();
        }

        public static bool IsSmokeEffectId(int effectId) {
            return effectId == SmokeEffectId;
        }

        private static IReadOnlyList<float> CopyParameters(float[] parameters) {
            if (parameters == null || parameters.Length == 0) {
                return Array.Empty<float>();
            }

            var copy = new float[parameters.Length];
            Array.Copy(parameters, copy, parameters.Length);
            return copy;
        }
    }
}
