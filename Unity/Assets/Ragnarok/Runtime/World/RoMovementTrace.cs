using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Ragnarok.Network;
using Ragnarok.Sprites;
using UnityEngine;

namespace Ragnarok.World {
    public enum RoMovementTraceEventType {
        Unknown = 0,
        Click = 1,
        MoveRequestSent = 2,
        OwnMovePacketObserved = 3,
        VisualMoveStarted = 4,
        MovementSample = 5,
        SegmentTransition = 6,
        StopMove = 7,
        ActionInterrupt = 8,
        HardSnap = 9,
        SoftCorrection = 10,
        CatchUp = 11,
        Hold = 12,
        CorrectionNone = 13,
        SettleCorrection = 14,
        ServerClockSample = 15
    }

    public readonly struct RoMovementTraceRecord {
        public const float UnsetFloat = -1f;
        public const int UnsetInt = -1;

        public RoMovementTraceRecord(
            RoMovementTraceEventType eventType,
            int frame,
            float timeSeconds,
            float deltaSeconds,
            uint actorId,
            RoMapActorObjectType objectType,
            bool isSelf,
            int logicalCellX,
            int logicalCellY,
            bool hasDisplayCell,
            int displayCellX,
            int displayCellY,
            Vector3 visualWorldPosition,
            uint moveStartTime,
            float serverElapsedSeconds,
            float presentedElapsedSeconds,
            RoMovePresentationCorrectionMode correctionMode,
            float distanceToAuthoritative,
            int segmentIndex,
            RoActorDirection facing,
            RoOnlineActorMotion onlineMotion,
            RoSpriteMotion spriteMotion,
            float walkPhase,
            string detail = null
        ) {
            EventType = eventType;
            Frame = frame;
            TimeSeconds = timeSeconds;
            DeltaSeconds = deltaSeconds;
            ActorId = actorId;
            ObjectType = objectType;
            IsSelf = isSelf;
            LogicalCellX = logicalCellX;
            LogicalCellY = logicalCellY;
            HasDisplayCell = hasDisplayCell;
            DisplayCellX = displayCellX;
            DisplayCellY = displayCellY;
            VisualWorldPosition = visualWorldPosition;
            MoveStartTime = moveStartTime;
            ServerElapsedSeconds = serverElapsedSeconds;
            PresentedElapsedSeconds = presentedElapsedSeconds;
            CorrectionMode = correctionMode;
            DistanceToAuthoritative = distanceToAuthoritative;
            SegmentIndex = segmentIndex;
            Facing = facing;
            OnlineMotion = onlineMotion;
            SpriteMotion = spriteMotion;
            WalkPhase = walkPhase;
            Detail = detail ?? string.Empty;
        }

        public RoMovementTraceEventType EventType { get; }
        public int Frame { get; }
        public float TimeSeconds { get; }
        public float DeltaSeconds { get; }
        public uint ActorId { get; }
        public RoMapActorObjectType ObjectType { get; }
        public bool IsSelf { get; }
        public int LogicalCellX { get; }
        public int LogicalCellY { get; }
        public bool HasDisplayCell { get; }
        public int DisplayCellX { get; }
        public int DisplayCellY { get; }
        public Vector3 VisualWorldPosition { get; }
        public uint MoveStartTime { get; }
        public float ServerElapsedSeconds { get; }
        public float PresentedElapsedSeconds { get; }
        public RoMovePresentationCorrectionMode CorrectionMode { get; }
        public float DistanceToAuthoritative { get; }
        public int SegmentIndex { get; }
        public RoActorDirection Facing { get; }
        public RoOnlineActorMotion OnlineMotion { get; }
        public RoSpriteMotion SpriteMotion { get; }
        public float WalkPhase { get; }
        public string Detail { get; }

        public static string CsvHeader =>
            "eventType,frame,timeSeconds,deltaSeconds,actorId,objectType,isSelf,logicalCellX,logicalCellY,hasDisplayCell,displayCellX,displayCellY,visualX,visualY,visualZ,moveStartTime,serverElapsedSeconds,presentedElapsedSeconds,correctionMode,distanceToAuthoritative,segmentIndex,facing,onlineMotion,spriteMotion,walkPhase,detail";

        public string ToCsvLine() {
            var builder = new StringBuilder(256);
            AppendCsv(builder, EventType.ToString());
            AppendCsv(builder, Frame);
            AppendCsv(builder, TimeSeconds);
            AppendCsv(builder, DeltaSeconds);
            AppendCsv(builder, ActorId);
            AppendCsv(builder, ObjectType.ToString());
            AppendCsv(builder, IsSelf);
            AppendCsv(builder, LogicalCellX);
            AppendCsv(builder, LogicalCellY);
            AppendCsv(builder, HasDisplayCell);
            AppendCsv(builder, DisplayCellX);
            AppendCsv(builder, DisplayCellY);
            AppendCsv(builder, VisualWorldPosition.x);
            AppendCsv(builder, VisualWorldPosition.y);
            AppendCsv(builder, VisualWorldPosition.z);
            AppendCsv(builder, MoveStartTime);
            AppendCsv(builder, ServerElapsedSeconds);
            AppendCsv(builder, PresentedElapsedSeconds);
            AppendCsv(builder, CorrectionMode.ToString());
            AppendCsv(builder, DistanceToAuthoritative);
            AppendCsv(builder, SegmentIndex);
            AppendCsv(builder, Facing.ToString());
            AppendCsv(builder, OnlineMotion.ToString());
            AppendCsv(builder, SpriteMotion.ToString());
            AppendCsv(builder, WalkPhase);
            AppendCsv(builder, Detail, last: true);
            return builder.ToString();
        }

        public override string ToString() {
            return $"t={TimeSeconds:0.000} f={Frame} event={EventType} actor={ActorId} {ObjectType} self={IsSelf} cell={LogicalCellX},{LogicalCellY} visual={VisualWorldPosition.x:0.###},{VisualWorldPosition.z:0.###} moveStart={MoveStartTime} server={ServerElapsedSeconds:0.###} presented={PresentedElapsedSeconds:0.###} correction={CorrectionMode} seg={SegmentIndex} facing={Facing} motion={OnlineMotion}/{SpriteMotion} {Detail}";
        }

        private static void AppendCsv(StringBuilder builder, string value, bool last = false) {
            value ??= string.Empty;
            bool quote = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (quote) {
                builder.Append('"');
                for (int i = 0; i < value.Length; i++) {
                    char c = value[i];
                    if (c == '"') {
                        builder.Append('"');
                    }

                    builder.Append(c);
                }

                builder.Append('"');
            } else {
                builder.Append(value);
            }

            if (!last) {
                builder.Append(',');
            }
        }

        private static void AppendCsv(StringBuilder builder, int value) {
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
        }

        private static void AppendCsv(StringBuilder builder, uint value) {
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
        }

        private static void AppendCsv(StringBuilder builder, bool value) {
            builder.Append(value ? "true" : "false");
            builder.Append(',');
        }

        private static void AppendCsv(StringBuilder builder, float value) {
            builder.Append(value.ToString("0.######", CultureInfo.InvariantCulture));
            builder.Append(',');
        }
    }

    public sealed class RoMovementTraceBuffer {
        private readonly List<RoMovementTraceRecord> entries;
        private int capacity;
        private long revision;

        public RoMovementTraceBuffer(int capacity = 4096) {
            this.capacity = Math.Max(1, capacity);
            entries = new List<RoMovementTraceRecord>(Math.Min(this.capacity, 4096));
        }

        public bool IsEnabled { get; set; } = true;
        public int Capacity {
            get => capacity;
            set {
                capacity = Math.Max(1, value);
                TrimToCapacity();
            }
        }

        public int Count => entries.Count;
        public long Revision => revision;
        public IReadOnlyList<RoMovementTraceRecord> Entries => entries;

        public bool Record(in RoMovementTraceRecord record) {
            if (!IsEnabled) {
                return false;
            }

            entries.Add(record);
            revision++;
            TrimToCapacity();
            return true;
        }

        public void Clear() {
            if (entries.Count == 0) {
                return;
            }

            entries.Clear();
            revision++;
        }

        public RoMovementTraceRecord[] Snapshot() {
            return entries.Count == 0 ? Array.Empty<RoMovementTraceRecord>() : entries.ToArray();
        }

        public int Snapshot(List<RoMovementTraceRecord> output) {
            if (output == null || entries.Count == 0) {
                return 0;
            }

            output.AddRange(entries);
            return entries.Count;
        }

        public string ToCsv() {
            var builder = new StringBuilder(Math.Max(256, entries.Count * 180));
            builder.AppendLine(RoMovementTraceRecord.CsvHeader);
            for (int i = 0; i < entries.Count; i++) {
                builder.AppendLine(entries[i].ToCsvLine());
            }

            return builder.ToString();
        }

        private void TrimToCapacity() {
            int overflow = entries.Count - capacity;
            if (overflow > 0) {
                entries.RemoveRange(0, overflow);
            }
        }
    }
}
