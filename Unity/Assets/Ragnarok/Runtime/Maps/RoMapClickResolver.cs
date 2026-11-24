using Ragnarok.Network;
using UnityEngine;

namespace Ragnarok.Maps {
    public enum RoMapClickTargetStatus {
        Accepted = 0,
        RayMiss = 1,
        MissingWalkSurface = 2,
        OutOfBounds = 3,
        NotWalkable = 4
    }

    public readonly struct RoMapClickTargetResult {
        public RoMapClickTargetResult(
            RoMapClickTargetStatus status,
            Vector2Int cell,
            Vector3 worldPoint,
            Vector3 cellWorldCenter,
            RoClickMoveTargetResult movementTarget,
            string message) {
            Status = status;
            Cell = cell;
            WorldPoint = worldPoint;
            CellWorldCenter = cellWorldCenter;
            MovementTarget = movementTarget;
            Message = message ?? string.Empty;
        }

        public RoMapClickTargetStatus Status { get; }
        public Vector2Int Cell { get; }
        public Vector3 WorldPoint { get; }
        public Vector3 CellWorldCenter { get; }
        public RoClickMoveTargetResult MovementTarget { get; }
        public string Message { get; }
        public bool Accepted => Status == RoMapClickTargetStatus.Accepted;
    }

    public static class RoMapClickResolver {
        public static RoMapClickTargetResult ResolveWorldPoint(
            RoWalkSurfaceGrid surface,
            Vector3 worldPoint,
            Matrix4x4 worldToLocal,
            Matrix4x4 localToWorld,
            int direction = 0,
            float centerYOffset = 0f) {
            if (surface == null) {
                return Rejected(
                    RoMapClickTargetStatus.MissingWalkSurface,
                    default,
                    worldPoint,
                    default,
                    "A walk surface is required before map clicks can be resolved.");
            }

            Vector3 localPoint = worldToLocal.MultiplyPoint3x4(worldPoint);
            return ResolveLocalPoint(surface, localPoint, localToWorld, direction, centerYOffset, worldPoint);
        }

        public static RoMapClickTargetResult ResolveLocalPoint(
            RoWalkSurfaceGrid surface,
            Vector3 localPoint,
            Matrix4x4 localToWorld,
            int direction = 0,
            float centerYOffset = 0f) {
            return ResolveLocalPoint(surface, localPoint, localToWorld, direction, centerYOffset, null);
        }

        public static RoMapClickTargetResult ResolveRayOnPlane(
            RoWalkSurfaceGrid surface,
            Ray worldRay,
            Plane worldPlane,
            Matrix4x4 worldToLocal,
            Matrix4x4 localToWorld,
            int direction = 0,
            float centerYOffset = 0f) {
            if (!worldPlane.Raycast(worldRay, out float distance)) {
                return Rejected(
                    RoMapClickTargetStatus.RayMiss,
                    default,
                    default,
                    default,
                    "Ray does not intersect the map plane.");
            }

            Vector3 worldPoint = worldRay.GetPoint(distance);
            return ResolveWorldPoint(surface, worldPoint, worldToLocal, localToWorld, direction, centerYOffset);
        }

        private static RoMapClickTargetResult ResolveLocalPoint(
            RoWalkSurfaceGrid surface,
            Vector3 localPoint,
            Matrix4x4 localToWorld,
            int direction,
            float centerYOffset,
            Vector3? worldPointOverride) {
            if (surface == null) {
                return Rejected(
                    RoMapClickTargetStatus.MissingWalkSurface,
                    default,
                    worldPointOverride ?? localPoint,
                    default,
                    "A walk surface is required before map clicks can be resolved.");
            }

            Vector2Int cell = RoMapCoordinateSystem.LocalPointToGatCell(localPoint);
            Vector3 worldPoint = worldPointOverride ?? localToWorld.MultiplyPoint3x4(localPoint);
            RoClickMoveTargetResult movementTarget = RoClickMovePlanner.ValidateTileTarget(surface, cell.x, cell.y, direction);
            if (!movementTarget.Accepted) {
                RoMapClickTargetStatus status = movementTarget.Status == RoClickMoveTargetStatus.OutOfBounds
                    ? RoMapClickTargetStatus.OutOfBounds
                    : RoMapClickTargetStatus.NotWalkable;
                return Rejected(status, cell, worldPoint, default, movementTarget.Message, movementTarget);
            }

            Vector3 localCenter = RoMapCoordinateSystem.WalkSurfaceCellCenter(surface, cell, centerYOffset);
            Vector3 worldCenter = localToWorld.MultiplyPoint3x4(localCenter);
            return new RoMapClickTargetResult(
                RoMapClickTargetStatus.Accepted,
                cell,
                worldPoint,
                worldCenter,
                movementTarget,
                string.Empty);
        }

        private static RoMapClickTargetResult Rejected(
            RoMapClickTargetStatus status,
            Vector2Int cell,
            Vector3 worldPoint,
            Vector3 cellWorldCenter,
            string message,
            RoClickMoveTargetResult movementTarget = default) {
            return new RoMapClickTargetResult(status, cell, worldPoint, cellWorldCenter, movementTarget, message);
        }
    }
}
