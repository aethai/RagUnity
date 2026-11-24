using System;

namespace Ragnarok.Network {
    public readonly struct RoBankRuntimeSnapshot {
        public RoBankRuntimeSnapshot(
            bool isOpen,
            bool hasSnapshot,
            long bankZeny,
            int onHandZeny,
            short lastReason,
            RoBankRuntimeOperation lastOperation) {
            IsOpen = isOpen;
            HasSnapshot = hasSnapshot;
            BankZeny = Math.Max(0, bankZeny);
            OnHandZeny = Math.Max(0, onHandZeny);
            LastReason = lastReason;
            LastOperation = lastOperation;
        }

        public bool IsOpen { get; }
        public bool HasSnapshot { get; }
        public long BankZeny { get; }
        public int OnHandZeny { get; }
        public short LastReason { get; }
        public RoBankRuntimeOperation LastOperation { get; }
    }

    public enum RoBankRuntimeOperation {
        None = 0,
        Open = 1,
        Check = 2,
        Deposit = 3,
        Withdraw = 4,
        Close = 5
    }
}
