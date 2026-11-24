using UnityEngine;

namespace Ragnarok.Effects {
    public struct RoActorOverlaySpec {
        public RoActorOverlayKind Kind;
        public int EmotionId;
        public string Text;
        public string SpriteBasePath;
        public int SpriteActionIndex;
        public Color Color;
        public float DurationSeconds;
        public float HeightOffset;
        public float Scale;
        public float BobAmplitude;
        public float BobFrequency;
        public int RenderQueue;
        public bool UseDepthTest;
        public bool LoopSprite;

        public float EffectiveDurationSeconds => DurationSeconds > 0f
            ? DurationSeconds
            : (Kind == RoActorOverlayKind.EmotionSprite ? 1.8f : 2.2f);

        public float EffectiveHeightOffset => HeightOffset > 0f ? HeightOffset : 2.1f;
        public float EffectiveScale => Scale > 0f ? Scale : 1f;
        public float EffectiveBobAmplitude => BobAmplitude >= 0f ? BobAmplitude : 0.05f;
        public float EffectiveBobFrequency => BobFrequency > 0f ? BobFrequency : 4f;
        // transparent + 150 (above the actors and the effects); the old 30150 was past the last queue URP draws (5000),
        // so emotion balloons and status texts were never drawn
        public int EffectiveRenderQueue => RenderQueue > 0 ? RenderQueue : 3150;
        public int EffectiveSpriteActionIndex {
            get {
                if (Kind == RoActorOverlayKind.EmotionSprite) {
                    int resolvedActionIndex = RoEmotionCatalog.ResolveSpriteActionIndex(EmotionId);
                    if (SpriteActionIndex < 0) {
                        return resolvedActionIndex;
                    }

                    if (SpriteActionIndex == 0 && EmotionId != 0) {
                        return resolvedActionIndex >= 0 ? resolvedActionIndex : -1;
                    }
                }

                return SpriteActionIndex >= 0 ? SpriteActionIndex : -1;
            }
        }

        public string BuildFallbackText() {
            if (!string.IsNullOrWhiteSpace(Text)) {
                return Text;
            }

            return Kind == RoActorOverlayKind.EmotionSprite
                ? RoEmotionCatalog.BuildDebugLabel(EmotionId)
                : string.Empty;
        }

        public Color ResolveColor() {
            return Color.a > 0f ? Color : Color.white;
        }

        public static RoActorOverlaySpec Emotion(int emotionId, float durationSeconds = 0f) {
            return new RoActorOverlaySpec {
                Kind = RoActorOverlayKind.EmotionSprite,
                EmotionId = emotionId,
                SpriteBasePath = RoEmotionCatalog.EmotionSpriteBasePath,
                SpriteActionIndex = RoEmotionCatalog.ResolveSpriteActionIndex(emotionId),
                DurationSeconds = durationSeconds,
                HeightOffset = 2.1f,
                Scale = 1f,
                BobAmplitude = 0f,
                BobFrequency = 0f,
                RenderQueue = 3150,
                UseDepthTest = false,
                LoopSprite = false,
                Color = Color.white
            };
        }

        public static RoActorOverlaySpec StatusText(string text, float durationSeconds = 2.2f) {
            return new RoActorOverlaySpec {
                Kind = RoActorOverlayKind.StatusText,
                Text = text,
                DurationSeconds = durationSeconds,
                HeightOffset = 2.25f,
                Scale = 1f,
                BobAmplitude = 0.05f,
                BobFrequency = 4f,
                RenderQueue = 3140,
                UseDepthTest = false,
                Color = Color.white
            };
        }
    }
}
