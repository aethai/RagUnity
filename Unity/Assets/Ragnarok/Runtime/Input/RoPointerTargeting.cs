using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.Input {
    public enum RoCursorState {
        Default = 0,
        Talk = 1,
        Click = 2,
        Lock = 3,
        Rotate = 4,
        Attack = 5,
        Warp = 7,
        Invalid = 8,
        Pickup = 9,
        Target = 10,
        GroundMove = 11,
        UiDrag = 12,
        UiResize = 13
    }

    public enum RoPointerTargetKind {
        None = 0,
        GroundCell = 10,
        Portal = 20,
        Item = 30,
        Player = 40,
        Monster = 50,
        Npc = 60,
        Ui = 70,
        ModalUi = 80
    }

    [Flags]
    public enum RoPointerModifierFlags {
        None = 0,
        Shift = 1 << 0,
        Ctrl = 1 << 1,
        Alt = 1 << 2,
        PrimaryHeld = 1 << 3,
        SecondaryHeld = 1 << 4
    }

    public readonly struct RoPointerTarget {
        public RoPointerTarget(
            RoPointerTargetKind kind,
            RoCursorState cursorState,
            int priority,
            bool isValid,
            bool blocksGroundClick,
            Vector2Int cell,
            Vector3 worldPosition,
            GameObject gameObject,
            string label
        ) {
            Kind = kind;
            CursorState = cursorState;
            Priority = priority;
            IsValid = isValid;
            BlocksGroundClick = blocksGroundClick;
            Cell = cell;
            WorldPosition = worldPosition;
            GameObject = gameObject;
            Label = label;
        }

        public RoPointerTargetKind Kind { get; }
        public RoCursorState CursorState { get; }
        public int Priority { get; }
        public bool IsValid { get; }
        public bool BlocksGroundClick { get; }
        public Vector2Int Cell { get; }
        public Vector3 WorldPosition { get; }
        public GameObject GameObject { get; }
        public string Label { get; }

        public static RoPointerTarget None => new RoPointerTarget(
            RoPointerTargetKind.None,
            RoCursorState.Default,
            RoPointerTargetResolver.GetPriority(RoPointerTargetKind.None),
            false,
            false,
            default,
            default,
            null,
            "none"
        );

        public static RoPointerTarget GroundCell(
            Vector2Int cell,
            Vector3 worldPosition,
            bool isWalkable,
            RoPointerModifierFlags modifiers = RoPointerModifierFlags.None
        ) {
            return new RoPointerTarget(
                RoPointerTargetKind.GroundCell,
                RoPointerTargetResolver.GetGroundCursorState(isWalkable, modifiers),
                RoPointerTargetResolver.GetPriority(RoPointerTargetKind.GroundCell),
                isWalkable,
                false,
                cell,
                worldPosition,
                null,
                isWalkable ? "ground cell" : "blocked ground cell"
            );
        }

        public static RoPointerTarget WorldTarget(
            RoPointerTargetKind kind,
            Vector3 worldPosition,
            GameObject gameObject,
            RoPointerModifierFlags modifiers,
            bool blocksGroundClick = true,
            string label = null
        ) {
            return new RoPointerTarget(
                kind,
                RoPointerTargetResolver.GetCursorState(kind, modifiers, true),
                RoPointerTargetResolver.GetPriority(kind),
                kind != RoPointerTargetKind.None,
                blocksGroundClick,
                default,
                worldPosition,
                gameObject,
                string.IsNullOrWhiteSpace(label) ? kind.ToString() : label
            );
        }

        public static RoPointerTarget Ui(string label = null, bool modal = false) {
            RoPointerTargetKind kind = modal ? RoPointerTargetKind.ModalUi : RoPointerTargetKind.Ui;
            return new RoPointerTarget(
                kind,
                RoPointerTargetResolver.GetCursorState(kind, RoPointerModifierFlags.None, true),
                RoPointerTargetResolver.GetPriority(kind),
                true,
                true,
                default,
                default,
                null,
                string.IsNullOrWhiteSpace(label) ? kind.ToString() : label
            );
        }

        public static RoPointerTarget Portal(
            Vector2Int cell,
            Vector3 worldPosition,
            GameObject gameObject,
            RoPointerModifierFlags modifiers,
            string label = null
        ) {
            return new RoPointerTarget(
                RoPointerTargetKind.Portal,
                RoPointerTargetResolver.GetCursorState(RoPointerTargetKind.Portal, modifiers, true),
                RoPointerTargetResolver.GetPriority(RoPointerTargetKind.Portal),
                true,
                true,
                cell,
                worldPosition,
                gameObject,
                string.IsNullOrWhiteSpace(label) ? "portal" : label
            );
        }

        public override string ToString() {
            return $"{Kind} cursor={CursorState} priority={Priority} valid={IsValid} label='{Label}' cell={Cell}";
        }
    }

    public static class RoPointerTargetResolver {
        public static int GetPriority(RoPointerTargetKind kind) {
            return kind switch {
                RoPointerTargetKind.ModalUi => 800,
                RoPointerTargetKind.Ui => 700,
                RoPointerTargetKind.Npc => 600,
                RoPointerTargetKind.Monster => 500,
                RoPointerTargetKind.Player => 400,
                RoPointerTargetKind.Item => 300,
                RoPointerTargetKind.Portal => 200,
                RoPointerTargetKind.GroundCell => 100,
                _ => 0
            };
        }

        public static RoCursorState GetGroundCursorState(
            bool isWalkable,
            RoPointerModifierFlags modifiers = RoPointerModifierFlags.None
        ) {
            if (!isWalkable) {
                return RoCursorState.Invalid;
            }

            return RoCursorState.GroundMove;
        }

        public static RoCursorState GetCursorState(
            RoPointerTargetKind kind,
            RoPointerModifierFlags modifiers,
            bool isValid
        ) {
            if (!isValid) {
                return RoCursorState.Invalid;
            }

            return kind switch {
                RoPointerTargetKind.ModalUi => RoCursorState.Default,
                RoPointerTargetKind.Ui => RoCursorState.Default,
                RoPointerTargetKind.Npc => RoCursorState.Talk,
                RoPointerTargetKind.Monster => RoCursorState.Attack,
                RoPointerTargetKind.Player => modifiers.HasFlag(RoPointerModifierFlags.Shift)
                    ? RoCursorState.Attack
                    : RoCursorState.Default,
                RoPointerTargetKind.Item => RoCursorState.Pickup,
                RoPointerTargetKind.Portal => RoCursorState.Warp,
                RoPointerTargetKind.GroundCell => GetGroundCursorState(isValid, modifiers),
                _ => RoCursorState.Default
            };
        }

        public static RoPointerTarget Resolve(
            IReadOnlyList<RoPointerTarget> candidates,
            RoPointerModifierFlags modifiers
        ) {
            if (candidates == null || candidates.Count == 0) {
                return RoPointerTarget.None;
            }

            RoPointerTarget best = RoPointerTarget.None;
            for (int i = 0; i < candidates.Count; i++) {
                RoPointerTarget candidate = candidates[i];
                if (candidate.Kind == RoPointerTargetKind.None) {
                    continue;
                }

                if (candidate.Priority < best.Priority) {
                    continue;
                }

                if (candidate.Priority == best.Priority && best.IsValid) {
                    continue;
                }

                best = WithResolvedCursor(candidate, modifiers);
            }

            return best;
        }

        public static bool AllowsGroundMove(RoPointerTarget target) {
            return target.Kind == RoPointerTargetKind.GroundCell && target.IsValid;
        }

        private static RoPointerTarget WithResolvedCursor(RoPointerTarget target, RoPointerModifierFlags modifiers) {
            RoCursorState cursorState = target.Kind == RoPointerTargetKind.GroundCell
                ? GetGroundCursorState(target.IsValid, modifiers)
                : GetCursorState(target.Kind, modifiers, target.IsValid);

            return new RoPointerTarget(
                target.Kind,
                cursorState,
                target.Priority,
                target.IsValid,
                target.BlocksGroundClick,
                target.Cell,
                target.WorldPosition,
                target.GameObject,
                target.Label
            );
        }
    }
}
