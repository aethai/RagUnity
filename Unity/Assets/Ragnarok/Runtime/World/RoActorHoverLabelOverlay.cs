using System.Collections.Generic;
using Ragnarok.Data;
using Ragnarok.Diagnostics;
using Ragnarok.Entities;
using Ragnarok.Input;
using Ragnarok.Maps;
using Ragnarok.Network;
using Ragnarok.UI;
using UnityEngine;

namespace Ragnarok.World {
    public readonly struct RoActorHoverLabelData {
        public RoActorHoverLabelData(string primary, string secondary, Color32 color) {
            Primary = primary ?? string.Empty;
            Secondary = secondary ?? string.Empty;
            Color = color;
        }

        public string Primary { get; }
        public string Secondary { get; }
        public Color32 Color { get; }
        public bool HasLabel => !string.IsNullOrWhiteSpace(Primary);
    }

    public static class RoActorHoverLabelFormatter {
        public static readonly Color32 PlayerColor = new Color32(0xff, 0xff, 0xff, 0xff);
        public static readonly Color32 GameMasterColor = new Color32(0xff, 0xff, 0x00, 0xff);
        public static readonly Color32 MonsterColor = new Color32(0xff, 0xc6, 0xc6, 0xff);
        public static readonly Color32 NpcColor = new Color32(0x94, 0xbd, 0xf7, 0xff);
        public static readonly Color32 ItemColor = new Color32(0xff, 0xef, 0x94, 0xff);

        public static bool TryBuild(RoOnlineActorSnapshot actor, out RoActorHoverLabelData label) {
            return TryBuild(actor, RoItemMetadataTable.Empty, out label);
        }

        public static bool TryBuild(
            RoOnlineActorSnapshot actor,
            RoItemMetadataTable itemMetadata,
            out RoActorHoverLabelData label
        ) {
            label = default;
            if (actor.ServerId == 0 ||
                actor.ObjectType == RoMapActorObjectType.Warp ||
                actor.SpawnState == RoMapActorSpawnState.Dead ||
                actor.Motion == RoOnlineActorMotion.Dead) {
                return false;
            }

            string primary = BuildPrimary(actor, itemMetadata);
            if (string.IsNullOrWhiteSpace(primary)) {
                return false;
            }

            label = new RoActorHoverLabelData(primary, BuildSecondary(actor), ResolveColor(actor, primary));
            return true;
        }

        public static string BuildPrimary(RoOnlineActorSnapshot actor) {
            return BuildPrimary(actor, RoItemMetadataTable.Empty);
        }

        public static string BuildPrimary(RoOnlineActorSnapshot actor, RoItemMetadataTable itemMetadata) {
            if (actor.ObjectType == RoMapActorObjectType.Item) {
                string itemName = BuildItemPrimary(actor, itemMetadata);
                if (!string.IsNullOrWhiteSpace(itemName)) {
                    return itemName;
                }
            }

            string name = !string.IsNullOrWhiteSpace(actor.Name)
                ? NormalizeLabelText(actor.Name)
                : BuildFallbackName(actor);

            if (actor.ObjectType == RoMapActorObjectType.Player &&
                !string.IsNullOrWhiteSpace(actor.PartyName)) {
                string partyName = NormalizeLabelText(actor.PartyName);
                if (!string.IsNullOrEmpty(partyName)) {
                    return $"{name} ({partyName})";
                }
            }

            return name;
        }

        /// <summary>
        /// The balloon lines of a player's or an NPC's name card (CActorNameCard, filled by 0x0a30 / 0x0adf through
        /// 0x588cf0: +4 name, +0x1c party, +0x34 guild, +0x4c position, +0x64 title (GetTitleString of the title id),
        /// +0x7c the 0x0adf title text) as vf54 0x7c7560 lays them out, top to bottom, each only when not empty:
        /// /showname on ("type 1", [0x014ef125] set): +0x7c, the position, the name; off ("type 2"): +0x7c,
        /// "[title] name (party)" (the parts that exist; "%s (%s)" for Custom's langtype), "guild [position]". The name is
        /// cut at '#'. <paramref name="titleName"/> resolves a title id (null: none).
        /// </summary>
        public static void BuildNameCardLines(RoOnlineActorSnapshot actor, bool showNameType1, System.Func<int, string> titleName,
            out string first, out string second, out string third) {
            string name = !string.IsNullOrWhiteSpace(actor.Name) ? NormalizeLabelText(actor.Name) : BuildFallbackName(actor);
            bool player = actor.ObjectType == RoMapActorObjectType.Player;
            // a player's card comes from 0x0a30 (no +0x7c text, a title id); an NPC's from 0x0adf (its title text at +0x7c)
            string extra = player ? string.Empty : Trim(actor.TitleName);
            string title = player && actor.TitleId > 0 && titleName != null ? NormalizeLabelText(titleName(actor.TitleId)) : string.Empty;
            string position = player ? Trim(actor.GuildPositionName) : string.Empty;
            string a, b;
            if (showNameType1) {
                a = position;
                b = name;
            } else {
                string party = player ? Trim(actor.PartyName) : string.Empty;
                string guild = player ? Trim(actor.GuildName) : string.Empty;
                string named = title.Length > 0 ? "[" + title + "] " + name : name;
                a = party.Length > 0 ? named + " (" + party + ")" : named;
                b = guild.Length > 0 ? guild + " [" + position + "]" : string.Empty;
            }

            // the lines that exist, in order
            first = extra;
            second = a;
            third = b;
            if (second.Length == 0) {
                second = third;
                third = string.Empty;
            }

            if (first.Length == 0) {
                first = second;
                second = third;
                third = string.Empty;
            }
        }

        public static string BuildSecondary(RoOnlineActorSnapshot actor) {
            if (IsMonsterLike(actor.ObjectType) && actor.MaxHp > 0) {
                int hp = Mathf.Clamp(actor.Hp, 0, actor.MaxHp);
                return $"HP: {hp}/{actor.MaxHp}";
            }

            if (actor.ObjectType == RoMapActorObjectType.Player) {
                string guild = Trim(actor.GuildName);
                string position = Trim(actor.GuildPositionName);
                if (!string.IsNullOrEmpty(guild) && !string.IsNullOrEmpty(position)) {
                    return $"{guild} [{position}]";
                }

                if (!string.IsNullOrEmpty(guild)) {
                    return guild;
                }

                if (!string.IsNullOrEmpty(position)) {
                    return position;
                }

                string title = NormalizeLabelText(actor.TitleName);
                if (!string.IsNullOrEmpty(title)) {
                    return title;
                }

                return Trim(actor.PartyName);
            }

            if (actor.ObjectType == RoMapActorObjectType.Npc || actor.ObjectType == RoMapActorObjectType.Npc2) {
                return Trim(actor.TitleName);
            }

            return string.Empty;
        }

        public static Color32 ResolveColor(RoOnlineActorSnapshot actor) {
            return ResolveColor(actor, BuildPrimary(actor));
        }

        private static Color32 ResolveColor(RoOnlineActorSnapshot actor, string primary) {
            if (primary.StartsWith("[GM]", System.StringComparison.OrdinalIgnoreCase)) {
                return GameMasterColor;
            }

            if (actor.ObjectType == RoMapActorObjectType.Item) {
                return ItemColor;
            }

            if (actor.ObjectType == RoMapActorObjectType.Npc || actor.ObjectType == RoMapActorObjectType.Npc2) {
                return NpcColor;
            }

            return IsMonsterLike(actor.ObjectType) ? MonsterColor : PlayerColor;
        }

        public static bool IsMonsterLike(RoMapActorObjectType objectType) {
            return objectType == RoMapActorObjectType.Monster ||
                objectType == RoMapActorObjectType.Pet ||
                objectType == RoMapActorObjectType.Homunculus ||
                objectType == RoMapActorObjectType.Mercenary ||
                objectType == RoMapActorObjectType.Elemental;
        }

        private static string BuildItemPrimary(RoOnlineActorSnapshot actor, RoItemMetadataTable itemMetadata) {
            string name = ResolveItemDisplayName(actor, itemMetadata);
            if (string.IsNullOrWhiteSpace(name)) {
                name = !string.IsNullOrWhiteSpace(actor.Name)
                    ? NormalizeLabelText(actor.Name)
                    : BuildFallbackName(actor);
            }

            return actor.ItemCount > 1 ? $"{name} x{actor.ItemCount}" : name;
        }

        private static string ResolveItemDisplayName(RoOnlineActorSnapshot actor, RoItemMetadataTable itemMetadata) {
            if (actor.ItemId == 0) {
                return "Zeny";
            }

            RoItemMetadataTable table = itemMetadata ?? RoItemMetadataTable.Empty;
            if (!table.TryGetItem(unchecked((int) actor.ItemId), out RoItemMetadata item) || item == null) {
                return string.Empty;
            }

            string name = NormalizeLabelText(item.GetDisplayName(actor.ItemIdentified));
            return IsUnknownItemName(name) ? string.Empty : name;
        }

        private static bool IsUnknownItemName(string name) {
            return string.IsNullOrWhiteSpace(name) ||
                string.Equals(name.Trim(), "Unknown Item", System.StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildFallbackName(RoOnlineActorSnapshot actor) {
            if (actor.ObjectType == RoMapActorObjectType.Item && actor.ItemId != 0) {
                return actor.ItemCount > 1 ? $"Item {actor.ItemId} x{actor.ItemCount}" : $"Item {actor.ItemId}";
            }

            return actor.ObjectType == RoMapActorObjectType.Unknown
                ? $"Actor {actor.ServerId}"
                : $"{actor.ObjectType} {actor.ServerId}";
        }

        private static string Trim(string value) {
            return NormalizeLabelText(value);
        }

        private static string NormalizeLabelText(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            string sanitized = value.Replace("\0", string.Empty).Trim();
            int hashIndex = sanitized.IndexOf('#');
            return hashIndex >= 0
                ? sanitized.Substring(0, hashIndex).Trim()
                : sanitized;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10040)]
    public sealed class RoActorHoverLabelOverlay : MonoBehaviour {
        private const float LabelScreenOffsetY = 5f;
        private const float IdentityRequestIntervalSeconds = 1f;
        private const float LabelVerticalGap = 1f;
        private const float HorizontalPadding = 6f;
        private const float DependencyLookupRetrySeconds = 0.5f;
        private const float RenderableBoundsCacheLifetimeSeconds = 0.05f;
        private static readonly Color32 OutlineColor = new Color32(0, 0, 0, 255);
        private static readonly System.Func<int, string> TitleNameOf = Ragnarok.UI.RoClassicAchievements.TitleNameOf;

        // the last name card built: its inputs (the snapshot keeps the same strings until they change) and its lines
        private string cardName, cardParty, cardGuild, cardPosition, cardTitleName, cardFirst, cardSecond, cardThird;
        private int cardTitleId = -1;
        private RoMapActorObjectType cardType;
        private bool cardShowName;

        [SerializeField] private RoOnlineWorldController worldController;
        [SerializeField] private RoClickToMoveController clickToMoveController;
        [SerializeField] private RoMinimalLoginShell loginShell;
        [SerializeField] private UnityEngine.Camera renderCamera;
        private RoRuntimeMapController runtimeMapController;
        private RoItemMetadataTable itemMetadata = RoItemMetadataTable.Empty;
        private RoRuntimeMapController itemMetadataSourceMapController;

        private readonly Dictionary<uint, float> nextIdentityRequestTimeByActorId = new Dictionary<uint, float>();
        private GUIStyle primaryStyle;
        private GUIStyle secondaryStyle;
        private GUIStyle outlineStyle;
        private readonly GUIContent primaryContent = new GUIContent();
        private readonly GUIContent secondaryContent = new GUIContent();
        private readonly List<Renderer> rendererBuffer = new List<Renderer>(8);
        private Transform cachedBoundsRoot;
        private UnityEngine.Camera cachedBoundsCamera;
        private Rect cachedBounds;
        private Vector3 cachedBoundsRootPosition;
        private Quaternion cachedBoundsRootRotation;
        private Vector3 cachedBoundsRootScale;
        private Vector3 cachedBoundsCameraPosition;
        private Quaternion cachedBoundsCameraRotation;
        private float cachedBoundsUntilTime;
        private bool cachedBoundsValid;
        private float nextWorldControllerLookupTime;
        private float nextClickToMoveLookupTime;
        private float nextLoginShellLookupTime;
        private float nextRuntimeMapControllerLookupTime;

        private void Awake() {
            useGUILayout = false;
        }

        public void SetWorldController(RoOnlineWorldController controller) {
            worldController = controller;
            runtimeMapController = controller != null ? controller.RuntimeMapController : null;
            itemMetadataSourceMapController = null;
            itemMetadata = RoItemMetadataTable.Empty;
            nextWorldControllerLookupTime = 0f;
            nextRuntimeMapControllerLookupTime = 0f;
        }

        public void SetClickToMoveController(RoClickToMoveController controller) {
            clickToMoveController = controller;
            nextClickToMoveLookupTime = 0f;
        }

        public void SetLoginShell(RoMinimalLoginShell shell) {
            loginShell = shell;
            nextLoginShellLookupTime = 0f;
        }

        /// <summary>Classic UI path: the original's UINameBalloonText drawn by the classic renderer (no IMGUI).</summary>
        private void LateUpdate() {

            Ragnarok.UI.Classic.RoClassicNameBalloon balloon = Ragnarok.UI.Classic.RoClassicNameBalloon.Hover;
            UnityEngine.Camera camera = ResolveCamera();
            if (camera == null ||
                !TryResolveHover(out RoOnlineActorSnapshot actor, out Vector3 anchorWorldPosition, out GameObject _)) {
                balloon.Hide();
                return;
            }

            TryRequestIdentity(actor);
            Vector3 screen = camera.WorldToScreenPoint(anchorWorldPosition);
            if (screen.z <= 0f || !RoActorHoverLabelFormatter.TryBuild(actor, ResolveItemMetadata(), out RoActorHoverLabelData label)) {
                balloon.Hide();
                return;
            }

            // 0xbfb270: the guild's emblem while /emblem is on, framed on a siege map with /frame (Emblem Border) on
            balloon.SetEmblem(actor.GuildId != 0 && Ragnarok.UI.RoClassicGuildEmblems.ShowInBalloons ? Ragnarok.UI.RoClassicGuildEmblems.Resolve(actor.GuildId) : null,
                RoPvpState.IsSiege && Ragnarok.UI.RoClassicCommandFlags.EmblemFrame);
            string first = label.Primary, second = label.Secondary, third = null;
            if (actor.ObjectType == RoMapActorObjectType.Player || actor.ObjectType == RoMapActorObjectType.Npc ||
                actor.ObjectType == RoMapActorObjectType.Npc2) {
                // the name card's lines as /showname picks them (vf54 0x7c7560)
                NameCardLines(actor, out first, out second, out third);
            }

            // the actor's projected ground position, truncated like CGameActor's screen x/y (+0xac/+0xb0); the own
            // player's balloon follows /font
            balloon.Show(first, second, third, label.Color, (int) screen.x, camera.pixelHeight - (int) screen.y, 1f, actor.IsSelf);
        }

        private void NameCardLines(RoOnlineActorSnapshot actor, out string first, out string second, out string third) {
            bool showName = Ragnarok.UI.RoClassicCommandFlags.ShowName;
            if (cardFirst == null || showName != cardShowName || actor.ObjectType != cardType || actor.TitleId != cardTitleId ||
                !ReferenceEquals(actor.Name, cardName) || !ReferenceEquals(actor.PartyName, cardParty) ||
                !ReferenceEquals(actor.GuildName, cardGuild) || !ReferenceEquals(actor.GuildPositionName, cardPosition) ||
                !ReferenceEquals(actor.TitleName, cardTitleName)) {
                RoActorHoverLabelFormatter.BuildNameCardLines(actor, showName, TitleNameOf, out cardFirst, out cardSecond, out cardThird);
                cardShowName = showName;
                cardType = actor.ObjectType;
                cardTitleId = actor.TitleId;
                cardName = actor.Name;
                cardParty = actor.PartyName;
                cardGuild = actor.GuildName;
                cardPosition = actor.GuildPositionName;
                cardTitleName = actor.TitleName;
            }

            first = cardFirst;
            second = cardSecond;
            third = cardThird;
        }

        private void OnDisable() {
            Ragnarok.UI.Classic.RoClassicNameBalloon.Hover.Hide();
        }

        /// <summary>Diagnostics: pretend the cursor hovers this actor (0 = the real pointer target).</summary>
        public static uint DebugHoverActorId;

        private bool TryResolveHover(
            out RoOnlineActorSnapshot actor,
            out Vector3 anchorWorldPosition,
            out GameObject targetObject
        ) {
            actor = default;
            anchorWorldPosition = default;
            targetObject = null;
            if (DebugHoverActorId != 0) {
                RoOnlineWorldController debugController = worldController != null
                    ? worldController
                    : FindFirstObjectByType<RoOnlineWorldController>();
                if (debugController == null || !debugController.State.TryGetActor(DebugHoverActorId, out actor)) {
                    return false;
                }

                RoOnlineActorPresenter debugPresenter = debugController.ActorPresenter;
                if (debugPresenter == null ||
                    !debugPresenter.TryGetPresentedActorTransform(DebugHoverActorId, out Transform debugTransform) ||
                    debugTransform == null) {
                    return false;
                }

                anchorWorldPosition = debugTransform.position;
                targetObject = debugTransform.gameObject;
                return true;
            }
            RoClickToMoveController clickController = ResolveClickToMoveController();
            if (clickController == null) {
                return false;
            }

            RoPointerTarget target = clickController.CurrentPointerTarget;
            if (!target.IsValid ||
                target.Kind == RoPointerTargetKind.None ||
                target.Kind == RoPointerTargetKind.Portal ||
                target.GameObject == null) {
                return false;
            }

            RoOnlinePointerTargetMarker marker = target.GameObject.GetComponentInParent<RoOnlinePointerTargetMarker>();
            if (marker == null || marker.ObjectType == RoMapActorObjectType.Warp) {
                return false;
            }

            RoOnlineWorldController controller = ResolveWorldController(marker);
            if (controller == null || !controller.State.TryGetActor(marker.ServerId, out actor)) {
                return false;
            }

            targetObject = target.GameObject;
            anchorWorldPosition = target.WorldPosition;
            RoOnlineActorPresenter presenter = controller.ActorPresenter;
            if (presenter != null &&
                presenter.TryGetPresentedActorTransform(marker.ServerId, out Transform actorTransform) &&
                actorTransform != null) {
                anchorWorldPosition = actorTransform.position;
            }

            return true;
        }

        private bool TryResolveLabelScreenAnchor(
            UnityEngine.Camera camera,
            GameObject targetObject,
            Vector3 fallbackWorldPosition,
            out Vector3 screen
        ) {
            screen = camera.WorldToScreenPoint(fallbackWorldPosition);
            if (screen.z <= 0f) {
                return false;
            }

            if (targetObject != null &&
                TryCalculateRenderableScreenBounds(camera, targetObject.transform, out Rect bounds)) {
                screen = new Vector3(bounds.center.x, bounds.yMin, screen.z);
            }

            return true;
        }

        private bool TryCalculateRenderableScreenBounds(
            UnityEngine.Camera camera,
            Transform root,
            out Rect screenBounds
        ) {
            screenBounds = default;
            if (camera == null || root == null) {
                return false;
            }

            if (TryUseCachedRenderableScreenBounds(camera, root, out screenBounds)) {
                return true;
            }

            rendererBuffer.Clear();
            root.GetComponentsInChildren(true, rendererBuffer);
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            bool hasPoint = false;
            for (int i = 0; i < rendererBuffer.Count; i++) {
                Renderer renderer = rendererBuffer[i];
                if (!ShouldUseLabelAnchorRenderer(renderer)) {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                for (int x = 0; x <= 1; x++) {
                    for (int y = 0; y <= 1; y++) {
                        for (int z = 0; z <= 1; z++) {
                            Vector3 corner = new Vector3(
                                x == 0 ? min.x : max.x,
                                y == 0 ? min.y : max.y,
                                z == 0 ? min.z : max.z);
                            Vector3 screen = camera.WorldToScreenPoint(corner);
                            if (screen.z <= 0f) {
                                continue;
                            }

                            hasPoint = true;
                            minX = Mathf.Min(minX, screen.x);
                            minY = Mathf.Min(minY, screen.y);
                            maxX = Mathf.Max(maxX, screen.x);
                            maxY = Mathf.Max(maxY, screen.y);
                        }
                    }
                }
            }

            rendererBuffer.Clear();
            if (!hasPoint) {
                return false;
            }

            screenBounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            StoreCachedRenderableScreenBounds(camera, root, screenBounds);
            return true;
        }

        private bool TryUseCachedRenderableScreenBounds(
            UnityEngine.Camera camera,
            Transform root,
            out Rect screenBounds
        ) {
            if (cachedBoundsValid &&
                cachedBoundsRoot == root &&
                cachedBoundsCamera == camera &&
                Time.unscaledTime <= cachedBoundsUntilTime &&
                cachedBoundsRootPosition == root.position &&
                cachedBoundsRootRotation == root.rotation &&
                cachedBoundsRootScale == root.lossyScale &&
                cachedBoundsCameraPosition == camera.transform.position &&
                cachedBoundsCameraRotation == camera.transform.rotation) {
                screenBounds = cachedBounds;
                return true;
            }

            screenBounds = default;
            return false;
        }

        private void StoreCachedRenderableScreenBounds(UnityEngine.Camera camera, Transform root, Rect bounds) {
            cachedBoundsRoot = root;
            cachedBoundsCamera = camera;
            cachedBounds = bounds;
            cachedBoundsRootPosition = root.position;
            cachedBoundsRootRotation = root.rotation;
            cachedBoundsRootScale = root.lossyScale;
            cachedBoundsCameraPosition = camera.transform.position;
            cachedBoundsCameraRotation = camera.transform.rotation;
            cachedBoundsUntilTime = Time.unscaledTime + RenderableBoundsCacheLifetimeSeconds;
            cachedBoundsValid = true;
        }

        private static bool ShouldUseLabelAnchorRenderer(Renderer renderer) {
            return renderer != null &&
                renderer.enabled &&
                renderer.FindComponent<TextMesh>() == null &&
                renderer.GetComponentInParent<RoActorShadowVisual>() == null;
        }

        private void TryRequestIdentity(RoOnlineActorSnapshot actor) {
            if (actor.HasResolvedIdentity ||
                actor.ServerId == 0 ||
                actor.ObjectType == RoMapActorObjectType.Item ||
                actor.ObjectType == RoMapActorObjectType.Warp ||
                actor.ObjectType == RoMapActorObjectType.Effect) {
                return;
            }

            float now = Time.unscaledTime;
            if (nextIdentityRequestTimeByActorId.TryGetValue(actor.ServerId, out float nextTime) && now < nextTime) {
                return;
            }

            nextIdentityRequestTimeByActorId[actor.ServerId] = now + IdentityRequestIntervalSeconds;
            ResolveLoginShell()?.TrySendActorNameRequest(actor.ServerId);
        }

        private RoOnlineWorldController ResolveWorldController(RoOnlinePointerTargetMarker marker) {
            if (worldController != null) {
                return worldController;
            }

            worldController = marker != null ? marker.GetComponentInParent<RoOnlineWorldController>() : null;
            if (worldController == null) {
                float now = Time.unscaledTime;
                if (now < nextWorldControllerLookupTime) {
                    return null;
                }

                worldController = FindFirstObjectByType<RoOnlineWorldController>();
                nextWorldControllerLookupTime = worldController != null
                    ? 0f
                    : now + DependencyLookupRetrySeconds;
            } else {
                nextWorldControllerLookupTime = 0f;
            }

            return worldController;
        }

        private RoClickToMoveController ResolveClickToMoveController() {
            if (clickToMoveController == null) {
                float now = Time.unscaledTime;
                if (now < nextClickToMoveLookupTime) {
                    return null;
                }

                clickToMoveController = FindFirstObjectByType<RoClickToMoveController>();
                nextClickToMoveLookupTime = clickToMoveController != null
                    ? 0f
                    : now + DependencyLookupRetrySeconds;
            }

            return clickToMoveController;
        }

        private RoMinimalLoginShell ResolveLoginShell() {
            if (loginShell == null) {
                float now = Time.unscaledTime;
                if (now < nextLoginShellLookupTime) {
                    return null;
                }

                loginShell = FindFirstObjectByType<RoMinimalLoginShell>();
                nextLoginShellLookupTime = loginShell != null
                    ? 0f
                    : now + DependencyLookupRetrySeconds;
            }

            return loginShell;
        }

        private RoItemMetadataTable ResolveItemMetadata() {
            RoRuntimeMapController mapController = ResolveRuntimeMapController();
            if (mapController == null) {
                itemMetadataSourceMapController = null;
                itemMetadata = RoItemMetadataTable.Empty;
                return itemMetadata;
            }

            if (ReferenceEquals(itemMetadataSourceMapController, mapController)) {
                return itemMetadata;
            }

            itemMetadataSourceMapController = mapController;
            itemMetadata = mapController.Content != null && mapController.Content.Assets != null
                ? mapController.Content.Assets.GameData?.Items ?? RoItemMetadataTable.Empty
                : RoItemMetadataTable.Empty;
            return itemMetadata;
        }

        private RoRuntimeMapController ResolveRuntimeMapController() {
            if (worldController != null && worldController.RuntimeMapController != null) {
                runtimeMapController = worldController.RuntimeMapController;
                nextRuntimeMapControllerLookupTime = 0f;
                return runtimeMapController;
            }

            if (runtimeMapController != null) {
                return runtimeMapController;
            }

            float now = Time.unscaledTime;
            if (now < nextRuntimeMapControllerLookupTime) {
                return null;
            }

            runtimeMapController = FindFirstObjectByType<RoRuntimeMapController>();
            nextRuntimeMapControllerLookupTime = runtimeMapController != null
                ? 0f
                : now + DependencyLookupRetrySeconds;
            return runtimeMapController;
        }

        private UnityEngine.Camera ResolveCamera() {
            if (renderCamera != null) {
                return renderCamera;
            }

            renderCamera = UnityEngine.Camera.main;
            return renderCamera;
        }

        private void EnsureStyles() {
            if (primaryStyle != null) {
                return;
            }

            primaryStyle = new GUIStyle(GUI.skin.label) {
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                clipping = TextClipping.Overflow,
                wordWrap = false
            };
            secondaryStyle = new GUIStyle(primaryStyle) {
                fontStyle = FontStyle.Normal,
                fontSize = 11
            };
            outlineStyle = new GUIStyle(primaryStyle) {
                normal = { textColor = OutlineColor }
            };
        }

        private static void ApplyTextColorToAllStates(GUIStyle style, Color color) {
            if (style == null) {
                return;
            }

            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private void DrawOutlinedLabel(Rect rect, GUIContent content, GUIStyle style) {
            outlineStyle.fontSize = style.fontSize;
            outlineStyle.fontStyle = style.fontStyle;
            DrawLabel(new Rect(rect.x - 1f, rect.y, rect.width, rect.height), content, outlineStyle);
            DrawLabel(new Rect(rect.x + 1f, rect.y, rect.width, rect.height), content, outlineStyle);
            DrawLabel(new Rect(rect.x, rect.y - 1f, rect.width, rect.height), content, outlineStyle);
            DrawLabel(new Rect(rect.x, rect.y + 1f, rect.width, rect.height), content, outlineStyle);
            DrawLabel(rect, content, style);
        }

        private static void DrawLabel(Rect rect, GUIContent content, GUIStyle style) {
            GUI.Label(rect, content, style);
        }
    }
}
