using System;
using UnityEngine;

namespace ROIO.Models.FileTypes
{

    /**
     * Each Action represent a set of frames (think of idle, walk, pick)
     * Each Frame represent a frame of a given action
     * Each Layer has the information needed to render a frame
     * 
     * A single Motion can have multiple layers
     * Some layers might have their index as negative, those must (?) be ignored
     */
    [Serializable]
    public class ACT
    {
        public static string Header = "AC";

        public string filename;
        public string version;

        public string[] sounds;
        public Action[] actions;

        public bool TryGetAction(int actionIndex, out Action action)
        {
            if (actions == null || actionIndex < 0 || actionIndex >= actions.Length)
            {
                action = null;
                return false;
            }

            action = actions[actionIndex];
            return action != null;
        }

        public bool TryGetMotion(int actionIndex, int motionIndex, out Frame motion)
        {
            if (!TryGetAction(actionIndex, out Action action) ||
                action.frames == null ||
                motionIndex < 0 ||
                motionIndex >= action.frames.Length)
            {
                motion = null;
                return false;
            }

            motion = action.frames[motionIndex];
            return motion != null;
        }

        public bool TryGetSoundName(int soundIndex, out string soundName)
        {
            if (sounds == null || soundIndex < 0 || soundIndex >= sounds.Length)
            {
                soundName = null;
                return false;
            }

            soundName = sounds[soundIndex];
            return !string.IsNullOrWhiteSpace(soundName);
        }

        [Serializable]
        public class Action
        {
            public Frame[] frames;
            public float delay;
            public int FrameCount => frames.Length;
        }

        [Serializable]
        public class Frame
        {
            public Layer[] layers;
            public int soundId;
            public Vector2Int[] pos;
        }

        [Serializable]
        public class Layer
        {
            public Vector2Int pos;
            public int index;
            public bool isMirror;
            public Vector2 scale;
            public Color color;
            public float angle;
            public int sprType; // 0 = sprite, 1 = tga
            public int width;
            public int height;
        }
    }
}
