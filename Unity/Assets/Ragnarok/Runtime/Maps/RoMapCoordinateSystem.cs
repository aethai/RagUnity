using ROIO.Models.FileTypes;
using Ragnarok.Network;
using UnityEngine;

namespace Ragnarok.Maps {
    public static class RoMapCoordinateSystem {
        public const float GroundModelSeparation = -0.002f;
        public const float SceneGridClearance = 0.02f;
        private const float WalkSurfaceIntersectionStepWorld = 0.25f;
        private const float WalkSurfaceIntersectionHeightTolerance = 0.1f;
        private const int WalkSurfaceIntersectionBinarySearchIterations = 8;

        public static Vector3 GroundLocalPosition => new Vector3(0f, GroundModelSeparation, 0f);
        public static Vector3 GroundLocalScale => new Vector3(1f, -1f, 1f);
        public static Vector3 WaterLocalScale => new Vector3(1f, -1f, 1f);

        public static Vector3 ModelRootLocalPosition(GND.Mesh ground) {
            return new Vector3(ground.width, 0f, ground.height);
        }

        public static bool TryNormalizeRay(Ray ray, out Ray normalizedRay) {
            normalizedRay = default;
            if (!IsFinite(ray.origin) || !IsFinite(ray.direction)) {
                return false;
            }

            float directionMagnitudeSqr = ray.direction.sqrMagnitude;
            if (directionMagnitudeSqr <= 0.000001f) {
                return false;
            }

            Vector3 direction = ray.direction;
            if (Mathf.Abs(directionMagnitudeSqr - 1f) > 0.0001f) {
                direction /= Mathf.Sqrt(directionMagnitudeSqr);
            }

            normalizedRay = new Ray(ray.origin, direction);
            return true;
        }

        public static float GatHeight(GAT.Cell cell) {
            return -(cell.Heights[0] + cell.Heights[1] + cell.Heights[2] + cell.Heights[3]) * 0.25f;
        }

        public static bool TrySampleGatHeight(GAT altitude, float localX, float localZ, out float height) {
            if (altitude == null || altitude.cells == null || altitude.width == 0 || altitude.height == 0) {
                height = 0f;
                return false;
            }

            float maxX = Mathf.Max(0f, altitude.width - 0.0001f);
            float maxZ = Mathf.Max(0f, altitude.height - 0.0001f);
            float clampedX = Mathf.Clamp(localX, 0f, maxX);
            float clampedZ = Mathf.Clamp(localZ, 0f, maxZ);
            int cellX = Mathf.Clamp(Mathf.FloorToInt(clampedX), 0, (int) altitude.width - 1);
            int cellY = Mathf.Clamp(Mathf.FloorToInt(clampedZ), 0, (int) altitude.height - 1);
            int index = cellX + (cellY * (int) altitude.width);
            if (index < 0 || index >= altitude.cells.Length) {
                height = 0f;
                return false;
            }

            GAT.Cell cell = altitude.cells[index];
            float fx = Mathf.Clamp01(clampedX - cellX);
            float fz = Mathf.Clamp01(clampedZ - cellY);
            float south = Mathf.Lerp(-cell.Heights[2], -cell.Heights[3], fx);
            float north = Mathf.Lerp(-cell.Heights[0], -cell.Heights[1], fx);
            height = Mathf.Lerp(south, north, fz);
            return true;
        }

        public static Vector3 GatCellCenter(GAT altitude, Vector2Int cell, float yOffset) {
            GAT.Cell gatCell = altitude.cells[cell.x + cell.y * (int) altitude.width];
            return new Vector3(cell.x + 0.5f, GatHeight(gatCell) + yOffset, cell.y + 0.5f);
        }

        public static bool TryResolveGatWalkSurfaceIntersection(
            GAT altitude,
            Matrix4x4 worldToLocal,
            Matrix4x4 localToWorld,
            Ray worldRay,
            out Vector3 worldPoint
        ) {
            worldPoint = default;
            if (altitude == null || altitude.cells == null || altitude.width == 0 || altitude.height == 0) {
                return false;
            }

            Vector3 localOrigin = worldToLocal.MultiplyPoint3x4(worldRay.origin);
            Vector3 localDirection = worldToLocal.MultiplyVector(worldRay.direction);
            if (localDirection.sqrMagnitude <= 0.000001f) {
                return false;
            }

            localDirection.Normalize();
            var localRay = new Ray(localOrigin, localDirection);
            float maxX = altitude.width;
            float maxZ = altitude.height;

            float horizontalMagnitude = Mathf.Max(Mathf.Abs(localDirection.x), Mathf.Abs(localDirection.z));
            if (horizontalMagnitude <= 0.0001f) {
                if (localOrigin.x < 0f || localOrigin.x > maxX || localOrigin.z < 0f || localOrigin.z > maxZ ||
                    Mathf.Abs(localDirection.y) <= 0.0001f ||
                    !TrySampleGatHeight(altitude, localOrigin.x, localOrigin.z, out float verticalHeight)) {
                    return false;
                }

                float verticalDistance = (verticalHeight - localOrigin.y) / localDirection.y;
                if (verticalDistance < 0f) {
                    return false;
                }

                Vector3 localPoint = localRay.GetPoint(verticalDistance);
                localPoint.y = verticalHeight;
                worldPoint = localToWorld.MultiplyPoint3x4(localPoint);
                return true;
            }

            if (!TryIntersectRayWithHorizontalBounds(localRay, 0f, maxX, 0f, maxZ, out float enterDistance, out float exitDistance)) {
                return false;
            }

            float previousDistance = Mathf.Max(0f, enterDistance);
            Vector3 previousPoint = localRay.GetPoint(previousDistance);
            if (!TrySampleGatHeight(altitude, previousPoint.x, previousPoint.z, out float previousHeight)) {
                return false;
            }

            float previousDelta = previousPoint.y - previousHeight;
            if (Mathf.Abs(previousDelta) <= WalkSurfaceIntersectionHeightTolerance) {
                previousPoint.y = previousHeight;
                worldPoint = localToWorld.MultiplyPoint3x4(previousPoint);
                return true;
            }

            float stepDistance = ResolveWalkSurfaceIntersectionStepDistance(localDirection);
            for (float distance = previousDistance + stepDistance; distance <= exitDistance; distance += stepDistance) {
                Vector3 point = localRay.GetPoint(distance);
                if (!TrySampleGatHeight(altitude, point.x, point.z, out float height)) {
                    continue;
                }

                float delta = point.y - height;
                if (Mathf.Abs(delta) <= WalkSurfaceIntersectionHeightTolerance) {
                    point.y = height;
                    worldPoint = localToWorld.MultiplyPoint3x4(point);
                    return true;
                }

                if ((previousDelta >= 0f && delta <= 0f) || (previousDelta <= 0f && delta >= 0f)) {
                    return TryRefineGatWalkSurfaceIntersection(
                        altitude,
                        localToWorld,
                        localRay,
                        previousDistance,
                        distance,
                        out worldPoint);
                }

                previousDistance = distance;
                previousDelta = delta;
            }

            return false;
        }

        public static Vector3 WalkSurfaceCellCenter(RoWalkSurfaceGrid surface, Vector2Int cell, float yOffset) {
            if (surface == null) {
                throw new System.ArgumentNullException(nameof(surface));
            }

            float height = surface.TryGetHeight(cell.x, cell.y, out float sampledHeight)
                ? sampledHeight
                : 0f;
            return new Vector3(cell.x + 0.5f, height + yOffset, cell.y + 0.5f);
        }

        public static Vector2Int LocalPointToGatCell(Vector3 localPoint) {
            return new Vector2Int(
                Mathf.FloorToInt(localPoint.x),
                Mathf.FloorToInt(localPoint.z));
        }

        public static Vector2Int WorldPointToGatCell(Matrix4x4 worldToLocal, Vector3 worldPoint) {
            return LocalPointToGatCell(worldToLocal.MultiplyPoint3x4(worldPoint));
        }

        public static bool TryLocalPointToGatCell(RoWalkSurfaceGrid surface, Vector3 localPoint, out Vector2Int cell) {
            if (surface == null) {
                cell = default;
                return false;
            }

            cell = LocalPointToGatCell(localPoint);
            return surface.Contains(cell.x, cell.y);
        }

        public static bool TryWorldPointToGatCell(
            RoWalkSurfaceGrid surface,
            Matrix4x4 worldToLocal,
            Vector3 worldPoint,
            out Vector2Int cell) {
            if (surface == null) {
                cell = default;
                return false;
            }

            cell = WorldPointToGatCell(worldToLocal, worldPoint);
            return surface.Contains(cell.x, cell.y);
        }

        public static Vector3 GatVertex(int x, int y, float rawHeight) {
            return new Vector3(x, -rawHeight, y);
        }

        public static Bounds TransformBounds(Bounds bounds, Vector3 localPosition, Vector3 localScale) {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            var transformed = new Bounds(TransformPoint(new Vector3(min.x, min.y, min.z), localPosition, localScale), Vector3.zero);
            transformed.Encapsulate(TransformPoint(new Vector3(min.x, min.y, max.z), localPosition, localScale));
            transformed.Encapsulate(TransformPoint(new Vector3(min.x, max.y, min.z), localPosition, localScale));
            transformed.Encapsulate(TransformPoint(new Vector3(min.x, max.y, max.z), localPosition, localScale));
            transformed.Encapsulate(TransformPoint(new Vector3(max.x, min.y, min.z), localPosition, localScale));
            transformed.Encapsulate(TransformPoint(new Vector3(max.x, min.y, max.z), localPosition, localScale));
            transformed.Encapsulate(TransformPoint(new Vector3(max.x, max.y, min.z), localPosition, localScale));
            transformed.Encapsulate(TransformPoint(new Vector3(max.x, max.y, max.z), localPosition, localScale));
            return transformed;
        }

        public static Bounds RenderedGroundLocalBounds(Mesh groundMesh) {
            return TransformBounds(groundMesh.bounds, GroundLocalPosition, GroundLocalScale);
        }

        public static Vector3 MapRootLocalPosition(Bounds renderedGroundLocalBounds) {
            return new Vector3(
                -renderedGroundLocalBounds.center.x,
                -renderedGroundLocalBounds.min.y + SceneGridClearance,
                -renderedGroundLocalBounds.center.z
            );
        }

        private static Vector3 TransformPoint(Vector3 point, Vector3 localPosition, Vector3 localScale) {
            return Vector3.Scale(point, localScale) + localPosition;
        }

        private static bool TryRefineGatWalkSurfaceIntersection(
            GAT altitude,
            Matrix4x4 localToWorld,
            Ray localRay,
            float startDistance,
            float endDistance,
            out Vector3 worldPoint
        ) {
            float lowDistance = startDistance;
            Vector3 lowPoint = localRay.GetPoint(lowDistance);
            if (!TrySampleGatHeight(altitude, lowPoint.x, lowPoint.z, out float lowHeight)) {
                worldPoint = default;
                return false;
            }

            float lowDelta = lowPoint.y - lowHeight;
            float highDistance = endDistance;

            for (int iteration = 0; iteration < WalkSurfaceIntersectionBinarySearchIterations; iteration++) {
                float midDistance = (lowDistance + highDistance) * 0.5f;
                Vector3 midPoint = localRay.GetPoint(midDistance);
                if (!TrySampleGatHeight(altitude, midPoint.x, midPoint.z, out float midHeight)) {
                    break;
                }

                float midDelta = midPoint.y - midHeight;
                if (Mathf.Abs(midDelta) <= WalkSurfaceIntersectionHeightTolerance) {
                    midPoint.y = midHeight;
                    worldPoint = localToWorld.MultiplyPoint3x4(midPoint);
                    return true;
                }

                if ((lowDelta >= 0f && midDelta >= 0f) || (lowDelta <= 0f && midDelta <= 0f)) {
                    lowDistance = midDistance;
                    lowDelta = midDelta;
                } else {
                    highDistance = midDistance;
                }
            }

            float hitDistance = (lowDistance + highDistance) * 0.5f;
            Vector3 point = localRay.GetPoint(hitDistance);
            if (!TrySampleGatHeight(altitude, point.x, point.z, out float height)) {
                worldPoint = default;
                return false;
            }

            point.y = height;
            worldPoint = localToWorld.MultiplyPoint3x4(point);
            return true;
        }

        private static bool IsFinite(Vector3 value) {
            return float.IsFinite(value.x) &&
                float.IsFinite(value.y) &&
                float.IsFinite(value.z);
        }

        private static bool TryIntersectRayWithHorizontalBounds(
            Ray ray,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            out float enterDistance,
            out float exitDistance
        ) {
            enterDistance = 0f;
            exitDistance = float.PositiveInfinity;

            if (!TryClipRayAxis(ray.origin.x, ray.direction.x, minX, maxX, ref enterDistance, ref exitDistance) ||
                !TryClipRayAxis(ray.origin.z, ray.direction.z, minZ, maxZ, ref enterDistance, ref exitDistance)) {
                return false;
            }

            if (exitDistance < 0f) {
                return false;
            }

            enterDistance = Mathf.Max(0f, enterDistance);
            return exitDistance >= enterDistance && !float.IsInfinity(exitDistance);
        }

        private static bool TryClipRayAxis(float origin, float direction, float min, float max, ref float enterDistance, ref float exitDistance) {
            if (Mathf.Abs(direction) <= 0.0001f) {
                return origin >= min && origin <= max;
            }

            float inverseDirection = 1f / direction;
            float distanceA = (min - origin) * inverseDirection;
            float distanceB = (max - origin) * inverseDirection;
            if (distanceA > distanceB) {
                (distanceA, distanceB) = (distanceB, distanceA);
            }

            enterDistance = Mathf.Max(enterDistance, distanceA);
            exitDistance = Mathf.Min(exitDistance, distanceB);
            return exitDistance >= enterDistance;
        }

        private static float ResolveWalkSurfaceIntersectionStepDistance(Vector3 direction) {
            float horizontalMagnitude = Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.z));
            return horizontalMagnitude <= 0.0001f
                ? 0.5f
                : Mathf.Clamp(WalkSurfaceIntersectionStepWorld / horizontalMagnitude, 0.1f, 2f);
        }
    }
}
