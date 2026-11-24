using Ragnarok.Input;
using Ragnarok.Sprites;
using UnityEngine;

namespace Ragnarok.Entities {
    public enum RoClickIntentKind {
        Idle = 0,
        BlockedByUi = 1,
        WalkToGround = 2,
        WalkToWorldPoint = 3,
        InteractEntity = 4,
        PickUpItem = 5,
        CastOnTile = 6,
        CastOnEntity = 7,
        RotateCamera = 8,
        HeadLook = 9,
        SittingHeadLook = 10
    }

    public readonly struct RoClickIntent {
        public RoClickIntent(
            RoClickIntentKind kind,
            RoPointerTarget target,
            Vector2Int cell,
            Vector3 lookPoint,
            RoActorDirection lookDirection,
            bool shouldApplyLook,
            RoPointerModifierFlags modifiers = RoPointerModifierFlags.None
        ) {
            Kind = kind;
            Target = target;
            Cell = cell;
            LookPoint = lookPoint;
            LookDirection = lookDirection;
            ShouldApplyLook = shouldApplyLook;
            Modifiers = modifiers;
        }

        public RoClickIntentKind Kind { get; }
        public RoPointerTarget Target { get; }
        public Vector2Int Cell { get; }
        public Vector3 LookPoint { get; }
        public RoActorDirection LookDirection { get; }
        public bool ShouldApplyLook { get; }
        public RoPointerModifierFlags Modifiers { get; }

        public bool HasLookPoint => LookDirection != RoActorDirection.None;

        public static RoClickIntent Idle => new RoClickIntent(
            RoClickIntentKind.Idle,
            RoPointerTarget.None,
            default,
            default,
            RoActorDirection.None,
            false,
            RoPointerModifierFlags.None
        );

        public static RoClickIntent ForKind(
            RoClickIntentKind kind,
            RoPointerTarget target = default,
            RoPointerModifierFlags modifiers = RoPointerModifierFlags.None
        ) {
            return new RoClickIntent(
                kind,
                target.Kind == RoPointerTargetKind.None ? RoPointerTarget.None : target,
                default,
                default,
                RoActorDirection.None,
                false,
                modifiers
            );
        }

        public static RoClickIntent ForGroundWalk(
            RoPointerTarget target,
            RoPointerModifierFlags modifiers = RoPointerModifierFlags.None
        ) {
            return new RoClickIntent(
                RoClickIntentKind.WalkToGround,
                target,
                target.Cell,
                default,
                RoActorDirection.None,
                false,
                modifiers
            );
        }

        public static RoClickIntent ForLook(
            RoClickIntentKind kind,
            Vector3 lookPoint,
            RoActorDirection lookDirection,
            bool shouldApplyLook,
            RoPointerModifierFlags modifiers = RoPointerModifierFlags.None
        ) {
            return new RoClickIntent(
                kind,
                RoPointerTarget.None,
                default,
                lookPoint,
                lookDirection,
                shouldApplyLook,
                modifiers
            );
        }
    }

    public enum RoClickIntentState {
        Idle = 0,
        Walking = 1,
        Interacting = 2,
        CastingOnTile = 3,
        CastingOnEntity = 4,
        UiCaptured = 5,
        RotateCamera = 6
    }

    public struct RoClickIntentTracker {
        public RoClickIntentTracker(
            RoClickIntentState state,
            RoPointerTarget pressedTarget,
            RoPointerModifierFlags pressedModifiers
        ) {
            State = state;
            PressedTarget = pressedTarget.Kind == RoPointerTargetKind.None ? RoPointerTarget.None : pressedTarget;
            PressedModifiers = pressedModifiers;
        }

        public RoClickIntentState State { get; private set; }
        public RoPointerTarget PressedTarget { get; private set; }
        public RoPointerModifierFlags PressedModifiers { get; private set; }

        public void Reset() {
            State = RoClickIntentState.Idle;
            PressedTarget = RoPointerTarget.None;
            PressedModifiers = RoPointerModifierFlags.None;
        }

        public void Begin(
            RoClickIntentState state,
            RoPointerTarget pressedTarget,
            RoPointerModifierFlags pressedModifiers
        ) {
            State = state;
            PressedTarget = pressedTarget.Kind == RoPointerTargetKind.None ? RoPointerTarget.None : pressedTarget;
            PressedModifiers = pressedModifiers;
        }
    }

    public readonly struct RoClickIntentContext {
        public RoClickIntentContext(
            bool hasPointerPosition,
            bool isPointerCaptured,
            bool hasCamera,
            bool isPrimaryHeld,
            bool wasPrimaryHeld,
            bool isSecondaryHeld,
            bool wasSecondaryHeld,
            bool isHeadLookModifierHeld,
            bool hasGridSelector,
            bool isSitting,
            bool hasSelectedSkill,
            bool selectedSkillTargetsTile,
            bool selectedSkillTargetsEntity,
            bool canSubmitPointerTargetClick,
            bool hasPointerTargetPoint,
            Vector3 pointerTargetPoint,
            RoActorDirection pointerTargetDirection,
            RoPointerTarget pointerTarget,
            bool hasSubmittedHeldGridCell,
            Vector2Int submittedHeldGridCell,
            bool hasSubmittedHeldHeadLookDirection,
            RoActorDirection submittedHeldHeadLookDirection,
            RoPointerModifierFlags pointerModifiers = RoPointerModifierFlags.None
        ) {
            HasPointerPosition = hasPointerPosition;
            IsPointerCaptured = isPointerCaptured;
            HasCamera = hasCamera;
            IsPrimaryHeld = isPrimaryHeld;
            WasPrimaryHeld = wasPrimaryHeld;
            IsSecondaryHeld = isSecondaryHeld;
            WasSecondaryHeld = wasSecondaryHeld;
            IsHeadLookModifierHeld = isHeadLookModifierHeld;
            HasGridSelector = hasGridSelector;
            IsSitting = isSitting;
            HasSelectedSkill = hasSelectedSkill;
            SelectedSkillTargetsTile = selectedSkillTargetsTile;
            SelectedSkillTargetsEntity = selectedSkillTargetsEntity;
            CanSubmitPointerTargetClick = canSubmitPointerTargetClick;
            HasPointerTargetPoint = hasPointerTargetPoint;
            PointerTargetPoint = pointerTargetPoint;
            PointerTargetDirection = pointerTargetDirection;
            PointerTarget = pointerTarget;
            HasSubmittedHeldGridCell = hasSubmittedHeldGridCell;
            SubmittedHeldGridCell = submittedHeldGridCell;
            HasSubmittedHeldHeadLookDirection = hasSubmittedHeldHeadLookDirection;
            SubmittedHeldHeadLookDirection = submittedHeldHeadLookDirection;
            PointerModifiers = pointerModifiers;
        }

        public bool HasPointerPosition { get; }
        public bool IsPointerCaptured { get; }
        public bool HasCamera { get; }
        public bool IsPrimaryHeld { get; }
        public bool WasPrimaryHeld { get; }
        public bool IsSecondaryHeld { get; }
        public bool WasSecondaryHeld { get; }
        public bool IsHeadLookModifierHeld { get; }
        public bool HasGridSelector { get; }
        public bool IsSitting { get; }
        public bool HasSelectedSkill { get; }
        public bool SelectedSkillTargetsTile { get; }
        public bool SelectedSkillTargetsEntity { get; }
        public bool CanSubmitPointerTargetClick { get; }
        public bool HasPointerTargetPoint { get; }
        public Vector3 PointerTargetPoint { get; }
        public RoActorDirection PointerTargetDirection { get; }
        public RoPointerTarget PointerTarget { get; }
        public bool HasSubmittedHeldGridCell { get; }
        public Vector2Int SubmittedHeldGridCell { get; }
        public bool HasSubmittedHeldHeadLookDirection { get; }
        public RoActorDirection SubmittedHeldHeadLookDirection { get; }
        public RoPointerModifierFlags PointerModifiers { get; }

        public bool IsPrimaryPressedThisFrame => IsPrimaryHeld && !WasPrimaryHeld;
        public bool IsPrimaryReleasedThisFrame => !IsPrimaryHeld && WasPrimaryHeld;
        public bool IsSecondaryPressedThisFrame => IsSecondaryHeld && !WasSecondaryHeld;
    }

    public static class RoClickIntentResolver {
        public static RoClickIntent Resolve(in RoClickIntentContext context) {
            if (!context.HasPointerPosition) {
                return RoClickIntent.Idle;
            }

            if (context.IsPointerCaptured) {
                return RoClickIntent.ForKind(RoClickIntentKind.BlockedByUi);
            }

            if (context.IsSecondaryPressedThisFrame || context.IsSecondaryHeld) {
                return RoClickIntent.ForKind(RoClickIntentKind.RotateCamera, context.PointerTarget);
            }

            if (!context.HasCamera || !context.IsPrimaryHeld) {
                return RoClickIntent.Idle;
            }

            if (context.IsSitting) {
                return RoClickIntent.ForLook(
                    RoClickIntentKind.SittingHeadLook,
                    context.PointerTargetPoint,
                    context.PointerTargetDirection,
                    RoClickToMoveController.ShouldApplyHeldSittingLook(
                        context.IsPrimaryHeld,
                        context.IsSitting,
                        context.HasPointerTargetPoint,
                        context.PointerTargetDirection,
                        context.HasSubmittedHeldHeadLookDirection,
                        context.SubmittedHeldHeadLookDirection
                    )
                );
            }

            if (context.IsPrimaryPressedThisFrame && context.HasSelectedSkill) {
                if (context.SelectedSkillTargetsTile && RoPointerTargetResolver.AllowsGroundMove(context.PointerTarget)) {
                    return RoClickIntent.ForKind(RoClickIntentKind.CastOnTile, context.PointerTarget);
                }

                if (context.SelectedSkillTargetsEntity && IsEntityTarget(context.PointerTarget)) {
                    return RoClickIntent.ForKind(RoClickIntentKind.CastOnEntity, context.PointerTarget);
                }
            }

            if (context.IsPrimaryPressedThisFrame && context.CanSubmitPointerTargetClick) {
                return context.PointerTarget.Kind == RoPointerTargetKind.Item
                    ? RoClickIntent.ForKind(RoClickIntentKind.PickUpItem, context.PointerTarget)
                    : RoClickIntent.ForKind(RoClickIntentKind.InteractEntity, context.PointerTarget);
            }

            if (context.IsHeadLookModifierHeld) {
                return RoClickIntent.ForLook(
                    RoClickIntentKind.HeadLook,
                    context.PointerTargetPoint,
                    context.PointerTargetDirection,
                    RoClickToMoveController.ShouldApplyHeldHeadLook(
                        context.IsPrimaryHeld,
                        context.IsHeadLookModifierHeld,
                        context.HasPointerTargetPoint,
                        context.PointerTargetDirection,
                        context.HasSubmittedHeldHeadLookDirection,
                        context.SubmittedHeldHeadLookDirection
                    )
                );
            }

            if (context.HasGridSelector) {
                return RoPointerTargetResolver.AllowsGroundMove(context.PointerTarget) &&
                    RoClickToMoveController.ShouldSubmitHeldGridTarget(
                        context.IsPrimaryHeld,
                        true,
                        context.PointerTarget.Cell,
                        context.HasSubmittedHeldGridCell,
                        context.SubmittedHeldGridCell
                    )
                    ? RoClickIntent.ForGroundWalk(context.PointerTarget)
                    : RoClickIntent.Idle;
            }

            return context.IsPrimaryPressedThisFrame
                ? RoClickIntent.ForKind(RoClickIntentKind.WalkToWorldPoint, context.PointerTarget)
                : RoClickIntent.Idle;
        }

        public static RoClickIntent Resolve(in RoClickIntentContext context, ref RoClickIntentTracker tracker) {
            if (!context.HasPointerPosition) {
                tracker.Reset();
                return RoClickIntent.Idle;
            }

            if (tracker.State == RoClickIntentState.UiCaptured) {
                if (context.IsPrimaryReleasedThisFrame || !context.IsPrimaryHeld) {
                    tracker.Reset();
                    return RoClickIntent.Idle;
                }

                return RoClickIntent.ForKind(
                    RoClickIntentKind.BlockedByUi,
                    RoPointerTarget.None,
                    tracker.PressedModifiers);
            }

            if (context.IsPrimaryReleasedThisFrame) {
                return ResolvePrimaryRelease(context, ref tracker);
            }

            if (context.IsPointerCaptured) {
                if (context.IsPrimaryPressedThisFrame || context.IsPrimaryHeld) {
                    tracker.Begin(
                        RoClickIntentState.UiCaptured,
                        RoPointerTarget.None,
                        context.PointerModifiers);
                }

                return RoClickIntent.ForKind(
                    RoClickIntentKind.BlockedByUi,
                    RoPointerTarget.None,
                    context.PointerModifiers);
            }

            if (context.IsSecondaryPressedThisFrame || context.IsSecondaryHeld) {
                tracker.Begin(
                    RoClickIntentState.RotateCamera,
                    context.PointerTarget,
                    context.PointerModifiers);
                return RoClickIntent.ForKind(
                    RoClickIntentKind.RotateCamera,
                    context.PointerTarget,
                    context.PointerModifiers);
            }

            if (tracker.State == RoClickIntentState.RotateCamera) {
                tracker.Reset();
            }

            if (!context.HasCamera || !context.IsPrimaryHeld) {
                return RoClickIntent.Idle;
            }

            if (context.IsSitting) {
                return RoClickIntent.ForLook(
                    RoClickIntentKind.SittingHeadLook,
                    context.PointerTargetPoint,
                    context.PointerTargetDirection,
                    RoClickToMoveController.ShouldApplyHeldSittingLook(
                        context.IsPrimaryHeld,
                        context.IsSitting,
                        context.HasPointerTargetPoint,
                        context.PointerTargetDirection,
                        context.HasSubmittedHeldHeadLookDirection,
                        context.SubmittedHeldHeadLookDirection
                    ),
                    context.PointerModifiers
                );
            }

            if (context.IsPrimaryPressedThisFrame) {
                if (context.HasSelectedSkill) {
                    RoClickIntentState skillState = context.SelectedSkillTargetsTile
                        ? RoClickIntentState.CastingOnTile
                        : context.SelectedSkillTargetsEntity
                            ? RoClickIntentState.CastingOnEntity
                            : RoClickIntentState.Idle;
                    tracker.Begin(skillState, context.PointerTarget, context.PointerModifiers);
                    return RoClickIntent.Idle;
                }

                if (context.CanSubmitPointerTargetClick) {
                    tracker.Begin(
                        RoClickIntentState.Interacting,
                        context.PointerTarget,
                        context.PointerModifiers);
                    return RoClickIntent.Idle;
                }
            }

            if (tracker.State == RoClickIntentState.Interacting ||
                tracker.State == RoClickIntentState.CastingOnTile ||
                tracker.State == RoClickIntentState.CastingOnEntity) {
                return RoClickIntent.Idle;
            }

            if (context.IsHeadLookModifierHeld) {
                return RoClickIntent.ForLook(
                    RoClickIntentKind.HeadLook,
                    context.PointerTargetPoint,
                    context.PointerTargetDirection,
                    RoClickToMoveController.ShouldApplyHeldHeadLook(
                        context.IsPrimaryHeld,
                        context.IsHeadLookModifierHeld,
                        context.HasPointerTargetPoint,
                        context.PointerTargetDirection,
                        context.HasSubmittedHeldHeadLookDirection,
                        context.SubmittedHeldHeadLookDirection
                    ),
                    context.PointerModifiers
                );
            }

            if (context.HasGridSelector) {
                if (RoPointerTargetResolver.AllowsGroundMove(context.PointerTarget) &&
                    RoClickToMoveController.ShouldSubmitHeldGridTarget(
                        context.IsPrimaryHeld,
                        true,
                        context.PointerTarget.Cell,
                        context.HasSubmittedHeldGridCell,
                        context.SubmittedHeldGridCell
                    )) {
                    tracker.Begin(
                        RoClickIntentState.Walking,
                        context.PointerTarget,
                        context.PointerModifiers);
                    return RoClickIntent.ForGroundWalk(context.PointerTarget, context.PointerModifiers);
                }

                return RoClickIntent.Idle;
            }

            if (context.IsPrimaryPressedThisFrame) {
                tracker.Begin(
                    RoClickIntentState.Walking,
                    context.PointerTarget,
                    context.PointerModifiers);
                return RoClickIntent.ForKind(
                    RoClickIntentKind.WalkToWorldPoint,
                    context.PointerTarget,
                    context.PointerModifiers);
            }

            return RoClickIntent.Idle;
        }

        private static RoClickIntent ResolvePrimaryRelease(
            in RoClickIntentContext context,
            ref RoClickIntentTracker tracker
        ) {
            RoClickIntentState state = tracker.State;
            RoPointerTarget pressedTarget = tracker.PressedTarget;
            RoPointerModifierFlags pressedModifiers = tracker.PressedModifiers;
            tracker.Reset();

            switch (state) {
                case RoClickIntentState.Interacting:
                    if (!IsEntityTarget(pressedTarget)) {
                        return RoClickIntent.Idle;
                    }

                    return pressedTarget.Kind == RoPointerTargetKind.Item
                        ? RoClickIntent.ForKind(RoClickIntentKind.PickUpItem, pressedTarget, pressedModifiers)
                        : RoClickIntent.ForKind(RoClickIntentKind.InteractEntity, pressedTarget, pressedModifiers);

                case RoClickIntentState.CastingOnTile:
                    return context.HasSelectedSkill &&
                        context.SelectedSkillTargetsTile &&
                        RoPointerTargetResolver.AllowsGroundMove(context.PointerTarget)
                            ? RoClickIntent.ForKind(
                                RoClickIntentKind.CastOnTile,
                                context.PointerTarget,
                                pressedModifiers)
                            : RoClickIntent.Idle;

                case RoClickIntentState.CastingOnEntity:
                    return context.HasSelectedSkill &&
                        context.SelectedSkillTargetsEntity &&
                        IsEntityTarget(context.PointerTarget)
                            ? RoClickIntent.ForKind(
                                RoClickIntentKind.CastOnEntity,
                                context.PointerTarget,
                                pressedModifiers)
                            : RoClickIntent.Idle;

                default:
                    return RoClickIntent.Idle;
            }
        }

        private static bool IsEntityTarget(RoPointerTarget target) {
            return target.IsValid &&
                target.Kind != RoPointerTargetKind.None &&
                target.Kind != RoPointerTargetKind.GroundCell &&
                target.Kind != RoPointerTargetKind.Ui &&
                target.Kind != RoPointerTargetKind.ModalUi;
        }
    }
}
