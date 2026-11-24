using System;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The NPC talk logic outside the classic windows (<see cref="RoClassicNpcDialog"/>, <see cref="RoClassicNpcShop"/>):
    /// the gate a new CZ_CONTACTNPC passes (the shell's NPC router) and the zeny shop's requests and local closes. The
    /// original client keeps no shop once its windows go (the deal choice's Cancel sends CZ_CLOSE_DIALOG, the shop's Cancel
    /// CZ_NPC_TRADE_QUIT, Buy / Sell the cart), so every close drops the session's deal or list the way the barter and
    /// market shops do (<see cref="RoLoginShellSession.ClearNpcCommerceLocally"/>): nothing shows the shop again and the
    /// next contact is not refused for it. A contact right after such a close (nothing NPC-related arrived since) skips the
    /// router's repeat guard, so the NPC can be clicked again at once.
    /// </summary>
    public static class RoNpcInteractionModel {
        private static RoLoginShellSession closedSession;
        private static int closedNpcSequence = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            closedSession = null;
            closedNpcSequence = int.MinValue;
        }

        /// <summary>
        /// The contact gate: on the map, not busy, not moving between maps, no local action lock, and no NPC dialog or shop
        /// open (one conversation at a time).
        /// </summary>
        public static bool CanContactNpc(
            RoShellCommandStateSnapshot commandState,
            RoNpcDialogSnapshot dialog,
            RoNpcShopSnapshot shop,
            out string reason
        ) {
            if (commandState.Stage != RoLoginShellStage.MapEntered) {
                reason = "map is not active";
                return false;
            }

            if (commandState.IsBusy || commandState.IsMapTransitionPending) {
                reason = "session is busy";
                return false;
            }

            if (commandState.IsActionLocked) {
                reason = string.IsNullOrWhiteSpace(commandState.ActionLockReason)
                    ? "local action is locked"
                    : commandState.ActionLockReason;
                return false;
            }

            if (dialog?.IsOpen == true) {
                reason = "NPC dialog already open. Finish or close it first.";
                return false;
            }

            if (shop?.IsOpen == true) {
                reason = "NPC shop already open. Finish or close it first.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>True while the last NPC shop close was local and no NPC packet or request has changed the state since.</summary>
        public static bool ClosedLocally(RoLoginShellSession session) =>
            session != null &&
            ReferenceEquals(session, closedSession) &&
            closedNpcSequence == session.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.Npc);

        /// <summary>The deal choice's Buy (CZ_ACK_SELECT_DEALTYPE 0): the server answers with ZC_PC_PURCHASE_ITEMLIST.</summary>
        public static void OpenBuy(RoLoginShellSession session) {
            try {
                session?.BeginNpcShopOpenBuy();
            } catch (Exception ex) {
                Debug.LogWarning($"NPC buy list was not requested: {ex.Message}");
            }
        }

        /// <summary>The deal choice's Sell (CZ_ACK_SELECT_DEALTYPE 1): the server answers with ZC_PC_SELL_ITEMLIST.</summary>
        public static void OpenSell(RoLoginShellSession session) {
            try {
                session?.BeginNpcShopOpenSell();
            } catch (Exception ex) {
                Debug.LogWarning($"NPC sell list was not requested: {ex.Message}");
            }
        }

        /// <summary>The deal choice's Cancel (and its Esc): CZ_CLOSE_DIALOG, and the pending deal goes.</summary>
        public static void CancelDeal(RoLoginShellSession session) {
            try {
                session?.BeginNpcDialogClose();
            } catch (RoUnsupportedPacketProfileException) {
                // nothing to send on this profile: the deal still goes
            } catch (Exception ex) {
                Debug.LogWarning($"NPC deal close was not sent: {ex.Message}");
            }

            ClearLocally(session, RoNpcShopMode.DealType);
        }

        /// <summary>
        /// The buy / sell windows close: Cancel sends CZ_NPC_TRADE_QUIT (<paramref name="sendClose"/>), Buy / Sell already sent
        /// the cart (the server answers with ZC_PC_PURCHASE_RESULT / ZC_PC_SELL_RESULT); the list goes either way.
        /// </summary>
        public static void CloseStore(RoLoginShellSession session, RoNpcShopMode mode, bool sendClose) {
            if (sendClose) {
                try {
                    session?.BeginNpcShopClose();
                } catch (RoUnsupportedPacketProfileException) {
                    // nothing to send on this profile: the list still goes
                } catch (Exception ex) {
                    Debug.LogWarning($"NPC store close was not sent: {ex.Message}");
                }
            }

            ClearLocally(session, mode);
        }

        private static void ClearLocally(RoLoginShellSession session, RoNpcShopMode mode) {
            if (session == null) return;
            session.ClearNpcCommerceLocally(mode);
            closedSession = session;
            closedNpcSequence = session.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.Npc);
        }
    }
}
