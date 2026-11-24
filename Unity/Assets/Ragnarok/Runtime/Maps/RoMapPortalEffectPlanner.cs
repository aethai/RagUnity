using System;
using System.Collections.Generic;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.Maps {
    public readonly struct RoMapPortalIndicator {
        public RoMapPortalIndicator(string name, int effectId, Vector3 localPosition) {
            Name = name ?? string.Empty;
            EffectId = effectId;
            LocalPosition = localPosition;
        }

        public string Name { get; }
        public int EffectId { get; }
        public Vector3 LocalPosition { get; }
    }

    public static class RoMapPortalEffectPlanner {
        private static readonly HashSet<int> PortalEffectIds = new HashSet<int> {
            6, 8, 35, 36, 61, 316, 317, 321, 341, 344, 389, 402, 561, 562
        };

        public static RoMapPortalIndicator[] Build(RSW world) {
            if (world?.effects == null || world.effects.Count == 0) {
                return Array.Empty<RoMapPortalIndicator>();
            }

            var indicators = new List<RoMapPortalIndicator>();
            for (int i = 0; i < world.effects.Count; i++) {
                RSW.Effect effect = world.effects[i];
                if (effect == null || !IsPortalEffectId(effect.id) || effect.pos == null || effect.pos.Length < 3) {
                    continue;
                }

                indicators.Add(new RoMapPortalIndicator(
                    effect.name,
                    effect.id,
                    new Vector3(effect.pos[0], -effect.pos[1] + 0.05f, effect.pos[2])));
            }

            return indicators.ToArray();
        }

        public static bool IsPortalEffectId(int effectId) {
            return PortalEffectIds.Contains(effectId);
        }
    }
}
