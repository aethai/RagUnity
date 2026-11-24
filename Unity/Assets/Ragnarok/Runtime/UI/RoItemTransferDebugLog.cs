using System;
using Ragnarok.Diagnostics;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>An item move between the bag, the storage, the cart and the like (the transfer debug log's kinds).</summary>
    public enum RoInventoryDragDropAction {
        None = 0,
        Equip = 1,
        Unequip = 2,
        DropToWorld = 3,
        LinkToChat = 4,
        MoveInventoryToStorage = 5,
        MoveStorageToInventory = 6,
        MoveInventoryToCart = 7,
        MoveCartToInventory = 8,
        AddInventoryToTrade = 9,
        MoveStorageToCart = 10,
        MoveCartToStorage = 11,
        RemoveRodexAttachment = 12
    }

    /// <summary>Verbose diagnostics of the item transfers (bag / storage / cart): the requests and the packets sent.</summary>
    internal static class RoItemTransferDebugLog {
        private const string Prefix = "[RO ItemTransfer]";

        public static bool IsTransferAction(RoInventoryDragDropAction action) {
            switch (action) {
                case RoInventoryDragDropAction.MoveInventoryToStorage:
                case RoInventoryDragDropAction.MoveStorageToInventory:
                case RoInventoryDragDropAction.MoveInventoryToCart:
                case RoInventoryDragDropAction.MoveCartToInventory:
                case RoInventoryDragDropAction.MoveStorageToCart:
                case RoInventoryDragDropAction.MoveCartToStorage:
                case RoInventoryDragDropAction.RemoveRodexAttachment:
                    return true;
                default:
                    return false;
            }
        }


        public static void UiTransfer(string source, RoInventoryDragDropAction action, int sourceIndex, int amount) {
            if (!IsTransferAction(action)) {
                return;
            }

            if (!RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                return;
            }

            RoRuntimeDiagnostics.IncrementDebugLogLine();
            Debug.Log($"{Prefix} ui source={source} action={action} sourceIndex={sourceIndex} amount={amount}");
        }

        public static void ExecutorTransfer(string executor, string packetName, int sourceIndex, int amount) {
            if (!RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                return;
            }

            RoRuntimeDiagnostics.IncrementDebugLogLine();
            Debug.Log($"{Prefix} executor={executor} packet={packetName} sourceIndex={sourceIndex} amount={amount}");
        }

        public static void Packet(string packetName, int sourceIndex, int amount, byte[] packet) {
            if (!RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                return;
            }

            RoRuntimeDiagnostics.IncrementDebugLogLine();
            Debug.Log($"{Prefix} packet={packetName} sourceIndex={sourceIndex} amount={amount} bytes={FormatBytes(packet)}");
        }

        public static void PacketPreviewFailed(string packetName, int sourceIndex, int amount, Exception exception) {
            if (!RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                return;
            }

            RoRuntimeDiagnostics.IncrementDebugLogLine();
            Debug.Log(
                $"{Prefix} packet-preview-failed packet={packetName} sourceIndex={sourceIndex} " +
                $"amount={amount} error={exception.GetType().Name}: {exception.Message}");
        }

        private static string FormatBytes(byte[] packet) {
            if (packet == null || packet.Length == 0) {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(packet.Length * 3);
            for (int i = 0; i < packet.Length; i++) {
                if (i > 0) {
                    builder.Append(' ');
                }

                builder.Append(packet[i].ToString("X2"));
            }

            return builder.ToString();
        }
    }
}
