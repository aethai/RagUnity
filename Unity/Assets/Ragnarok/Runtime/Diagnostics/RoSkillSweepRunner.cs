using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Ragnarok.Network;
using Ragnarok.UI;
using Ragnarok.World;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Ragnarok.Diagnostics {
    /// <summary>
    /// Automated in-game skill sweep for the local lab server (docker/hercules-lab, Admin group with all_skill +
    /// skill_unconditional). Dormant unless <c>Temp/ro-skill-sweep.json</c> exists when play mode starts: then it
    /// logs in, enters the map with the plan's character, and for every skill of the plan spawns / approaches a
    /// target, casts the skill, records a short clip through ffmpeg (raw frames piped, nothing written per frame)
    /// and appends what happened (skill packets, launched effect ids and their engine, failures, classic draw
    /// counters) to <c>report.jsonl</c> in the plan's output directory.
    /// </summary>
    public sealed partial class RoSkillSweepRunner : MonoBehaviour {
        const string TriggerFileName = "ro-skill-sweep.json";
        const string ConsoleFileName = "ro-skill-sweep.cmd";
        const int SkillIntervalFailCause = 4;

#pragma warning disable 0649 // JsonUtility DTOs
        [Serializable]
        sealed class SweepSkill {
            public int id;
            public string code;
            public string name;
            public int level;
            public string mode;
            public bool reset;
            public bool rehome;
            /// <summary>Play the server's packets for this skill client-side instead of asking the server
            /// (InjectFeedbackEventForDiagnostics): the client's presentation of a use the lab cannot trigger.</summary>
            public bool inject;
            public int castMs;
            /// <summary>Ground unit type (ZC_SKILL_ENTRY job) the injected use places on the cell, 0 = none.</summary>
            public int unitType;
            /// <summary>The packets of an injected use after the cast (from the original client's effect table):
            /// ZC_NOTIFY_SKILL, ZC_USE_SKILL, ZC_NOTIFY_GROUNDSKILL. None set = by mode.</summary>
            public bool injectDamage, injectResult, injectGround;
            /// <summary>Property of the injected cast (picks the begin-cast effect by element).</summary>
            public int element;
            /// <summary>The injected unit is a trap that fires: ZC_SPRITE_CHANGE to UNT_USED_TRAPS after a moment.</summary>
            public bool triggerTrap;
            /// <summary>Cast by the second client (plan.botCommandFile) on the caster's side: a remote player's use.</summary>
            public bool remote;
        }

        [Serializable]
        sealed class SweepPlan {
            public string account;
            public string password;
            public string character;
            public string outputDir;
            public string ffmpeg;
            public int fps = 20;
            public int width = 800;
            public bool flipVertical;
            public bool record = true;
            public float mapSettleSeconds = 8f;
            public float preRollSeconds = 0.4f;
            public float postSeconds = 4f;
            public float failPostSeconds = 1f;
            public float maxCastSeconds = 12f;
            public int targetMobId = 1008;
            /// <summary>Character the "friend" skills target (the lab's second client, scripts/labbot); the caster
            /// itself when it is not on screen.</summary>
            public string friendName = "FxBuddy";
            /// <summary>Command file of the lab's second client (scripts/labbot --command-file): "remote" skills are cast
            /// by it, so the sweep sees another player's use.</summary>
            public string botCommandFile;
            public string[] setupCommands;
            public string[] resetCommands;
            public int startIndex;
            public int count = -1;
            public bool exitPlayModeWhenDone = true;
            public bool classicUi;
            /// <summary>The login is left to the (classic) login window, driven from the console (!pregame).</summary>
            public bool manualLogin;
            public SweepSkill[] skills;
        }

#pragma warning restore 0649

        [Serializable]
        sealed class SweepEntry {
            public int index;
            public int id;
            public string code;
            public string name;
            public int level;
            public string mode;
            public int targetAid;
            public int groundX;
            public int groundY;
            public int selfX;
            public int selfY;
            public int targetX;
            public int targetY;
            public int sendAttempts;
            public float castSeconds;
            public float clipSeconds;
            public int frames;
            public string clip;
            public string outcome;
            public int classicMaxEffects;
            public int classicMaxPrims;
            public int classicMaxBatches;
            public int classicMaxVertices;
            public int classicDrawnFrames;
            public List<string> events = new List<string>();
        }

        static readonly Vector2Int[] AdjacentOffsets = {
            new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1),
            new Vector2Int(-1, -1), new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1),
        };

        /// <summary>
        /// The server refuses a ground skill on a blocked cell (flower beds, the fountain: USESKILL_FAIL_LEVEL), so the
        /// ground target moves to the nearest walkable cell (rings of growing Chebyshev radius, up to 4).
        /// </summary>
        void SnapGroundToWalkable(ref int x, ref int y) {
            Ragnarok.Maps.RoGridSelector grid = world != null && world.RuntimeMapController != null
                ? world.RuntimeMapController.GridSelector
                : null;
            if (grid == null || grid.IsCellWalkable(new Vector2Int(x, y))) {
                return;
            }

            for (int r = 1; r <= 4; r++) {
                for (int dy = -r; dy <= r; dy++) {
                    for (int dx = -r; dx <= r; dx++) {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) == r && grid.IsCellWalkable(new Vector2Int(x + dx, y + dy))) {
                            x += dx;
                            y += dy;
                            return;
                        }
                    }
                }
            }
        }

        static readonly Vector2Int[] GroundOffsets = {
            new Vector2Int(0, 0), new Vector2Int(0, 2), new Vector2Int(2, 0), new Vector2Int(0, -2), new Vector2Int(-2, 0),
            new Vector2Int(2, 2), new Vector2Int(-2, -2), new Vector2Int(2, -2), new Vector2Int(-2, 2),
        };

        string planPath;
        SweepPlan plan;
        RoMinimalLoginShell shell;
        RoOnlineWorldController world;
        ClipRecorder recorder;
        bool captureEveryFrame;
        // the !clip frame log: one line per captured frame (f_NNNN.png), with the carts' state that frame
        StreamWriter clipFrameLog;
        int clipFrameIndex;
        StreamWriter log;
        readonly List<RoOnlineActorSnapshot> actors = new List<RoOnlineActorSnapshot>();
        int groundCounter;
        Vector2Int home;
        bool hasHome;
        bool rehomeMovesClient = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() {
            // the console drives the client from a file (chat, GM commands, clicks, logins): a ready-made bot
            // interface, so a release build never starts it
            if (!Application.isEditor && !Debug.isDebugBuild) {
                return;
            }

            string trigger = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", TriggerFileName));
            if (!File.Exists(trigger)) {
                return;
            }

            // consume the trigger right away: a crash or a normal play session afterwards must not re-run the sweep
            string running = Path.ChangeExtension(trigger, ".running.json");
            if (File.Exists(running)) {
                File.Delete(running);
            }

            File.Move(trigger, running);
            var go = new GameObject("RO skill sweep");
            DontDestroyOnLoad(go);
            go.AddComponent<RoSkillSweepRunner>().planPath = running;
        }

        void Start() {
            try {
                plan = JsonUtility.FromJson<SweepPlan>(File.ReadAllText(planPath));
                Directory.CreateDirectory(plan.outputDir);
                log = new StreamWriter(Path.Combine(plan.outputDir, "sweep.log"), append: true) { AutoFlush = true };
            } catch (Exception e) {
                Debug.LogError("[SkillSweep] bad plan " + planPath + ": " + e);
                enabled = false;
                return;
            }

            RoSkillSweepProbe.Enabled = true;
            // explicit either way: the player's preference must not leak into sweeps
            StartCoroutine(Run());
            StartCoroutine(CaptureLoop());
        }

        float nextConsolePoll;
        readonly Queue<string> consoleLines = new Queue<string>();

        /// <summary>
        /// Remote console for a running sweep: every line of Temp/ro-skill-sweep.cmd is sent as chat (GM commands),
        /// except <c>!status</c> (logs own state, the shell status and the last packets) and
        /// <c>!skill &lt;id&gt; &lt;level&gt; &lt;enemy|ground|self|friend&gt;</c> (one sweep step).
        /// </summary>
        void Update() {
            PumpPendingSpam();
            if (Now < nextConsolePoll) return;
            nextConsolePoll = Now + 0.8f;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", ConsoleFileName));
            if (File.Exists(path)) {
                try {
                    foreach (string raw in File.ReadAllLines(path)) {
                        if (raw.Trim().Length > 0) consoleLines.Enqueue(raw.Trim());
                    }

                    File.Delete(path);
                } catch (IOException) {
                    // being written; next poll
                }
            }

            if (consoleLines.Count > 0) {
                string line = consoleLines.Dequeue();
                if (shell == null) shell = FindFirstObjectByType<RoMinimalLoginShell>();
                if (world == null) world = FindFirstObjectByType<RoOnlineWorldController>();
                Log("console: " + line);
                if (line == "!status") {
                    LogStatus();
                } else if (line.StartsWith("!classic", StringComparison.Ordinal)) {
                    // !classic [texture name]: the classic engine's live state (ground scopes) and a texture lookup
                    Ragnarok.Effects.Classic.RoClassicEffectBridge bridge = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance;
                    Log(bridge == null ? "classic: no bridge" : "classic: " + bridge.DescribeState());
                    string tex = line.Length > 9 ? line.Substring(9).Trim() : "effect\\magic_target.tga";
                    if (bridge != null) {
                        bool found = bridge.TryGetTexture(tex, out uint handle, out int w, out int h, out _, out _);
                        Log($"classic texture '{tex}': found={found} handle={handle} size={w}x{h}");
                    }
                } else if (line.StartsWith("!fxscreen ", StringComparison.Ordinal)) {
                    // !fxscreen <needle>: screen position (y from the top) of every renderer whose path contains the needle
                    string needle = line.Substring(10).Trim();
                    UnityEngine.Camera cam = UnityEngine.Camera.main;
                    var text = new System.Text.StringBuilder();
                    foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
                        if (cam == null || GetPath(r.transform).IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        Vector3 sp = cam.WorldToScreenPoint(r.bounds.center);
                        text.Append("\n  ").Append(GetPath(r.transform)).Append(" enabled=").Append(r.enabled).Append(" visible=").Append(r.isVisible)
                            .Append(" screen=").Append(sp.x.ToString("F0")).Append(",").Append((Screen.height - sp.y).ToString("F0")).Append(" z=").Append(sp.z.ToString("F1"));
                    }

                    Log("fxscreen " + needle + ":" + text);
                } else if (line.StartsWith("!hidego ", StringComparison.Ordinal)) {
                    // !hidego <needle>: deactivates the GameObjects of renderers whose path contains the needle (owners
                    // that re-enable their renderers every frame stay hidden this way)
                    string needle = line.Substring(8).Trim();
                    var hiddenObjects = new HashSet<GameObject>();
                    foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
                        if (GetPath(r.transform).IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        hiddenObjects.Add(r.gameObject);
                    }

                    foreach (GameObject go in hiddenObjects) go.SetActive(false);
                    Log("hidego " + needle + ": " + hiddenObjects.Count + " objects");
                } else if (line.StartsWith("!hide ", StringComparison.Ordinal)) {
                    // !hide <needle>|*: disables the renderers whose path contains the needle (diagnostics bisection)
                    string needle = line.Substring(6).Trim();
                    int hidden = 0;
                    foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
                        if (!r.enabled || (needle != "*" && GetPath(r.transform).IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                        r.enabled = false;
                        hidden++;
                    }

                    Log("hide " + needle + ": " + hidden + " renderers");
                } else if (line.StartsWith("!fxpick ", StringComparison.Ordinal)) {
                    // !fxpick <x> <y>: visible renderers whose screen rect covers that pixel (y from the top, like !shot)
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var camNames = new System.Text.StringBuilder();
                    foreach (UnityEngine.Camera c in UnityEngine.Camera.allCameras) camNames.Append(c.name).Append(c == UnityEngine.Camera.main ? "(main)" : "").Append(" d").Append(c.depth).Append(" tt=").Append(c.targetTexture != null ? c.targetTexture.width + "x" + c.targetTexture.height : "screen").Append("; ");
                    Log("fxpick cameras: " + camNames);
                    UnityEngine.Camera cam = p.Length > 3 ? System.Array.Find(UnityEngine.Camera.allCameras, c => c.name == p[3]) : UnityEngine.Camera.main;
                    // screenshot pixels -> the camera's pixel rect (the world camera may render at a different scale)
                    float px = float.Parse(p[1], CultureInfo.InvariantCulture) / Screen.width * (cam != null ? cam.pixelWidth : 1);
                    float py = (Screen.height - float.Parse(p[2], CultureInfo.InvariantCulture)) / Screen.height * (cam != null ? cam.pixelHeight : 1);
                    var hits = new List<(float area, string text)>();
                    foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
                        if (cam == null || !r.enabled || !r.isVisible) continue;
                        Bounds b = r.bounds;
                        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                        for (int k = 0; k < 8; k++) {
                            Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                            Vector3 s = cam.WorldToScreenPoint(corner);
                            if (s.z <= 0f) continue;
                            minX = Mathf.Min(minX, s.x); maxX = Mathf.Max(maxX, s.x); minY = Mathf.Min(minY, s.y); maxY = Mathf.Max(maxY, s.y);
                        }

                        if (px < minX || px > maxX || py < minY || py > maxY) continue;
                        Material m = r.sharedMaterial;
                        Texture t = m != null && m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m != null && m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                        hits.Add(((maxX - minX) * (maxY - minY), r.GetType().Name + " " + GetPath(r.transform) + " at " + b.center.ToString("F1") +
                            " size " + b.size.ToString("F1") + " mat=" + (m != null ? m.name + "/" + m.shader.name : "-") + " tex=" + (t != null ? t.name : "-")));
                    }

                    hits.Sort((a, b2) => a.area.CompareTo(b2.area));
                    var pick = new System.Text.StringBuilder();
                    for (int k = 0; k < hits.Count && k < 15; k++) pick.Append("\n  ").Append(hits[k].text);
                    Log("fxpick " + p[1] + "," + p[2] + " (screen " + Screen.width + "x" + Screen.height + ", camera " + (cam != null ? cam.pixelWidth + "x" + cam.pixelHeight + " " + cam.name : "none") + "): " + hits.Count + " hits, smallest first:" + pick);
                } else if (line.StartsWith("!drawstats", StringComparison.Ordinal)) {
                    // !drawstats: the visible renderers grouped by kind, with their draws, materials and shaders
                    Log("drawstats:" + DescribeDrawStats());
                } else if (line.StartsWith("!abdepth ", StringComparison.Ordinal)) {
                    // !abdepth <name> [single]: the frame with and without the sprite depth prepass (RoSpriteDepthModeProbe);
                    // single = every layer in two passes against the one-pass layers (the two must not differ)
                    string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PerformanceCaptures", "ui", "shots"));
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    bool single = p.Length > 2 && p[2] == "single";
                    Log("abdepth " + p[1] + (single ? " single" : "") + ": " + RoSpriteDepthModeProbe.Compare(p[1], dir, single));
                } else if (line.StartsWith("!spritecut", StringComparison.Ordinal)) {
                    // !spritecut [cut] [bias]: the actor layers' depth cut / pull (−1 = keep) and their vertex alpha, feet, shader
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    Log("spritecut: " + Ragnarok.Entities.RoClientDrawOrder.DiagnoseDepthMaterials(
                        p.Length > 1 ? float.Parse(p[1], CultureInfo.InvariantCulture) : -1f,
                        p.Length > 2 ? float.Parse(p[2], CultureInfo.InvariantCulture) : -1f));
                } else if (line.StartsWith("!fxdepth ", StringComparison.Ordinal)) {
                    // !fxdepth <x> <y>: effect batches and actor layers at that pixel (y from the top, like !shot) with their
                    // draw order and depths (RoClassicEffectBridge.DescribeDepthAt)
                    string[] p = line.Split(' ');
                    Ragnarok.Effects.Classic.RoClassicEffectBridge bridge = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance;
                    Log(bridge == null ? "fxdepth: no bridge" : bridge.DescribeDepthAt(
                        float.Parse(p[1], CultureInfo.InvariantCulture), float.Parse(p[2], CultureInfo.InvariantCulture)));
                } else if (line.StartsWith("!fxcover ", StringComparison.Ordinal)) {
                    // !fxcover <x> <y> [...]: the effect triangles covering each pixel (y from the top) with the effect / prim
                    // that drew them (RoClassicEffectBridge.DescribeCoverAt; traces the sources for one frame)
                    StartCoroutine(RecordFxCover(line));
                } else if (line.StartsWith("!msgdump", StringComparison.Ordinal)) {
                    // !msgdump: the live damage numbers / messages (CMsgEffect glyphs) and where they are drawn
                    Ragnarok.Effects.Classic.RoClassicEffectBridge bridge = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance;
                    Log(bridge == null ? "msgdump: no bridge" : "msgdump: " + bridge.DescribeMsgEffects());
                } else if (line.StartsWith("!fxkinds ", StringComparison.Ordinal)) {
                    // !fxkinds <seconds>: ms a frame of the classic engine's steps by kind (effect / prim / new system, process
                    // and render; an effect's render is its STR layers) and the costliest effect ids and prim types
                    StartCoroutine(FxKinds(float.Parse(line.Substring(9).Trim(), CultureInfo.InvariantCulture)));
                } else if (line.StartsWith("!fxdump", StringComparison.Ordinal)) {
                    // !fxdump [radius]: running classic effects, then every other visible renderer near the caster
                    Ragnarok.Effects.Classic.RoClassicEffectBridge bridge = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance;
                    Log(bridge == null ? "fxdump: no bridge" : "fxdump classic: " + bridge.DescribeEffects());
                    // !fxdump <radius> [needle]: with a needle, every visible renderer whose path contains it (any size)
                    string[] args = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    float radius = args.Length > 1 ? float.Parse(args[1], CultureInfo.InvariantCulture) : 6f;
                    string needle = args.Length > 2 ? args[2] : null;
                    UnityEngine.Camera cam = UnityEngine.Camera.main;
                    Vector3 centre = cam != null ? cam.transform.position + cam.transform.forward * 20f : Vector3.zero;
                    if (cam != null && new Plane(Vector3.up, Vector3.zero).Raycast(new Ray(cam.transform.position, cam.transform.forward), out float hit)) {
                        centre = cam.transform.position + cam.transform.forward * hit;
                    }

                    var near = new System.Text.StringBuilder();
                    foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
                        if (!r.enabled || !r.isVisible) continue;
                        Vector3 c = r.bounds.center;
                        if (needle != null) {
                            if (GetPath(r.transform).IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        } else if (new Vector2(c.x - centre.x, c.z - centre.z).magnitude > radius || r.bounds.size.magnitude > 60f ||
                                   r.name.StartsWith("RO Actor Layer", StringComparison.Ordinal) || GetPath(r.transform).Contains("/RO Models/")) {
                            continue;
                        }
                        Material m = r.sharedMaterial;
                        Texture t = m != null && m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m != null && m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                        near.Append("\n  ").Append(r.GetType().Name).Append(' ').Append(GetPath(r.transform))
                            .Append(" at ").Append(c.ToString("F1")).Append(" size ").Append(r.bounds.size.ToString("F1"))
                            .Append(" mat=").Append(m != null ? m.name + "/" + m.shader.name : "-").Append(" tex=").Append(t != null ? t.name : "-");
                    }

                    Log("fxdump renderers within " + radius + " of " + centre.ToString("F1") + ":" + near);
                } else if (line.StartsWith("!fxbridge ", StringComparison.Ordinal)) {
                    // !fxbridge <seconds>: ms a frame in the classic effect bridge's face / quad / sprite callbacks (part of
                    // RO.ClassicFx.Render) and how many calls
                    StartCoroutine(RecordFxBridge(float.Parse(line.Substring(10).Trim(), CultureInfo.InvariantCulture)));
                } else if (line.StartsWith("!markers ", StringComparison.Ordinal)) {
                    // !markers <seconds> <marker,marker,...>: the average ms a frame (and calls a frame) of those profiler
                    // markers over that time, in a development player too (ProfilerRecorder); e.g.
                    // !markers 10 RO.ClassicFx.Advance,RO.ClassicFx.Render,RO.ClassicFx.Draw.Flush
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length > 2) {
                        StartCoroutine(RecordMarkers(float.Parse(p[1], CultureInfo.InvariantCulture), p[2].Split(',')));
                    }
                } else if (line.StartsWith("!uiperf", StringComparison.Ordinal)) {
                    // !uiperf <seconds> <label>: UI frame-cost capture (RoUiPerfProbe)
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    float seconds = p.Length > 1 ? float.Parse(p[1], CultureInfo.InvariantCulture) : 20f;
                    string outPath = RoUiPerfProbe.Begin(seconds, p.Length > 2 ? p[2] : "capture");
                    Log(outPath == null ? "uiperf: already running" : "uiperf: recording " + seconds + "s -> " + outPath);
                } else if (line.StartsWith("!perf", StringComparison.Ordinal)) {
                    // !perf <seconds> <label>: the full RoPerfCapture (its UI/shell counters) into PerformanceCaptures/<label>
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    float seconds = p.Length > 1 ? float.Parse(p[1], CultureInfo.InvariantCulture) : 15f;
                    string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PerformanceCaptures",
                        DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + "_" + (p.Length > 2 ? p[2] : "sweep")));
                    Directory.CreateDirectory(dir);
                    Log(RoPerfCapture.BeginCapture(seconds, dir) != null ? "perf: recording " + seconds + "s -> " + dir : "perf: busy");

                } else if (line.StartsWith("!shot ", StringComparison.Ordinal)) {
                    // !shot <name>: game view screenshot into PerformanceCaptures/ui/shots/<name>.png
                    string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PerformanceCaptures", "ui", "shots"));
                    Directory.CreateDirectory(dir);
                    string shot = Path.Combine(dir, line.Substring(6).Trim() + ".png");
                    ScreenCapture.CaptureScreenshot(shot);
                    Log("shot: " + shot);
                } else if (line.StartsWith("!shotburst ", StringComparison.Ordinal)) {
                    // !shotburst <name> <frames> [x y w h]: a crop (y from the top) of every rendered frame for <frames>
                    // frames into PerformanceCaptures/ui/shots/<name>_NNN.png (one-frame flicker shows only frame by frame)
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    StartCoroutine(ShotBurst(p[1], int.Parse(p[2], CultureInfo.InvariantCulture),
                        p.Length > 6 ? new RectInt(int.Parse(p[3], CultureInfo.InvariantCulture), int.Parse(p[4], CultureInfo.InvariantCulture),
                            int.Parse(p[5], CultureInfo.InvariantCulture), int.Parse(p[6], CultureInfo.InvariantCulture)) : new RectInt(0, 0, 0, 0)));
                } else if (line.StartsWith("!hover", StringComparison.Ordinal)) {
                    // !hover <aid> | !hover mob | !hover off: forces the hover label onto an actor
                    string arg = line.Length > 7 ? line.Substring(7).Trim() : "mob";
                    uint aid = 0;
                    if (arg == "mob" || arg == "any") {
                        if (world == null) world = FindFirstObjectByType<RoOnlineWorldController>();
                        if (world != null && TryGetSelf(out RoOnlineActorSnapshot me)) {
                            world.SnapshotActors(actors);
                            int best = int.MaxValue;
                            foreach (RoOnlineActorSnapshot a in actors) {
                                if (a.IsSelf || (arg == "mob" && a.ObjectType != RoMapActorObjectType.Monster)) continue;
                                int d = Math.Abs(a.CellX - me.CellX) + Math.Abs(a.CellY - me.CellY);
                                if (d < best) {
                                    best = d;
                                    aid = a.ServerId;
                                }
                            }
                        }
                    } else if (arg != "off") {
                        uint.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out aid);
                    }

                    RoActorHoverLabelOverlay.DebugHoverActorId = aid;
                    Log("hover: " + aid);
                } else if (line.StartsWith("!hkbar ", StringComparison.Ordinal)) {
                    // !hkbar <rows 0..4>: shows the hotkey bar with that many rows
                    bool bar = shell != null && shell.SweepEnsureHotkeyBar();
                    int rows = int.Parse(line.Substring(7).Trim(), CultureInfo.InvariantCulture);
                    if (bar) Ragnarok.UI.RoClassicHotkeyBar.SweepSetVisibleRows(rows);

                    Log("hkbar: " + (bar ? rows.ToString(CultureInfo.InvariantCulture) : "not found"));
                } else if (line.StartsWith("!quick", StringComparison.Ordinal)) {
                    // !quick drop i item id | drop i skill id lv | use i | clear i | describe i | state (classic quick slots 0x9f)
                    shell?.SweepEnsureHotkeyBar();
                    Log("quick: " + Ragnarok.UI.RoClassicHotkeyBar.SweepQuickSlots(line.Substring(6)));
                } else if (line.StartsWith("!hk ", StringComparison.Ordinal)) {
                    // !hk <slot> skill|item <id> [level]: assigns a hotkey through the hotkey bar (server request)
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    bool bar = shell != null && shell.SweepEnsureHotkeyBar();
                    int slot = int.Parse(p[1], CultureInfo.InvariantCulture);
                    bool isSkill = p[2] == "skill";
                    uint hotkeyId = uint.Parse(p[3], CultureInfo.InvariantCulture);
                    short count = (short) (p.Length > 4 ? int.Parse(p[4], CultureInfo.InvariantCulture) : (isSkill ? 1 : 0));
                    if (bar) _ = Ragnarok.UI.RoClassicHotkeyBar.SweepSetShortcut(slot, new RoShortcutKeyEntry((sbyte) (isSkill ? 1 : 0), hotkeyId, count));
                    Log("hk: " + slot + " " + p[2] + " " + hotkeyId + (bar ? string.Empty : " (no bar)"));
                } else if (line.StartsWith("!cmenu ", StringComparison.Ordinal)) {
                    // !cmenu open|close: the classic UIMenuIconWnd under Basic Info
                    bool open = line.EndsWith("open", StringComparison.Ordinal);
                    Ragnarok.UI.RoClassicBasicInfo.SetClassicMenuExpanded(open);
                    Log("classic menu: " + (Ragnarok.UI.RoClassicBasicInfo.IsBound ? (open ? "open" : "closed") : "no basic info"));
                } else if (line.StartsWith("!uiscale ", StringComparison.Ordinal)) {
                    // !uiscale <scale>|auto: the general UI scale setting (manual value, or back to automatic)
                    string arg = line.Substring(9).Trim();
                    if (arg == "auto") {
                        Ragnarok.UI.RoClassicUiScale.ResetToAuto();
                        Log("uiscale: auto -> " + Ragnarok.UI.RoClassicUiScale.EffectiveScale.ToString(CultureInfo.InvariantCulture));
                    } else if (float.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out float scale)) {
                        Ragnarok.UI.RoClassicUiScale.SetManualScale(scale);
                        Log("uiscale: " + Ragnarok.UI.RoClassicUiScale.EffectiveScale.ToString(CultureInfo.InvariantCulture));
                    } else {
                        Log("uiscale: " + Ragnarok.UI.RoClassicUiScale.EffectiveScale.ToString(CultureInfo.InvariantCulture) + " (usage: !uiscale <scale>|auto)");
                    }
                } else if (line.StartsWith("!screen", StringComparison.Ordinal)) {
                    // !screen: the screen, the main camera's pixel rect, the classic client size and UI scale
                    UnityEngine.Camera cam = UnityEngine.Camera.main;
                    var cui = Ragnarok.UI.Classic.RoClassicUiSystem.Instance;
                    Log("screen: " + Screen.width + "x" + Screen.height + " cam=" + (cam != null ? cam.name + " " + cam.pixelRect + " rect=" + cam.rect : "-") +
                        " client=" + (cui != null ? cui.ClientSize.ToString() + " scale=" + cui.Scale.ToString(CultureInfo.InvariantCulture) : "-"));
                    Log("  bgm: " + Ragnarok.Audio.RoAudioSystem.DescribeBgm());
                    if (cui != null) foreach (var w in cui.Windows.Windows) if (w.Visible) Log("  window " + w.Layout.Class + " " + w.Layout.WindowId + " pos=" + w.Position + " size=" + w.Width + "x" + w.Height);
                } else if (line.StartsWith("!trace", StringComparison.Ordinal)) {
                    // !trace on|off|dump [n]: the session's packet trace (both directions) switched live, or its last n entries
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var trace = shell != null && shell.ClassicSession != null ? shell.ClassicSession.TraceBuffer : null;
                    if (trace == null) {
                        Log("trace: no session");
                    } else if (p.Length > 1 && (p[1] == "on" || p[1] == "off")) {
                        trace.IsEnabled = p[1] == "on";
                        Log("trace: " + p[1]);
                    } else {
                        RoPacketTraceEntry[] entries = trace.Snapshot();
                        int n = p.Length > 2 ? int.Parse(p[2], CultureInfo.InvariantCulture) : 30;
                        for (int i = Math.Max(0, entries.Length - n); i < entries.Length; i++) {
                            RoPacketTraceEntry t = entries[i];
                            Log($"  {t.Timestamp:HH:mm:ss.fff} {t.Direction} 0x{t.PacketId:x4} {t.PacketName} len {t.Length} {t.Status} {t.Message}");
                        }

                        Log("trace: " + (trace.IsEnabled ? "on" : "off") + ", " + entries.Length + " entries");
                    }
                } else if (line.StartsWith("!ptr", StringComparison.Ordinal)) {
                    // !ptr [x y]: which gate takes the pointer (Unity screen point; default: the mouse) away from the world
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
                    Vector2 point = p.Length >= 3
                        ? new Vector2(float.Parse(p[1], CultureInfo.InvariantCulture), float.Parse(p[2], CultureInfo.InvariantCulture))
                        : mouse != null ? mouse.position.ReadValue() : Vector2.zero;
                    Log("ptr " + point + ": " + Ragnarok.UI.RoUiPointerCapture.Describe(point));
                } else if (line.StartsWith("!inv ", StringComparison.Ordinal)) {
                    // !inv open|close|tab t|use n|world n|count x|ok|hotkey n slot|width w|desc n|state (classic inventory, cell n of the tab)
                    Log("inv: " + Ragnarok.UI.RoClassicInventory.Sweep(shell != null ? shell.ClassicSession : null, line.Substring(5)));
                } else if (line.StartsWith("!eq ", StringComparison.Ordinal)) {
                    // !eq open|close|tab t|off slot|wear bagIndex|min|ds pick k / dir r / apply|state (classic equipment window)
                    Log("eq: " + Ragnarok.UI.RoClassicEquipment.Sweep(shell != null ? shell.ClassicSession : null, line.Substring(4)));
                } else if (line.StartsWith("!sk ", StringComparison.Ordinal)) {
                    // !sk open|close|tab t|plan id|apply|reset|lvl id ±1|hover id|view|info|hotkey id slot|state (classic skill window)
                    Log("sk: " + Ragnarok.UI.RoClassicSkillTree.Sweep(shell != null ? shell.ClassicSession : null, line.Substring(4)));
                } else if (line.StartsWith("!shop ", StringComparison.Ordinal)) {
                    // !shop buy|sell|cancel|add row [n]|remove row [n]|ok|close|toggle|desc row|talk x y|state (classic NPC shop)
                    Log("shop: " + Ragnarok.UI.RoClassicNpcShop.Sweep(line.Substring(6)));
                } else if (line.StartsWith("!barter", StringComparison.Ordinal)) {
                    // !barter state|select row|add row [n]|remove row [n]|desc row|buy|cancel|esc (classic barter shop 0x14e / 0x14f)
                    Log("barter: " + Ragnarok.UI.RoClassicNpcMarket.SweepBarter(line.Substring(7)));
                } else if (line.StartsWith("!xbarter", StringComparison.Ordinal)) {
                    // !xbarter state|select row|add row [n]|remove row [n]|desc row|buy|cancel|esc (classic expanded barter 0x155 / 0x156)
                    Log("xbarter: " + Ragnarok.UI.RoClassicNpcMarket.SweepExpanded(line.Substring(8)));
                } else if (line.StartsWith("!market", StringComparison.Ordinal)) {
                    // !market state|select row|add row [n]|remove row [n]|desc row|buy|cancel|esc|ok (classic NPC market 0xfe / 0xff / 0x100)
                    Log("market: " + Ragnarok.UI.RoClassicNpcMarket.SweepMarket(line.Substring(7)));
                } else if (line.StartsWith("!mm ", StringComparison.Ordinal)) {
                    // !mm zoom in|zoom out|cycle|icons|state (classic HUD minimap)
                    Log("mm: " + Ragnarok.UI.RoClassicMinimap.Sweep(line.Substring(4)));
                } else if (line.StartsWith("!wm ", StringComparison.Ordinal)) {
                    // !wm open|close|world n|monster|hover x y|click x y|rclick|pick n|groups|state (classic world map 0x8c)
                    Log("wm: " + Ragnarok.UI.RoClassicMinimap.SweepWorldMap(line.Substring(4)));
                } else if (line.StartsWith("!npc ", StringComparison.Ordinal)) {
                    // !npc talk x y|next|close|pick n|cancel|up|down|input text|scroll px|lines text|press x y|state (classic NPC dialog)
                    Log("npc: " + Ragnarok.UI.RoClassicNpcDialog.Sweep(line.Substring(5)));
                } else if (line.StartsWith("!esc ", StringComparison.Ordinal)) {
                    // !esc open|dead|deadres|click id|state (classic Esc menu); !esc key = the real Esc path (RoUiRoot cancel)
                    if (line.Substring(5).Trim() == "key") {
                        Log("esc: key handled=" + (Ragnarok.UI.RoClassicGameUi.Instance != null && Ragnarok.UI.RoClassicGameUi.Instance.TryHandleEscape()));
                    } else {
                        Log("esc: " + Ragnarok.UI.RoClassicEscMenu.Sweep(FindFirstObjectByType<Ragnarok.UI.RoMinimalLoginShell>(), line.Substring(5)));
                    }
                } else if (line.StartsWith("!gs ", StringComparison.Ordinal)) {
                    // !gs open|tab t|press x y|release x y|click x y|wheel x y d|pick i|button id|state (classic Game Settings)
                    Log("gs: " + Ragnarok.UI.RoClassicGameSettings.Sweep(line.Substring(4)));
                } else if (line.StartsWith("!cl ", StringComparison.Ordinal)) {
                    // !cl toggle|press x y|wheel rows|state (classic Command List; the settings of the Game Settings window)
                    var classicSettings = Ragnarok.UI.RoClassicGameSettings.Settings;
                    Log("cl: " + (classicSettings != null ? Ragnarok.UI.RoClassicCommandList.Sweep(line.Substring(4), classicSettings) : "no client settings (enter the game first)"));
                } else if (line.StartsWith("!sc ", StringComparison.Ordinal)) {
                    // !sc open|tab t|press x y|key KeyCode [Alt|Ctrl|Shift|Tab]|button id|state (classic Shortcut Settings)
                    Log("sc: " + Ragnarok.UI.RoClassicShortcutSettings.Sweep(line.Substring(4)));
                } else if (line.StartsWith("!bm ", StringComparison.Ordinal)) {
                    // !bm toggle|press x y|wheel rows|state (classic battle-message filter of the active chat tab)
                    Log("bm: " + Ragnarok.UI.RoClassicBattleMsgOption.Sweep(line.Substring(4), Ragnarok.UI.RoClassicChat.Instance));
                } else if (line.StartsWith("!menu ", StringComparison.Ordinal)) {
                    // !menu open|close|tips (classic menu icons; tips = every icon's balloon text and the close buttons' hotkeys)
                    string menuCommand = line.Substring(6).Trim();
                    if (menuCommand == "open" || menuCommand == "close") Ragnarok.UI.RoClassicBasicInfo.SetClassicMenuExpanded(menuCommand == "open");
                    Func<int, string> hotkey = Ragnarok.UI.Classic.RoClassicUiSystem.WindowHotkeyText;
                    Log("menu: " + Ragnarok.UI.RoClassicBasicInfo.DescribeClassicMenuTooltips() + " || close: equip=" + hotkey?.Invoke(0xa) + " map=" + hotkey?.Invoke(0x8c) + " basicinfo=" + hotkey?.Invoke(0));
                } else if (line == "!sound") {
                    // !sound: the last requested sounds (bus and path)
                    Log("sound: " + Ragnarok.Audio.RoAudioEvents.DescribeRecent());
                } else if (line.StartsWith("!vend ", StringComparison.Ordinal)) {
                    // !vend title t|add mirrorIndex|price row zeny|remove row|ok|cancel|close|state (classic vending windows)
                    if (line.StartsWith("!vend view ", StringComparison.Ordinal)) {
                        shell.SweepRequestPlayerShop(uint.Parse(line.Substring(11).Trim(), CultureInfo.InvariantCulture));
                    }

                    Log("vend: " + Ragnarok.UI.RoClassicVending.Sweep(line.Substring(6)));
                } else if (line.StartsWith("!csk ", StringComparison.Ordinal)) {
                    // !csk open 0|1|pick row|up row|left row|right row|use|state (classic companion skill lists 0x72 / 0x7e)
                    Log("csk: " + Ragnarok.UI.RoClassicCompanionSkills.Sweep(line.Substring(5)));
                } else if (line.StartsWith("!tame ", StringComparison.Ordinal)) {
                    // !tame start|open aid|click|result n|state (classic pet taming 0x5b)
                    Log("tame: " + Ragnarok.UI.RoClassicPetTaming.Sweep(line.Substring(6)));
                } else if (line.StartsWith("!pregame", StringComparison.Ordinal)) {
                    // !pregame state|account t|password t|start|save (classic login 0x3 / wait 0x6)
                    Log("pregame: " + Ragnarok.UI.RoClassicPreGame.Sweep(line.Length > 9 ? line.Substring(9) : "state"));
                } else if (line.StartsWith("!quest ", StringComparison.Ordinal)) {
                    // !quest open|close|tab m|cat n|search t|row n|check n|lock n|detail id|wheel n|state (classic quest log 0x2718)
                    Log("quest: " + Ragnarok.UI.RoClassicQuest.Sweep(line.Substring(7)));
                } else if (line.StartsWith("!qtrack ", StringComparison.Ordinal)) {
                    // !qtrack show|hide|menu y|state (classic quest overlay 0xcc)
                    Log("qtrack: " + Ragnarok.UI.RoClassicQuestTracker.Sweep(line.Substring(8)));
                } else if (line.StartsWith("!rodex ", StringComparison.Ordinal)) {
                    // !rodex inbox|tab n|read row|check row|next|prev|refresh|write name|take items|zeny|delete|validate name|attach bag [n]|send t|b|z|close
                    Log("rodex: " + Ragnarok.UI.RoClassicRodex.Sweep(line.Substring(7)));
                } else if (line.StartsWith("!guild ", StringComparison.Ordinal)) {
                    // !guild open [tab]|tab n|sort c|select row|wheel d|close|state (classic guild manager 0x3c..0x3e)
                    Log("guild: " + Ragnarok.UI.RoClassicGuild.Sweep(line.Substring(7)));
                } else if (line.StartsWith("!hom ", StringComparison.Ordinal)) {
                    // !hom info|merc|feed|delete|fire|rename name|autofeed|state (classic homunculus 0x71 / mercenary 0x7d)
                    Log("hom: " + Ragnarok.UI.RoClassicCompanion.Sweep(line.Substring(5)));
                } else if (line.StartsWith("!pet ", StringComparison.Ordinal)) {
                    // !pet info|cmd n|feedyes|menu|rename name|state (classic pet windows 0x58 / 0x5a)
                    Log("pet: " + Ragnarok.UI.RoClassicPet.Sweep(line.Substring(5)));
                } else if (line.StartsWith("!produce ", StringComparison.Ordinal)) {
                    // !produce add itemId|remove slot|ok|cancel|state (classic manufacturing materials 0x50; the list is !repair make ...)
                    Log("produce: " + Ragnarok.UI.RoClassicProduce.Sweep(line.Substring(9)));
                } else if (line.StartsWith("!merge ", StringComparison.Ordinal)) {
                    // !merge toggle i|ok|cancel|size w h|state (classic Item Merge 0xe3)
                    Log("merge: " + Ragnarok.UI.RoClassicMerge.Sweep(line.Substring(7)));
                } else if (line.StartsWith("!repair ", StringComparison.Ordinal)) {
                    // !repair [refine] pick i|ok|cancel|state (classic repair list 0x6b, weapon refine 0x6f)
                    Log("repair: " + Ragnarok.UI.RoClassicRepair.Sweep(line.Substring(8)));
                } else if (line.StartsWith("!isel ", StringComparison.Ordinal)) {
                    // !isel pick 0x49|0x4a row|ok id|cancel id|state (classic Item Appraisal / Insert Card)
                    Log("isel: " + Ragnarok.UI.RoClassicItemSelect.Sweep(line.Substring(6)));
                } else if (line.StartsWith("!warp ", StringComparison.Ordinal)) {
                    // !warp pick i|ok|cancel|state (classic warp / teleport list 0x27)
                    Log("warp: " + Ragnarok.UI.RoClassicWarpList.Sweep(line.Substring(6)));
                } else if (line.StartsWith("!sl ", StringComparison.Ordinal)) {
                    // !sl toggle|set i text|send i|state (classic Shortcut List 0x56)
                    Log("sl: " + Ragnarok.UI.RoClassicShortcutList.Sweep(line.Substring(4), Ragnarok.UI.RoClassicChat.Instance));
                } else if (line.StartsWith("!emoji ", StringComparison.Ordinal)) {
                    // !emoji toggle|tab n|search text|back|wheel n|click i|dclick i|state (classic Emoji List 0x57)
                    Log("emoji: " + Ragnarok.UI.RoClassicEmojiList.Sweep(line.Substring(7), null));
                } else if (line.StartsWith("!f11", StringComparison.Ordinal)) {
                    // !f11 [press|state] (F11 Close Tabs: one press, then the state, the remembered and the hidden windows)
                    if (!line.EndsWith("state", StringComparison.Ordinal)) Ragnarok.UI.RoClassicCloseTabs.Press();
                    Log("f11: " + Ragnarok.UI.RoClassicCloseTabs.Describe());
                } else if (line.StartsWith("!notify ", StringComparison.Ordinal)) {
                    // !notify level|job|quest|item id n|click level|job|quest|state (classic notifications 0x15 / 0x31 / 0x8f / 0x3a)
                    Log("notify: " + shell.SweepNotify(line.Substring(8)));
                } else if (line.StartsWith("!selcart ", StringComparison.Ordinal)) {
                    // !selcart open level|deco identity t,t|pick type|close|state (classic cart list 0x5f)
                    Log("selcart: " + shell.SweepSelectCart(line.Substring(9)));
                } else if (line.StartsWith("!bs ", StringComparison.Ordinal)) {
                    // !bs title t|add i|amount row n|price row z|limit z|remove row|ok|cancel|close|view aid|sell i [n]|unsell row|sellok|
                    // sellcancel|boards|state (classic buying stores)
                    string bsCommand = line.Substring(4).Trim();
                    Log("bs: " + (bsCommand == "boards" ? Ragnarok.UI.RoClassicActorBoards.Describe() : Ragnarok.UI.RoClassicBuyingStore.Sweep(bsCommand)));
                } else if (line.StartsWith("!room ", StringComparison.Ordinal)) {
                    // !room form|create title limit pub|pri [pw]|setup|combo 0|1|combopick n|join chatId|password pw|say text|leave|
                    // select name|menu|pick n|size w h|wheel n|boards|state (classic chat rooms)
                    string roomCommand = line.Substring(6).Trim();
                    Log("room: " + (roomCommand == "boards" ? Ragnarok.UI.RoClassicActorBoards.Describe() : Ragnarok.UI.RoClassicChatRoom.Sweep(roomCommand)));
                } else if (line.StartsWith("!trade ", StringComparison.Ordinal)) {
                    // !trade request aid|yes|no|zeny n|add bagIndex n|ok|trade|cancel|state (classic trade windows)
                    Log("trade: " + Ragnarok.UI.RoClassicTrade.Sweep(line.Substring(7)));
                } else if (line.StartsWith("!cart ", StringComparison.Ordinal)) {
                    // !cart open|close|size w h|wheel n|take i [storage]|put bagIndex [n]|state (classic cart window)
                    Log("cart: " + Ragnarok.UI.RoClassicCart.Sweep(shell != null ? shell.ClassicSession : null, line.Substring(6).Trim()));
                } else if (line.StartsWith("!pf ", StringComparison.Ordinal)) {
                    // !pf open [friend]|close|tab friend|party|press x y|right x y|pick n|dbl|button id|dlg ...|mini ...|state (classic Party / Friend window)
                    Log("pf: " + Ragnarok.UI.RoClassicPartyFriends.Sweep(shell != null ? shell.ClassicSession : null, line.Substring(4)));
                } else if (line.StartsWith("!st ", StringComparison.Ordinal)) {
                    // !st tab t|take row [n]|put bagIndex [n]|desc row|cat i|search text|sort m|close|state (classic storage)
                    Log("st: " + Ragnarok.UI.RoClassicStorage.Sweep(line.Substring(4)));
                } else if (line.StartsWith("!desc", StringComparison.Ordinal)) {
                    // !desc [close]: the classic item description window
                    // !desc cmp on|off | card cell | cmpcard cell (0xea's card) | view
                    string[] dp = line.Length > 6 ? line.Substring(6).Trim().Split(' ') : new string[0];
                    if (line.EndsWith("close", StringComparison.Ordinal)) Ragnarok.UI.RoClassicItemDescription.Close();
                    else if (dp.Length == 2 && dp[0] == "cmp") Ragnarok.UI.RoClassicItemDescription.CompareEquips = dp[1] == "on";
                    else if (dp.Length == 2 && (dp[0] == "card" || dp[0] == "cmpcard")) Ragnarok.UI.RoClassicItemDescription.PressCardForTests(dp[0] == "cmpcard", int.Parse(dp[1], CultureInfo.InvariantCulture));
                    else if (dp.Length >= 1 && dp[0] == "view") Ragnarok.UI.RoClassicItemDescription.ViewForTests(dp.Length > 1 && dp[1] == "52");
                    else if (dp.Length == 3 && dp[0] == "move52") Ragnarok.UI.RoClassicItemDescription.MoveCardForTests(int.Parse(dp[1], CultureInfo.InvariantCulture), int.Parse(dp[2], CultureInfo.InvariantCulture));
                    else if (dp.Length >= 1 && dp[0] == "illust") Log("illust: " + Ragnarok.UI.RoClassicCardIllust.Describe());
                    else if (line.Length > 6 && int.TryParse(line.Substring(6).Trim(), out int descId)) {
                        // !desc <item id>: that item of the own inventory described
                        Ragnarok.Data.RoItemInstance owned = Ragnarok.UI.RoClassicPreviewEquip.OwnedItem(descId);
                        if (owned != null) Ragnarok.UI.RoClassicItemDescription.Toggle(owned, Ragnarok.UI.RoClassicUiHost.GameAssets()?.GameData?.Items);
                    }
                    Log("desc: " + Ragnarok.UI.RoClassicItemDescription.Describe());
                } else if (line.StartsWith("!mb ", StringComparison.Ordinal)) {
                    // !mb state|enter|esc|click id|show type text (classic message boxes)
                    string[] p = line.Substring(4).Split(new[] { ' ' }, 3);
                    switch (p[0]) {
                        case "enter": Ragnarok.UI.Classic.RoClassicMessageBox.AnswerTop(-1); break;
                        case "esc": Ragnarok.UI.Classic.RoClassicMessageBox.AnswerTop(-2); break;
                        case "click": Ragnarok.UI.Classic.RoClassicMessageBox.AnswerTop(int.Parse(p[1], CultureInfo.InvariantCulture)); break;
                        case "show":
                            Ragnarok.UI.Classic.RoClassicMessageBox.Show(p.Length > 2 ? p[2] : string.Empty, int.Parse(p[1], CultureInfo.InvariantCulture), true,
                                result => Log("mb: answered 0x" + result.ToString("x")));
                            break;
                    }

                    Log("mb: " + Ragnarok.UI.Classic.RoClassicMessageBox.Describe());
                } else if (line.StartsWith("!chat ", StringComparison.Ordinal)) {
                    // !chat enter|esc|tab|up|down|f10|type <text>|press x y|dbl x y|wheel ±1|button <path>|state
                    Log("chat: " + Ragnarok.UI.RoClassicChat.Sweep(line.Substring(6)));
                } else if (line.StartsWith("!markers", StringComparison.Ordinal)) {
                    string[] needles = line.Length > 9 ? line.Substring(9).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries) : null;
                    Log("markers: " + RoUiPerfProbe.DumpAvailable(needles));
                } else if (line.StartsWith("!ui ", StringComparison.Ordinal)) {
                    // !ui <basic-info menu id>...: clicks those Basic Info menu buttons (item, equip, info, skill, quest, ...)
                    foreach (string id in line.Substring(4).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
                        bool toggled = Ragnarok.UI.RoClassicBasicInfo.ToggleMenuIcon(id);
                        Log("ui: " + id + (toggled ? " toggled" : " not found"));
                    }
                } else if (line.StartsWith("!act ", StringComparison.Ordinal)) {
                    // !act <src> <dst> <damage> <action> [count] [damage2]: a ZC_NOTIFY_ACT played client-side (basic attack)
                    string[] p = line.Split(' ');
                    world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(new RoZcNotifyAct(
                        uint.Parse(p[1], CultureInfo.InvariantCulture), uint.Parse(p[2], CultureInfo.InvariantCulture), 0, 300, 230,
                        int.Parse(p[3], CultureInfo.InvariantCulture),
                        p.Length > 5 ? ushort.Parse(p[5], CultureInfo.InvariantCulture) : (ushort) 1,
                        byte.Parse(p[4], CultureInfo.InvariantCulture),
                        p.Length > 6 ? int.Parse(p[6], CultureInfo.InvariantCulture) : 0,
                        false)));
                } else if (line.StartsWith("!bg ", StringComparison.Ordinal)) {
                    // !bg inject <hex packet>|state: the classic battleground queue (0xd1 / 0xd2) and battlefield chat mode
                    Log("bg: " + Ragnarok.UI.RoClassicEntryQueue.Sweep(line.Substring(4)));
                } else if (line.StartsWith("!banner", StringComparison.Ordinal)) {
                    // !banner [show map]: the classic map-name banner
                    Log("banner: " + Ragnarok.UI.RoClassicMapBanner.Sweep(line.Substring(7)));
                } else if (line.StartsWith("!target ", StringComparison.Ordinal)) {
                    // !target <skill> <level> [x y] | cancel: skill targeting as from a hotkey (the classic level digits at the pointer x y)
                    string[] tg = line.Substring(8).Trim().Split(' ');
                    var overlay = Ragnarok.UI.RoSkillTargetSelection.EnsureInScene();
                    if (tg[0] == "cancel") {
                        overlay.Cancel();
                        Ragnarok.UI.RoClassicSkillLevel.ForcedPointer = new Vector2Int(-1, -1);
                    } else {
                        overlay.Begin(null, int.Parse(tg[0], CultureInfo.InvariantCulture), int.Parse(tg[1], CultureInfo.InvariantCulture), string.Empty,
                            Ragnarok.Data.RoSkillTargetFlags.Enemy, true, 10);
                        if (tg.Length >= 4) Ragnarok.UI.RoClassicSkillLevel.ForcedPointer = new Vector2Int(int.Parse(tg[2], CultureInfo.InvariantCulture), int.Parse(tg[3], CultureInfo.InvariantCulture));
                    }

                    Log("target: " + (overlay.HasActiveSelection ? overlay.SkillId + " lv " + overlay.SkillLevel : "none"));
                } else if (line.StartsWith("!ach", StringComparison.Ordinal)) {
                    // !ach [open|tab m s|mode n|row i|icon i|jump id|inject hex]: the classic achievements 0x10e / 0x10f
                    Log("ach: " + Ragnarok.UI.RoClassicAchievements.Sweep(line.Substring(4), null));
                } else if (line.StartsWith("!preview", StringComparison.Ordinal)) {
                    // !preview [item id|left|right|return|close]: the classic try-on preview 0x126
                    Log("preview: " + Ragnarok.UI.RoClassicPreviewEquip.Sweep(line.Substring(8), null));
                } else if (line.StartsWith("!bcast", StringComparison.Ordinal)) {
                    // !bcast [a text|b text]: the classic broadcast boxes (a 0x01c3 gold / a plain 0x009a played locally)
                    Log("bcast: " + Ragnarok.UI.RoClassicBroadcast.Sweep(line.Substring(6)));
                } else if (line.StartsWith("!att", StringComparison.Ordinal)) {
                    // !att [open|inject data|press x y]: the classic attendance check 0x148
                    Log("att: " + Ragnarok.UI.RoClassicAttendance.Sweep(line.Substring(4)));
                } else if (line.StartsWith("!bank", StringComparison.Ordinal)) {
                    // !bank [check|amount n|press hexid]: the classic bank 0x113
                    Log("bank: " + Ragnarok.UI.RoClassicBank.Sweep(line.Substring(5)));
                } else if (line == "!sign") {
                    // !sign: the classic map signboards of this map
                    Log("sign: " + Ragnarok.UI.RoClassicSignBoards.Describe());
                } else if (line.StartsWith("!efst", StringComparison.Ordinal)) {
                    // !efst [state|hover x y|tip]: the classic status icons (layout, /stateinfo tooltip)
                    Log("efst: " + Ragnarok.UI.RoClassicStatusIcons.Sweep(line.Substring(5)));
                } else if (line.StartsWith("!camdist ", StringComparison.Ordinal)) {
                    // !camdist <d>: the camera distance for close-up checks (diagnostics only; the min clamp follows)
                    float d = float.Parse(line.Substring(9).Trim(), CultureInfo.InvariantCulture);
                    var cam = FindFirstObjectByType<Ragnarok.Camera.RoCameraController>();
                    if (cam != null) {
                        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                        foreach (string field in new[] { "minDistance", "distance", "targetDistance" }) cam.GetType().GetField(field, flags)?.SetValue(cam, d);
                    }

                    Log("camdist: " + (cam != null ? cam.Distance.ToString(CultureInfo.InvariantCulture) : "no camera"));
                } else if (line.StartsWith("!pmenu ", StringComparison.Ordinal)) {
                    // !pmenu <aid> [item]: the right-click player menu for that actor (classic UIMenuWnd 0x12), optionally picking an item
                    string[] pm = line.Substring(7).Trim().Split(' ');
                    uint aid = uint.Parse(pm[0], CultureInfo.InvariantCulture);
                    var shell = FindFirstObjectByType<Ragnarok.UI.RoMinimalLoginShell>();
                    if (world != null && shell != null && world.State.TryGetActor(aid, out Ragnarok.World.RoOnlineActorSnapshot actor)) {
                        shell.ShowPlayerContextMenu(Vector2.zero, actor);
                        Log("pmenu: " + Ragnarok.UI.Classic.RoClassicMenu.Describe());
                        if (pm.Length > 1) Ragnarok.UI.Classic.RoClassicMenu.PickForTests(int.Parse(pm[1], CultureInfo.InvariantCulture));
                    } else {
                        Log("pmenu: no actor " + aid);
                    }
                } else if (line.StartsWith("!eqp", StringComparison.Ordinal)) {
                    // !eqp toggle|tab n|state (equipment properties 0x2711 and its manager)
                    Log("eqp: " + Ragnarok.UI.RoClassicEquipProperties.Sweep(line.Substring(4)));
                } else if (line.StartsWith("!md", StringComparison.Ordinal)) {
                    // !md wait name prio|prio n|info name destroySec enterSec|notify type enterSec|toggle|state (classic memorial dungeon 0x89)
                    Log("md: " + Ragnarok.UI.RoClassicMemorial.Sweep(line.Substring(3)));
                } else if (line.StartsWith("!netaudit", StringComparison.Ordinal)) {
                    // !netaudit [reset]: map packets the drain switch does not handle, and parse failures
                    Ragnarok.Network.RoNetAudit.Enabled = true;
                    if (line.EndsWith("reset", StringComparison.Ordinal)) Ragnarok.Network.RoNetAudit.Reset();
                    Log("netaudit: " + Ragnarok.Network.RoNetAudit.Describe());
                } else if (line.StartsWith("!book", StringComparison.Ordinal)) {
                    // !book read id [page]|auto id [page]|packet id page|next|prev|mark|close|isbook id|state (classic book reader 0x6a)
                    Log("book: " + Ragnarok.UI.RoClassicBook.Sweep(line.Substring(5)));
                } else if (line.StartsWith("!mobinfo", StringComparison.Ordinal)) {
                    // !mobinfo class level size hp def race mdef element [target aid]|close|state (classic monster info 0x4d)
                    Log("mobinfo: " + Ragnarok.UI.RoClassicMonsterInfo.Sweep(line.Substring(8)));
                } else if (line.StartsWith("!prompt", StringComparison.Ordinal)) {
                    // !prompt star which|talkie|graffiti|text t|ok|cancel|state (classic talkie box / graffiti / Star Gladiator boxes)
                    Log("prompt: " + Ragnarok.UI.RoClassicPrompts.Sweep(line.Substring(7)));
                } else if (line.StartsWith("!tip", StringComparison.Ordinal)) {
                    // !tip toggle|show id|next|search text|pick row|state (classic tip box 0x13b)
                    Log("tip: " + Ragnarok.UI.RoClassicTipBox.Sweep(line.Substring(4)));
                } else if (line.StartsWith("!clan", StringComparison.Ordinal)) {
                    // !clan toggle|state (classic clan window 0xed)
                    Log("clan: " + Ragnarok.UI.RoClassicClan.Sweep(line.Substring(5)));
                } else if (line.StartsWith("!agency", StringComparison.Ordinal)) {
                    // !agency toggle|search|reset|row i|register|submit|cancel|join|ask name job lv|answer 0|1|block|result n|state (classic Adventurer Agency)
                    Log("agency: " + Ragnarok.UI.RoClassicAgency.Sweep(line.Substring(7)));
                } else if (line.StartsWith("!emblem", StringComparison.Ordinal)) {
                    // !emblem state|ver gid v|file gid|balloon (classic guild emblems)
                    Log("emblem: " + Ragnarok.UI.RoClassicGuildEmblems.Sweep(line.Substring(7)));
                } else if (line.StartsWith("!block", StringComparison.Ordinal)) {
                    // !block state|answer type result (classic chat block set, player menu Block Chat)
                    Log("block: " + Ragnarok.UI.RoClassicChatBlock.Sweep(line.Substring(6)));
                } else if (line.StartsWith("!adopt", StringComparison.Ordinal)) {
                    // !adopt offer name|err n|ok|cancel|state (classic adoption boxes 0x69 / 0x6e)
                    Log("adopt: " + Ragnarok.UI.RoClassicAdoption.Sweep(line.Substring(6)));
                } else if (line.StartsWith("!report", StringComparison.Ordinal)) {
                    // !report open aid name|type n|text t|bytes|state (classic Report a User 0x2715; bytes does not send)
                    Log("report: " + Ragnarok.UI.RoClassicReport.Sweep(Ragnarok.UI.RoClassicEntryQueue.CurrentSession, line.Substring(7)));
                } else if (line.StartsWith("!nav ", StringComparison.Ordinal)) {
                    // !nav open|close|search q|filter n|pick g [m]|start|service 0/1|min|max|back|cmd /navi …|state (classic navigation 0xcb)
                    Log("nav: " + Ragnarok.UI.RoClassicNavigation.Sweep(line.Substring(5)));
                } else if (line.StartsWith("!veq ", StringComparison.Ordinal)) {
                    // !veq req aid|tab t|min|close|state (classic view of another player's equipment, window 0x8b)
                    Log("veq: " + Ragnarok.UI.RoClassicViewEquip.Sweep(line.Substring(5)));
                } else if (line.StartsWith("!wh ", StringComparison.Ordinal)) {
                    // !wh open name|say name text|close name|state (classic 1:1 whisper windows)
                    Log("wh: " + Ragnarok.UI.RoClassicWhisper.Sweep(line.Substring(4)));
                } else if (line.StartsWith("!pchat ", StringComparison.Ordinal)) {
                    // !pchat <aid|0 = own> <text>: a ZC_NOTIFY_CHAT_PARTY 0x0109 frame parsed and played client-side (the balloon)
                    string[] p = line.Split(new[] { ' ' }, 3);
                    uint aid = uint.Parse(p[1], CultureInfo.InvariantCulture);
                    if (aid == 0) aid = world.OwnActorId;
                    byte[] text = System.Text.Encoding.UTF8.GetBytes((p.Length > 2 ? p[2] : string.Empty) + "\0");
                    var frame = new byte[8 + text.Length];
                    BitConverter.GetBytes((ushort) 0x0109).CopyTo(frame, 0);
                    BitConverter.GetBytes((ushort) frame.Length).CopyTo(frame, 2);
                    BitConverter.GetBytes(aid).CopyTo(frame, 4);
                    text.CopyTo(frame, 8);
                    RoZcPartyChat chat = RoMapPacketParser.ParseZcPartyChat(frame);
                    world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(chat));
                    Log("pchat: aid=" + chat.SourceActorId + " own=" + world.OwnActorId + " text='" + chat.Message + "'");
                } else if (line.StartsWith("!rank ", StringComparison.Ordinal)) {
                    // !rank <rank> <total>: a ZC_NOTIFY_RANKING for the own character, played client-side
                    string[] p = line.Split(' ');
                    world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(new RoZcNotifyRanking(world.OwnActorId,
                        int.Parse(p[1], CultureInfo.InvariantCulture), int.Parse(p[2], CultureInfo.InvariantCulture))));
                    Log("rank: injected for " + world.OwnActorId);
                } else if (line.StartsWith("!pvp", StringComparison.Ordinal)) {
                    // !pvp: the PvP rank / battleground score state the overlays read (RoPvpState)
                    Log($"pvp: pk {RoPvpState.IsPk} countPk {RoPvpState.CountPk} rank {RoPvpState.Rank}/{RoPvpState.Total} rank1Since {RoPvpState.Rank1SinceMs} " +
                        $"lion {RoPvpState.Lion} eagle {RoPvpState.Eagle} rankOverlay {RoPvpState.ShowRankOverlay} timer {RoPvpState.ShowRank1Timer} score {RoPvpState.ShowScoreOverlay} " +
                        $"bg {RoPvpState.IsBattlefield} enemyMarks {Ragnarok.UI.RoClassicBattlefieldMarks.Sweep()} teams {DescribeBattlefieldTeams()}");
                } else if (line.StartsWith("!texrot", StringComparison.Ordinal)) {
                    // !texrot: RSM2 meshes whose texture rotates (own mesh copies) and their first UV
                    var sb = new System.Text.StringBuilder();
                    int rotated = 0;
                    foreach (MeshFilter filter in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)) {
                        Mesh mesh = filter.sharedMesh;
                        if (mesh == null || !mesh.name.EndsWith("(texture rotation)", StringComparison.Ordinal)) {
                            continue;
                        }

                        rotated++;
                        if (rotated <= 4) {
                            var uvs = new List<Vector2>();
                            mesh.GetUVs(0, uvs);
                            sb.Append(' ').Append(mesh.name).Append(" uv0=").Append(uvs.Count > 0 ? uvs[0].ToString("F4") : "-");
                        }
                    }

                    Log("texrot: " + rotated + sb);
                } else if (line.StartsWith("!atlas", StringComparison.Ordinal)) {
                    // !atlas: the sprite atlas cache (atlases are shared by paths whose .spr is one archive block)
                    Ragnarok.Entities.RoSpriteAtlasCacheStats a = Ragnarok.Entities.RoSpriteAtlasCache.SnapshotStats();
                    Log($"atlas: entries {a.Entries}, atlases {a.Atlases}, hits {a.Hits}, misses {a.Misses}, builds {a.Builds}, shared {a.SharedBuilds}");
                } else if (line.StartsWith("!go ", StringComparison.Ordinal)) {
                    // !go <x> <y>: the own character walks to the cell (CZ_REQUEST_MOVE, as a click on the ground)
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    bool sent = shell != null && shell.SweepMove(int.Parse(p[1], CultureInfo.InvariantCulture), int.Parse(p[2], CultureInfo.InvariantCulture));
                    Log("go: " + p[1] + "," + p[2] + (sent ? " sent" : " not sent"));
                } else if (line.StartsWith("!near ", StringComparison.Ordinal)) {
                    // !near <actor id> [radius]: every enabled renderer whose bounds centre is within radius (2) world units of
                    // the actor's ground point, with its path, position, mesh size and material
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    float radius = p.Length > 2 ? float.Parse(p[2], CultureInfo.InvariantCulture) : 2f;
                    if (world == null) world = FindFirstObjectByType<RoOnlineWorldController>();
                    var ctx = world as Ragnarok.Combat.IRoCombatPresentationContext;
                    if (ctx == null || !uint.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint nearId) ||
                        !ctx.TryResolveActorFeedbackAnchor(nearId, out Transform nearAnchor, out Vector3 nearPos)) {
                        Log("near: no actor " + p[1]);
                    } else {
                        Vector3 origin = nearAnchor != null ? nearAnchor.position : nearPos;
                        var sb = new System.Text.StringBuilder("near " + p[1] + " at " + origin.ToString("F2") + ":");
                        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
                            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                            Vector3 c = r.bounds.center;
                            if (new Vector2(c.x - origin.x, c.z - origin.z).sqrMagnitude > radius * radius) continue;
                            string rpath = r.name;
                            for (Transform t = r.transform.parent; t != null; t = t.parent) rpath = t.name + "/" + rpath;
                            sb.Append("\n  ").Append(rpath).Append(" pos ").Append(r.transform.position.ToString("F2"))
                                .Append(" size ").Append(r.bounds.size.ToString("F2"))
                                .Append(" mat ").Append(r.sharedMaterial != null ? r.sharedMaterial.name : "-")
                                .Append(" tex ").Append(r.sharedMaterial != null && r.sharedMaterial.mainTexture != null ? r.sharedMaterial.mainTexture.name : "-");
                        }

                        Log(sb.ToString());
                    }
                } else if (line == "!carts") {
                    // !carts: every actor's cart (follow point, facing, action, layers) as the !clip frame log writes it
                    Log("carts: " + (Ragnarok.Entities.RoClientDrawOrder.DescribeCartsForDiagnostics() ?? "none"));
                } else if (line.StartsWith("!actors", StringComparison.Ordinal)) {
                    // !actors [radius] [mob|npc|pc|item|warp]: the actors the client knows within radius (15) cells of the own
                    // character, nearest first: id, type, name, cell, distance, hp, job
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int radius = p.Length > 1 && int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : 15;
                    string kind = p.Length > 2 ? p[2] : p.Length > 1 && radius == 15 && !char.IsDigit(p[1][0]) ? p[1] : null;
                    if (world == null) world = FindFirstObjectByType<RoOnlineWorldController>();
                    if (world == null || !TryGetSelf(out RoOnlineActorSnapshot me)) {
                        Log("actors: not on a map");
                    } else {
                        var actors = new List<RoOnlineActorSnapshot>();
                        world.SnapshotActors(actors);
                        actors.RemoveAll(a => a.IsSelf || Math.Max(Math.Abs(a.CellX - me.CellX), Math.Abs(a.CellY - me.CellY)) > radius ||
                            (kind == "mob" && a.ObjectType != RoMapActorObjectType.Monster) ||
                            (kind == "npc" && a.ObjectType != RoMapActorObjectType.Npc && a.ObjectType != RoMapActorObjectType.Npc2) ||
                            (kind == "pc" && a.ObjectType != RoMapActorObjectType.Player) ||
                            (kind == "item" && a.ObjectType != RoMapActorObjectType.Item) ||
                            (kind == "warp" && a.ObjectType != RoMapActorObjectType.Warp));
                        actors.Sort((a, b) => Math.Max(Math.Abs(a.CellX - me.CellX), Math.Abs(a.CellY - me.CellY))
                            .CompareTo(Math.Max(Math.Abs(b.CellX - me.CellX), Math.Abs(b.CellY - me.CellY))));
                        Log($"actors: self {me.ServerId} at {me.CellX},{me.CellY} hp {me.Hp}/{me.MaxHp}, {actors.Count} within {radius}");
                        for (int i = 0; i < actors.Count && i < 40; i++) {
                            RoOnlineActorSnapshot a = actors[i];
                            Log($"  {a.ServerId} {a.ObjectType} '{a.Name}' at {a.CellX},{a.CellY} d {Math.Max(Math.Abs(a.CellX - me.CellX), Math.Abs(a.CellY - me.CellY))}" +
                                $" hp {a.Hp}/{a.MaxHp} job {a.Job} {a.Motion}");
                        }
                    }
                } else if (line.StartsWith("!walpha ", StringComparison.Ordinal)) {
                    // !walpha <window class> <alpha 0-255>: the window's alpha as a frame drag sets it (0x80), to capture the
                    // dragged look without a mouse (e.g. !walpha UIItemWnd 128, then !walpha UIItemWnd 255)
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var ui = Ragnarok.UI.Classic.RoClassicUiSystem.Instance;
                    int set = 0;
                    if (ui != null && p.Length > 2) {
                        foreach (Ragnarok.UI.Classic.RoClassicWindow w in ui.Windows.Windows) {
                            if (w.Layout.Class != p[1]) continue;
                            w.Alpha = byte.Parse(p[2], CultureInfo.InvariantCulture);
                            set++;
                        }
                    }

                    Log("walpha: " + set + " window(s)");
                } else if (line.StartsWith("!click ", StringComparison.Ordinal)) {
                    // !click <id>: a mouse click on the actor (monster: approach + attack, item: approach + pick up), the
                    // click controller's own path; !hit / !take send the request straight away instead
                    uint clickId = uint.Parse(line.Substring(7).Trim(), CultureInfo.InvariantCulture);
                    var clicks = FindFirstObjectByType<Ragnarok.Entities.RoClickToMoveController>();
                    Log("click: " + clickId + (clicks != null && clicks.SubmitOnlineTargetForDiagnostics(clickId) ? " submitted" : " not submitted"));
                } else if (line.StartsWith("!hit ", StringComparison.Ordinal) || line.StartsWith("!take ", StringComparison.Ordinal)) {
                    // !hit <id>: attacks the actor until it dies (CZ_REQUEST_ACT, repeat; the server walks into range), as a
                    // click on a monster does; !take <id>: picks up the ground item (CZ_ITEM_PICKUP; walk next to it first)
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int aid = unchecked((int) uint.Parse(p[1], CultureInfo.InvariantCulture));
                    bool sent = shell != null && (p[0] == "!hit" ? shell.TrySendAttackRequest(aid) : shell.TrySendPickupGroundItemRequest(aid));
                    Log(p[0].Substring(1) + ": " + p[1] + (sent ? " sent" : " not sent"));
                } else if (line.StartsWith("!statup ", StringComparison.Ordinal)) {
                    // !statup <str|agi|vit|int|dex|luk> [n]: spends n status points on the stat (CZ_STATUS_CHANGE with the amount)
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var stat = (RoStatusStat) Enum.Parse(typeof(RoStatusStat), p[1], true);
                    int n = p.Length > 2 ? int.Parse(p[2], CultureInfo.InvariantCulture) : 1;
                    Log("statup: " + stat + " x" + n + (shell?.ClassicSession?.BeginLegacyAllocateStat(stat, n) != null ? " sent" : " not sent"));
                } else if (line.StartsWith("!pick ", StringComparison.Ordinal)) {
                    // !pick <id> [shift] [go]: what a click on the actor does (RoPickHostility: PK / siege / battlefield, party, guild);
                    // go selects it like a click (RoOnlineWorldController.SelectOnlineTarget; the click controller then submits the attack)
                    string[] pk = line.Substring(6).Trim().Split(' ');
                    if (world == null) world = FindFirstObjectByType<RoOnlineWorldController>();
                    var mods = Array.IndexOf(pk, "shift") >= 0 ? Ragnarok.Input.RoPointerModifierFlags.Shift : Ragnarok.Input.RoPointerModifierFlags.None;
                    if (world == null || !uint.TryParse(pk[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint pickId) ||
                        !world.TryGetActorSnapshot(pickId, out RoOnlineActorSnapshot picked)) {
                        Log("pick: no such actor " + pk[0]);
                    } else {
                        RoOnlineTargetInteraction act = RoOnlinePointerTargetMarker.GetInteraction(picked,
                            RoOnlinePointerTargetMarker.GetPointerTargetKind(picked.ObjectType), mods);
                        Log($"pick: {pickId} '{picked.Name}' {picked.ObjectType} guild {picked.GuildId} {mods} -> {act} (pk {RoPvpState.IsPk} " +
                            $"siege {RoPvpState.IsSiege} bg {RoPvpState.IsBattlefield} party {RoPvpState.PartyProperty} guildprop {RoPvpState.GuildProperty} " +
                            $"nolockon {RoPvpState.NoLockOn} ownguild {RoPickHostility.Relations?.PickOwnGuildId})");
                        if (Array.IndexOf(pk, "go") >= 0) Log("pick: clicked " + world.SelectOnlineTarget(pickId, default, mods));
                    }
                } else if (line.StartsWith("!missing", StringComparison.Ordinal)) {
                    // !missing [n]: assets the client asked for and found in no source (RoAssetDiagnostics), e.g. after cutting a GRF
                    int max = line.Length > 9 && int.TryParse(line.Substring(9).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int m) ? m : 50;
                    Ragnarok.Assets.RoAssetDatabase game = RoClassicUiHost.GameAssets();
                    Ragnarok.Assets.RoAssetDatabase audio = Ragnarok.Audio.RoAudioSystem.Assets;
                    foreach (Ragnarok.Assets.RoAssetDatabase db in new[] { game, audio == game ? null : audio }) {
                        if (db == null) continue;
                        foreach (string row in db.Diagnostics.BuildMissingSummary(max).Split('\n')) {
                            if (row.Trim().Length > 0) Log("missing: " + row.TrimEnd());
                        }
                    }
                } else if (line.StartsWith("!dskin ", StringComparison.Ordinal)) {
                    // !dskin <id>: the damage skin (DSList id), as the equipment window's Apply sets it
                    Ragnarok.Effects.Classic.RoClassicEffectBridge.DamageSkin = int.Parse(line.Substring(7).Trim(), CultureInfo.InvariantCulture);
                    Log("dskin: " + Ragnarok.Effects.Classic.RoClassicEffectBridge.DamageSkins.Get(Ragnarok.Effects.Classic.RoClassicEffectBridge.DamageSkin).DisplayName
                        + " of " + Ragnarok.Effects.Classic.RoClassicEffectBridge.DamageSkins.Count);
                } else if (line.StartsWith("!wavcheck", StringComparison.Ordinal)) {
                    // !wavcheck: decodes every .wav of the GRFs with the client's decoder; formats and failures
                    Log("wavcheck: " + WavCheck());
                } else if (line.StartsWith("!crowddie ", StringComparison.Ordinal)) {
                    // !crowddie <n>: the first n crowd players die (ZC_NOTIFY_VANISH type died) and lie in the dead pose
                    int count = int.Parse(line.Substring(10).Trim(), CultureInfo.InvariantCulture);
                    var died = new List<uint>(count);
                    for (int i = 0; world != null && i < count && i < crowdCount; i++) {
                        world.State.ApplyVanish(new RoZcNotifyVanish(CrowdFirstId + (uint) i, 0, RoMapActorVanishType.Died));
                        died.Add(CrowdFirstId + (uint) i);
                    }

                    SyncCrowdPresentation(died);

                    Log("crowddie " + count);
                } else if (line.StartsWith("!skillspam ", StringComparison.Ordinal) || line.StartsWith("!fxspam ", StringComparison.Ordinal)) {
                    // Load test commands (RoSkillSweepRunner.LoadTest.cs; all client-side, nothing goes to the server; each
                    // run logs the average fps, p50 / p99 / worst frame, GC bytes per frame and the engine's peak effects / prims):
                    // !skillspam <total> [per second] | !skillspam stop: <total> random uses (50/s) of the whole sweep skill pool
                    // (RoLoadTestSkills.g.cs, 365 skills with their mode, cast, element, packets, unit and trap) played like
                    // InjectSkill from random crowd players (the own character without a crowd) on random crowd players / self /
                    // walkable cells within 8 cells: ZC_USESKILL_ACK with the cast capped at 600 ms, then ZC_NOTIFY_SKILL,
                    // ZC_USE_SKILL, ZC_NOTIFY_GROUNDSKILL and ZC_SKILL_ENTRY units (traps fire), queued and pumped per frame.
                    // !fxspam <total> [per second] [radius] | !fxspam stop: the pool's ground skills alone, from the own
                    // character on cells within [radius] (8); no crowd needed
                    bool areaOnly = line.StartsWith("!fxspam ", StringComparison.Ordinal);
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    StartSkillSpam(p[1] == "stop" ? 0 : Math.Max(1, int.Parse(p[1], CultureInfo.InvariantCulture)),
                        p.Length > 2 ? float.Parse(p[2], CultureInfo.InvariantCulture) : 50f, areaOnly,
                        areaOnly && p.Length > 3 ? int.Parse(p[3], CultureInfo.InvariantCulture) : 8);
                    Log(p[0].Substring(1) + " " + p[1]);
                } else if (line.StartsWith("!fxstorm ", StringComparison.Ordinal)) {
                    // !fxstorm <alive> [seconds] [radius] | !fxstorm stop: keeps <alive> classic effects running for [seconds] (20),
                    // random ids out of every effect the engine has (EF_ ids, skills and the rest) at random points within
                    // [radius] (10) cells of the own character
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    StartFxStorm(p[1] == "stop" ? 0 : int.Parse(p[1], CultureInfo.InvariantCulture),
                        p.Length > 2 ? float.Parse(p[2], CultureInfo.InvariantCulture) : 20f,
                        p.Length > 3 ? float.Parse(p[3], CultureInfo.InvariantCulture) : 10f, true);
                    Log("fxstorm " + p[1]);
                } else if (line.StartsWith("!crowdwalk ", StringComparison.Ordinal)) {
                    // !crowdwalk <n|all> [cells] | !crowdwalk stop: the first n crowd players walk back and forth between their
                    // cell and one [cells] (5) away in a free straight direction (ZC_NOTIFY_MOVE records applied to the world
                    // state like the drain applies another player's move), with a short random pause at each end
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    Log("crowdwalk: " + StartCrowdWalk(p[1], p.Length > 2 ? int.Parse(p[2], CultureInfo.InvariantCulture) : 5));
                } else if (line.StartsWith("!woe ", StringComparison.Ordinal)) {
                    // !woe <players> [seconds] | !woe stop: WoE scenario: a crowd of <players>, half of it walking (!crowdwalk),
                    // !fxstorm min(players, 300) alive and !skillspam 40/s all at once; measured for [seconds] (30) after a
                    // 3 s settle, logged like the others, then everything stopped and the crowd cleared
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p[1] == "stop") {
                        StopScenario("woe");
                    } else {
                        StartScenario(Woe(int.Parse(p[1], CultureInfo.InvariantCulture), p.Length > 2 ? float.Parse(p[2], CultureInfo.InvariantCulture) : 30f));
                    }
                } else if (line == "!bench" || line.StartsWith("!bench ", StringComparison.Ordinal)) {
                    // !bench [seconds per stage] | !bench stop: reproducible load bench, fixed seeds, stages one after the other,
                    // each adding to the previous: empty; crowd 300 standing; 150 of them walking; + fxstorm 300 alive;
                    // + skillspam 40/s; [seconds] (15) each after a 3 s settle; per stage the average fps, p50 / p99 / worst
                    // frame, GC bytes per frame, main / render thread ms (when a recorder gives them), peak classic effects /
                    // prims and actor counts; cleans up after itself and writes PerformanceCaptures/bench/bench-<time>.tsv / .md
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length > 1 && p[1] == "stop") {
                        StopScenario("bench");
                    } else {
                        StartScenario(Bench(p.Length > 1 ? float.Parse(p[1], CultureInfo.InvariantCulture) : 15f));
                    }
                } else if (line.StartsWith("!crowd ", StringComparison.Ordinal)) {
                    // !crowd <n> [radius] | !crowd clear: client-side standing players around the own character (half with a
                    // robe), fixed seed per spawn; clear also stops the crowd walk
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    Log("crowd: " + (p[1] == "clear" ? ClearCrowd() : SpawnCrowd(int.Parse(p[1], CultureInfo.InvariantCulture),
                        p.Length > 2 ? int.Parse(p[2], CultureInfo.InvariantCulture) : 12)));
                } else if (line.StartsWith("!crowdui ", StringComparison.Ordinal)) {
                    // !crowdui <balloons per second> [shops] [rooms] | !crowdui stop: the crowd's overhead UI (RoSkillSweepRunner.LoadTest)
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    Log(p[1] == "stop" ? StopCrowdUi() : CrowdUi(float.Parse(p[1], CultureInfo.InvariantCulture),
                        p.Length > 2 ? int.Parse(p[2], CultureInfo.InvariantCulture) : 0, p.Length > 3 ? int.Parse(p[3], CultureInfo.InvariantCulture) : 0));
                } else if (line.StartsWith("!modeldump ", StringComparison.Ordinal)) {
                    // !modeldump <name>: every model instance under RO Models (name, world position / rotation / scale, active
                    // renderers) sorted by name into PerformanceCaptures/<name>.txt, to compare two ways of building a map
                    GameObject modelsRoot = GameObject.Find("RO Models");
                    var rows = new List<string>();
                    if (modelsRoot != null) {
                        foreach (Transform child in modelsRoot.transform) {
                            if (!child.name.Contains("[")) continue;
                            Vector3 wp = child.position, ws = child.lossyScale; Quaternion wr = child.rotation;
                            rows.Add(string.Format(CultureInfo.InvariantCulture, "{0} p={1:F4},{2:F4},{3:F4} r={4:F5},{5:F5},{6:F5},{7:F5} s={8:F4},{9:F4},{10:F4} kids={11} renderers={12}",
                                child.name, wp.x, wp.y, wp.z, wr.x, wr.y, wr.z, wr.w, ws.x, ws.y, ws.z, child.childCount, child.GetComponentsInChildren<MeshRenderer>(true).Length));
                        }
                    }
                    rows.Sort(StringComparer.Ordinal);
                    string dumpPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PerformanceCaptures", line.Substring(11).Trim() + ".txt"));
                    File.WriteAllLines(dumpPath, rows);
                    Log("modeldump: " + rows.Count + " instances -> " + dumpPath);
                } else if (line.StartsWith("!modelasync ", StringComparison.Ordinal)) {
                    // !modelasync on|off: the static model instances made per template with InstantiateAsync (off = one by one)
                    Ragnarok.Maps.RoModelRenderer.InstantiateInBatches = line.EndsWith(" on", StringComparison.Ordinal);
                    Log("modelasync: " + Ragnarok.Maps.RoModelRenderer.InstantiateInBatches);
                } else if (line == "!texverify") {
                    // !texverify: the prepared model textures redone on the worker path and the main-thread path, compared
                    Log(Ragnarok.Maps.RoModelRenderer.VerifyTexturePrefetch(world != null && world.RuntimeMapController != null ? RoClassicUiHost.GameAssets() : null));
                } else if (line.StartsWith("!texprefetch ", StringComparison.Ordinal) || line.StartsWith("!texclear ", StringComparison.Ordinal)) {
                    // !texprefetch on|off: the model textures prefetched on worker threads (off = main thread as before);
                    // !texclear on|off: the model caches cleared at every map load (repeatable first-visit timings)
                    bool on = line.EndsWith(" on", StringComparison.Ordinal);
                    if (line.StartsWith("!texprefetch", StringComparison.Ordinal)) Ragnarok.Maps.RoModelRenderer.TexturePrefetchEnabled = on;
                    else Ragnarok.Maps.RoModelRenderer.ForceClearCachesOnMapLoad = on;
                    Log(line.Substring(1) + ": " + on);
                } else if (line == "!quit") {
                    // !quit: ends a player build (measurement scripts close the game they started)
                    Log("quit");
                    Application.Quit();
                } else if (line.StartsWith("!fxspikes ", StringComparison.Ordinal)) {
                    // !fxspikes on|off: classic engine spikes (>= 8 ms, step by step) and slow launches logged all session
                    bool on = line.EndsWith(" on", StringComparison.Ordinal);
                    RoSkillSweepProbe.Enabled = on;
                    RoSkillSweepProbe.Echo = on ? (System.Action<string>) (text => Log("fx " + text)) : null;
                    Log("fxspikes " + on);
                } else if (line.StartsWith("!clip ", StringComparison.Ordinal)) {
                    // !clip <seconds> <name> [width]: every rendered frame as a PNG into <outputDir>/clips/<name> (frame-by-frame
                    // inspection of one-frame glitches); ffmpeg from the plan, else the dev machine's ffmpeg-static
                    string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 3) StartCoroutine(RecordClip(float.Parse(p[1], CultureInfo.InvariantCulture), p[2], p.Length > 3 ? int.Parse(p[3], CultureInfo.InvariantCulture) : 1280));
                } else if (line.StartsWith("!walkloop ", StringComparison.Ordinal)) {
                    // !walkloop <cells> <seconds> [ns]: walks back and forth that many cells east/west (ns: north/south) of the current cell
                    string[] p = line.Split(' ');
                    StartCoroutine(WalkLoop(int.Parse(p[1], CultureInfo.InvariantCulture), float.Parse(p[2], CultureInfo.InvariantCulture), p.Length > 3 && p[3] == "ns"));
                } else if (line.StartsWith("!toggle ", StringComparison.Ordinal)) {
                    // !toggle pivot|sortonly|lean on|off: A/B switches for the crowd rendering changes; lean off = the old
                    // sprite depth (vertical plane + bias) instead of the client's leaning one
                    string[] p = line.Split(' ');
                    bool on = p.Length > 2 && p[2] == "on";
                    if (p[1] == "pivot") {
                        Ragnarok.Entities.RoActorLayeredVisual.UseLayerPivotForDiagnostics = on;
                    } else if (p[1] == "sortonly") {
                        Ragnarok.World.RoOnlineActorPresenter.SortOnlyOnCameraMoveForDiagnostics = on;
                    } else if (p[1] == "order") {
                        // the client's single draw order of actor layers and effects (off = actors by screen-y groups, effects above)
                        Ragnarok.Entities.RoClientDrawOrder.Enabled = on;
                    } else if (p[1] == "fxexact") {
                        Ragnarok.Effects.Classic.RoClassicEffectBridge.ExactDeviceState = on;
                    } else if (p[1] == "fxtickless") {
                        Ragnarok.Effects.Classic.RoClassicEffectBridge.ProcessOnTicklessFrames = on;
                    } else if (p[1] == "fxthread") {
                        Ragnarok.Effects.Classic.RoClassicEffectBridge.ThreadedRender = on;
                    } else if (p[1] == "fxlogic") {
                        Ragnarok.Effects.Classic.RoClassicEffectBridge.ThreadedLogic = on;
                    } else if (p[1] == "fxjoin") {
                        Ragnarok.Effects.Classic.RoClassicEffectBridge.JoinAtFrameStartEnabled = on;
                    } else if (p[1] == "fxdefer") {
                        Ragnarok.Effects.Classic.RoClassicEffectBridge.DeferLaunches = on;
                    } else if (p[1] == "fxkeyed") {
                        Ragnarok.Effects.Classic.RoClassicEffectBridge.KeyedMaterials = on;
                    } else if (p[1] == "fxqueue") {
                        Ragnarok.Effects.Classic.RoClassicEffectBridge.OrderedQueues = on;
                    } else if (p[1] == "sweepprobe") {
                        // the sweep's per-skill probe stays on after a plan ran; off for load tests that should cost what a
                        // player's game costs (it formats every packet / launch / spike into strings: ~13 KB a frame at !woe 300)
                        RoSkillSweepProbe.Enabled = on;
                    } else if (p[1] == "leanlinear") {
                        // the sprite depth linear in screen y like the client's body line (off = the old per-vertex curve with
                        // the below-feet ground clamp)
                        Shader.SetGlobalFloat("_RoSpriteCurvedDepth", on ? 0f : 1f);
                    } else if (p[1] == "imf") {
                        // the IMF head / body order per frame (off = the body always under the head, as before)
                        Ragnarok.Data.RoImfDrawOrder.Enabled = on;
                        Log("imf: " + on);
                    } else if (p[1] == "lean") {
                        Shader.SetGlobalFloat("_RoSpriteLegacyDepth", on ? 0f : 1f);
                    } else if (p[1] == "pull") {
                        Shader.SetGlobalFloat("_RoSpriteColorDepthPull", on ? Ragnarok.Entities.RoClientDrawOrder.ColorDepthPull : 0f);
                    } else if (p[1] == "pointerlegacy") {
                        // the hover broadphase as before the layer union / frame projection (A/B)
                        Ragnarok.Input.RoSpritePointerTargetShape.DiagnosticLegacyBroadphase = on;
                        Log("pointerlegacy: " + on);
                    } else if (p[1] == "singlepass") {
                        // the fully opaque sprite layers drawn in one pass (off = every depth-writing layer in two)
                        Ragnarok.Sprites.RoActSpriteAnimator.SinglePassDepth = on;
                        Log("singlepass: " + Ragnarok.Sprites.RoActSpriteAnimator.ReapplyDepthModeForAll() + " animators");
                    } else if (p[1] == "merged") {
                        // timing only: the sprites' depth pass off and the forward pass writing depth (RoSpriteDepthModeProbe)
                        Log("merged: " + RoSpriteDepthModeProbe.SetMergedForTiming(on));
                    } else if (p[1] == "spriteshadows") {
                        // timing only: the sprite layers cast no realtime shadow
                        Log("spriteshadows: " + RoSpriteDepthModeProbe.SetSpriteShadowsForTiming(on));
                    }

                    Log("toggle " + p[1] + " " + on);
                } else if (line.StartsWith("!heads", StringComparison.Ordinal)) {
                    // !heads [max]: presented players whose body does not show under the head
                    string[] p = line.Split(' ');
                    var presenter = FindFirstObjectByType<Ragnarok.World.RoOnlineActorPresenter>();
                    Log("heads: " + (presenter != null ? presenter.DescribeHeadsWithoutBodyForDiagnostics(p.Length > 1 ? int.Parse(p[1], CultureInfo.InvariantCulture) : 12) : "no presenter"));
                } else if (line.StartsWith("!layers", StringComparison.Ordinal)) {
                    // !layers [max]: presented players with a missing / undrawn / misoriented body layer
                    string[] p = line.Split(' ');
                    var presenter = FindFirstObjectByType<Ragnarok.World.RoOnlineActorPresenter>();
                    Log("layers: " + (presenter != null ? presenter.DescribeBrokenLayersForDiagnostics(p.Length > 1 ? int.Parse(p[1], CultureInfo.InvariantCulture) : 12) : "no presenter"));
                } else if (line == "!granny") {
                    // !granny: the presented Granny (.gr2) models with their guild and emblem state
                    var presenter = FindFirstObjectByType<Ragnarok.World.RoOnlineActorPresenter>();
                    Log("granny: " + (presenter != null ? presenter.DescribeGrannyForDiagnostics() : "no presenter"));
                } else if (line.StartsWith("!robepass ", StringComparison.Ordinal)) {
                    // !robepass <robe> <male 0|1> <job> <action from> <action to> [frames]: the robe pass per ACT action / frame
                    string[] p = line.Split(' ');
                    int robe = int.Parse(p[1], CultureInfo.InvariantCulture), job = int.Parse(p[3], CultureInfo.InvariantCulture);
                    int from = int.Parse(p[4], CultureInfo.InvariantCulture), to = int.Parse(p[5], CultureInfo.InvariantCulture);
                    int frames = p.Length > 6 ? int.Parse(p[6], CultureInfo.InvariantCulture) : 3;
                    var text = new System.Text.StringBuilder("ready=" + Ragnarok.Data.RoRobeDrawRules.IsReady);
                    for (int act = from; act <= to; act++) {
                        text.Append("\n  act ").Append(act).Append(':');
                        for (int f = 0; f < frames; f++) {
                            text.Append(' ').Append((int) Ragnarok.Data.RoRobeDrawRules.Resolve(robe, p[2] == "1", job, act, f));
                        }
                    }

                    Log("robepass: " + text);
                } else if (line == "!imf") {
                    // !imf: every layered actor's IMF state (head drawn before the body this frame?), the own character first
                    var text = new System.Text.StringBuilder();
                    int shown = 0;
                    foreach (var visual in FindObjectsByType<Ragnarok.Entities.RoActorLayeredVisual>(FindObjectsSortMode.None)) {
                        bool own = visual.GetComponentInParent<Ragnarok.World.RoOnlineActorPresenter>() == null;
                        if (!own && shown >= 12) continue;
                        text.Append("\n  ").Append(own ? "[self] " : string.Empty).Append(visual.name).Append(" face=").Append(visual.FacingDirection)
                            .Append(' ').Append(visual.DescribeHeadOrder());
                        shown++;
                    }

                    Log("imf:" + text);
                } else if (line == "!garments self") {
                    // !garments self: the layers of every actor visual not presented by the presenter (the own character)
                    var text = new System.Text.StringBuilder();
                    foreach (var visual in FindObjectsByType<Ragnarok.Entities.RoActorLayeredVisual>(FindObjectsSortMode.None)) {
                        if (visual.GetComponentInParent<Ragnarok.World.RoOnlineActorPresenter>() != null) continue;
                        text.Append("\n  ").Append(visual.name).Append(" motion=").Append(visual.Motion).Append(" face=").Append(visual.FacingDirection)
                            .Append(' ').Append(visual.DescribeRobePass())
                            .Append(' ').Append(visual.DescribeLayers());
                    }
                    Log("garments self:" + text);
                } else if (line.StartsWith("!garments", StringComparison.Ordinal)) {
                    // !garments [max]: the layers of presented players wearing a garment, walking ones first
                    string[] p = line.Split(' ');
                    var presenter = FindFirstObjectByType<Ragnarok.World.RoOnlineActorPresenter>();
                    Log("garments: " + (presenter != null ? presenter.DescribeGarmentLayersForDiagnostics(p.Length > 1 ? int.Parse(p[1], CultureInfo.InvariantCulture) : 6) : "no presenter"));
                } else if (line.StartsWith("!tickmarkers", StringComparison.Ordinal)) {
                    // !tickmarkers on|off: per-type actor tick markers (RO.Tick.* / RO.LateTick.* / RO.LateCheck.*) for cpuprobe
                    Ragnarok.Sprites.RoActorTickService.SplitTypeMarkers = line.EndsWith(" on", StringComparison.Ordinal);
                    Log("tickmarkers: " + Ragnarok.Sprites.RoActorTickService.SplitTypeMarkers);
                } else if (line.StartsWith("!pointersweep", StringComparison.Ordinal)) {
                    // !pointersweep on|off: the pointer circles the screen centre (measures hover targeting in a crowd
                    // whatever the real mouse does, e.g. outside an unfocused player window)
                    Ragnarok.Input.RoPointerScreenPosition.DiagnosticSweep = line.EndsWith(" on", StringComparison.Ordinal);
                    Log("pointersweep: " + Ragnarok.Input.RoPointerScreenPosition.DiagnosticSweep);
                } else if (line.StartsWith("!spin ", StringComparison.Ordinal)) {
                    // !spin <degPerSec> <seconds>: turns the camera steadily
                    string[] p = line.Split(' ');
                    StartCoroutine(Spin(float.Parse(p[1], CultureInfo.InvariantCulture), float.Parse(p[2], CultureInfo.InvariantCulture)));
                } else if (line.StartsWith("!motion ", StringComparison.Ordinal)) {
                    // !motion <aid|self> [seconds]: the actor's motion step and body ACT action / frame, logged on change
                    string[] p = line.Split(' ');
                    StartCoroutine(WatchMotion(p[1], p.Length > 2 ? float.Parse(p[2], CultureInfo.InvariantCulture) : 5f));
                } else if (line.StartsWith("!watch", StringComparison.Ordinal)) {
                    // !watch [seconds]: logs what the probe records (packets, classic launches) for a while
                    string[] p = line.Split(' ');
                    StartCoroutine(Watch(p.Length > 1 ? float.Parse(p[1], CultureInfo.InvariantCulture) : 5f));
                } else if (line.StartsWith("!skill ", StringComparison.Ordinal)) {
                    string[] p = line.Split(' ');
                    var skill = new SweepSkill {
                        id = int.Parse(p[1], CultureInfo.InvariantCulture), level = int.Parse(p[2], CultureInfo.InvariantCulture),
                        mode = p.Length > 3 ? p[3] : "self", code = "console", name = "console",
                    };
                    StartCoroutine(RunSkill(-1, skill));
                } else if (TryLookCommand(line)) {
                    // !look / !cam / !daytime / !hud (RoSkillSweepRunner.Look.cs)
                } else {
                    shell?.SweepSendChat(line);
                }
            }
        }

        static string WavCheck() {
            Ragnarok.Assets.RoAssetDatabase assets = Ragnarok.Audio.RoAudioSystem.Assets;
            if (assets == null || !assets.IsInitialized) return "no assets";
            var formats = new Dictionary<string, int>();
            var failures = new List<string>();
            int total = 0, unreadable = 0;
            var unreadableSamples = new List<string>();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            foreach (string path in assets.ListFilesWithExtension("wav")) {
                total++;
                byte[] data;
                try {
                    data = assets.ReadBytes(path);
                } catch (Exception e) {
                    unreadable++;
                    if (unreadableSamples.Count < 8) unreadableSamples.Add(path + " (" + e.Message + ")");
                    continue;
                }

                if (!Ragnarok.Audio.RoWavDecoder.TryDecode(data, out Ragnarok.Audio.RoPcm pcm, out string error)) {
                    if (failures.Count < 12) failures.Add(path + ": " + error);
                    else failures.Add(null);
                    continue;
                }

                string key = pcm.Channels + "ch " + pcm.SampleRate + "Hz";
                formats.TryGetValue(key, out int n);
                formats[key] = n + 1;
            }

            var parts = new List<string>();
            foreach (var kv in formats) parts.Add(kv.Key + " x" + kv.Value);
            int failed = failures.Count;
            failures.RemoveAll(f => f == null);
            return $"{total} wav in {watch.ElapsedMilliseconds} ms, {unreadable} unreadable, {failed} do not decode; " + string.Join(", ", parts) +
                (failures.Count > 0 ? " | " + string.Join(" | ", failures) : string.Empty) +
                (unreadableSamples.Count > 0 ? " || unreadable: " + string.Join(" | ", unreadableSamples) : string.Empty);
        }

        IEnumerator Watch(float seconds) {
            RoSkillSweepProbe.Drain(out _, out _, out _, out _, out _);
            yield return new WaitForSecondsRealtime(seconds);
            List<string> events = RoSkillSweepProbe.Drain(out _, out _, out _, out _, out _);
            int shown = 0;
            foreach (string e in events) {
                if (e.Contains(" packet ") || e.Contains(" classic ") || e.Contains(" hit ") || e.Contains(" click ") || e.Contains(" legacy ") || e.Contains(" missing ")) {
                    Log("watch " + e);
                    if (++shown >= 80) {
                        break;
                    }
                }
            }

            Log($"watch done: {events.Count} events");
        }

        sealed class DrawGroup {
            public int Renderers, Draws, PropertyBlocks;
            public readonly HashSet<int> Materials = new HashSet<int>();
            public readonly HashSet<string> Shaders = new HashSet<string>();
        }

        static string FoldDigits(string name) => System.Text.RegularExpressions.Regex.Replace(name ?? string.Empty, "[0-9]+", "#");

        /// <summary>Visible renderers grouped by component, object name and root (digits folded); canvases counted apart.</summary>
        static string DescribeDrawStats() {
            var groups = new Dictionary<string, DrawGroup>();
            int renderers = 0, draws = 0;
            var allMaterials = new HashSet<int>();
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
                if (!r.enabled || !r.gameObject.activeInHierarchy || !r.isVisible) continue;
                Material[] materials = r.sharedMaterials;
                int n = 1;
                if (r is MeshRenderer) {
                    MeshFilter filter = r.GetComponent<MeshFilter>();
                    n = filter != null && filter.sharedMesh != null ? filter.sharedMesh.subMeshCount : materials.Length;
                } else if (r is SkinnedMeshRenderer skinned && skinned.sharedMesh != null) {
                    n = skinned.sharedMesh.subMeshCount;
                }

                string key = r.GetType().Name + " " + FoldDigits(r.gameObject.name) + "  <  " + FoldDigits(r.transform.root.name);
                if (!groups.TryGetValue(key, out DrawGroup g)) groups[key] = g = new DrawGroup();
                g.Renderers++;
                g.Draws += n;
                if (r.HasPropertyBlock()) g.PropertyBlocks++;
                foreach (Material m in materials) {
                    if (m == null) continue;
                    g.Materials.Add(m.GetInstanceID());
                    allMaterials.Add(m.GetInstanceID());
                    if (m.shader != null) g.Shaders.Add(m.shader.name);
                }

                renderers++;
                draws += n;
            }

            var sorted = new List<KeyValuePair<string, DrawGroup>>(groups);
            sorted.Sort((a, b) => b.Value.Draws.CompareTo(a.Value.Draws));
            var text = new System.Text.StringBuilder();
            text.Append(' ').Append(renderers).Append(" visible renderers, ").Append(draws).Append(" sub-mesh draws, ")
                .Append(allMaterials.Count).Append(" distinct materials");
            for (int i = 0; i < sorted.Count && i < 45; i++) {
                DrawGroup g = sorted[i].Value;
                text.Append("\n  ").Append(g.Draws.ToString().PadLeft(5)).Append(" draws ").Append(g.Renderers.ToString().PadLeft(4))
                    .Append(" rend ").Append(g.Materials.Count.ToString().PadLeft(4)).Append(" mat ")
                    .Append(g.PropertyBlocks.ToString().PadLeft(4)).Append(" mpb  ").Append(sorted[i].Key)
                    .Append("  [").Append(string.Join(", ", g.Shaders)).Append(']');
            }

            int canvases = 0, overlayCanvases = 0;
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) {
                if (!c.isActiveAndEnabled || !c.isRootCanvas) continue;
                canvases++;
                if (c.renderMode == RenderMode.ScreenSpaceOverlay) overlayCanvases++;
            }

            text.Append("\n  canvases: ").Append(canvases).Append(" root (").Append(overlayCanvases).Append(" overlay)");
            return text.ToString();
        }

        static string GetPath(Transform t) {
            string path = t.name;
            for (Transform p = t.parent; p != null && path.Length < 160; p = p.parent) {
                path = p.name + "/" + path;
            }

            return path;
        }

        void LogStatus() {
            if (TryGetSelf(out RoOnlineActorSnapshot self)) {
                Log($"self {self.ServerId} cell {self.CellX},{self.CellY} motion {self.Motion} hp {self.Hp}/{self.MaxHp} effect 0x{self.EffectState:x} body {self.BodyState} health {self.HealthState}");
            }

            // the character's own status (the actor's hp above is only what map packets put on the actor)
            Ragnarok.Gameplay.RoPlayerStatusSnapshot own = shell?.ClassicSession?.InventorySnapshot()?.Status;
            if (own != null) {
                Log($"char lv {own.BaseLevel}/{own.JobLevel} job {own.JobId} hp {own.Hp}/{own.MaxHp} sp {own.Sp}/{own.MaxSp} " +
                    $"exp {own.BaseExperience}/{own.NextBaseExperience} jexp {own.JobExperience}/{own.NextJobExperience} zeny {own.Zeny} weight {own.Weight}/{own.MaxWeight}");
            }

            RoLoginShellSnapshot snapshot = shell?.SweepSnapshot();
            if (snapshot == null) return;
            Log($"stage {snapshot.Stage} busy {snapshot.IsBusy} status '{snapshot.StatusText}' error '{snapshot.ErrorText}' lock {snapshot.ActionLockRemainingMilliseconds}ms '{snapshot.ActionLockReason}' skills {snapshot.Gameplay?.Skills?.Length ?? -1}");
            RoPacketTraceEntry[] trace = snapshot.PacketTrace;
            if (trace != null) {
                for (int i = Math.Max(0, trace.Length - 25); i < trace.Length; i++) {
                    RoPacketTraceEntry t = trace[i];
                    Log($"  {t.Timestamp:HH:mm:ss.fff} {t.Direction} 0x{t.PacketId:x4} {t.PacketName} len {t.Length} {t.Status} {t.Message}");
                }
            }
        }

        void OnDestroy() {
            RoSkillSweepProbe.Enabled = false;
            DisposeLoadTest();
            recorder?.Stop(discard: false);
            recorder = null;
            log?.Dispose();
            log = null;
        }

        void Log(string message) {
            string line = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " " + message;
            log?.WriteLine(line);
            Debug.Log("[SkillSweep] " + message);
        }

        static float Now => Time.realtimeSinceStartup;

        IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds, string what, Action<bool> done) {
            float end = Now + timeoutSeconds;
            while (!condition()) {
                if (Now > end) {
                    Log("timeout waiting for " + what);
                    done(false);
                    yield break;
                }

                yield return null;
            }

            done(true);
        }

        IEnumerator Run() {
            Log($"sweep start: {plan.skills?.Length ?? 0} skills, from {plan.startIndex}, plan {planPath}");
            bool ok = false;
            yield return WaitFor(() => (shell = FindFirstObjectByType<RoMinimalLoginShell>()) != null, 60f, "login shell", r => ok = r);
            if (!ok) yield break;
            yield return WaitFor(() => shell.SweepStage == RoLoginShellStage.Login && !shell.SweepIsBusy, 60f, "login stage", r => ok = r);
            if (!ok) yield break;
            yield return new WaitForSecondsRealtime(3f);

            if (!plan.manualLogin) shell.SweepLogin(plan.account, plan.password);
            yield return WaitFor(() => shell.SweepStage == RoLoginShellStage.CharacterSelect && !shell.SweepIsBusy, plan.manualLogin ? 600f : 60f, "character select", r => ok = r);
            if (!ok) yield break;
            yield return new WaitForSecondsRealtime(1.5f);

            if (!plan.manualLogin) shell.SweepEnterMap(plan.character);
            yield return WaitFor(() => {
                if (shell.SweepStage != RoLoginShellStage.MapEntered || shell.SweepIsBusy) return false;
                if (world == null) world = FindFirstObjectByType<RoOnlineWorldController>();
                return world != null && world.OwnActorId != 0 && TryGetSelf(out _);
            }, plan.manualLogin ? 1800f : 180f, "map entry", r => ok = r);
            if (!ok) yield break;
            Log("map entered, own id " + world.OwnActorId);
            yield return new WaitForSecondsRealtime(plan.mapSettleSeconds);

            if (plan.setupCommands != null) {
                foreach (string command in plan.setupCommands) {
                    shell.SweepSendChat(command);
                    yield return new WaitForSecondsRealtime(0.8f);
                }
            }

            if (TryGetSelf(out RoOnlineActorSnapshot start)) {
                home = new Vector2Int(start.CellX, start.CellY);
                hasHome = true;
                Log("home " + home.x + "," + home.y);
            }

            int end = plan.count < 0 ? plan.skills.Length : Math.Min(plan.skills.Length, plan.startIndex + plan.count);
            for (int i = Math.Max(0, plan.startIndex); i < end; i++) {
                if (shell == null || shell.SweepStage != RoLoginShellStage.MapEntered) {
                    Log("left the map, stopping at " + i);
                    break;
                }

                yield return RunSkill(i, plan.skills[i]);
            }

            Log("sweep done");
            try {
                File.Move(planPath, Path.ChangeExtension(planPath, ".done." + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + ".json"));
            } catch (Exception e) {
                Log("could not archive the plan: " + e.Message);
            }

#if UNITY_EDITOR
            if (plan.exitPlayModeWhenDone) {
                UnityEditor.EditorApplication.isPlaying = false;
            }
#endif
        }

        IEnumerator RecordFxCover(string line) {
            Ragnarok.Effects.Classic.RoClassicEffectBridge bridge = Ragnarok.Effects.Classic.RoClassicEffectBridge.Instance;
            if (bridge == null) {
                Log("fxcover: no bridge");
                yield break;
            }

            Ragnarok.Effects.Classic.RoClassicEffectBridge.TraceSources = true;
            yield return null;
            yield return null;
            string[] p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i + 1 < p.Length; i += 2) {
                Log(bridge.DescribeCoverAt(float.Parse(p[i], CultureInfo.InvariantCulture), float.Parse(p[i + 1], CultureInfo.InvariantCulture)));
            }

            Ragnarok.Effects.Classic.RoClassicEffectBridge.TraceSources = false;
        }

        IEnumerator RecordFxBridge(float seconds) {
            Ragnarok.Effects.Classic.RoClassicEffectBridge.CallbackTicks = 0;
            Ragnarok.Effects.Classic.RoClassicEffectBridge.CallbackCount = 0;
            Ragnarok.Effects.Classic.RoClassicEffectBridge.TimeCallbacks = true;
            long join0 = Ragnarok.Effects.Classic.RoClassicEffectBridge.JoinWaitTicks;
            long callers0 = Ragnarok.Effects.Classic.RoClassicEffectBridge.CallerJoins;
            long requests0 = Ragnarok.Effects.Classic.RoClassicEffectBridge.MainRequestsServed;
            long deferred0 = Ragnarok.Effects.Classic.RoClassicEffectBridge.DeferredCalls;
            Ragnarok.Effects.Classic.RoClassicEffectBridge.JoinsByCaller.Clear();
            Ragnarok.Effects.Classic.RoClassicEffectBridge.DeferredByCaller.Clear();
            int frames = 0;
            float end = Now + seconds;
            while (Now < end) {
                yield return null;
                frames++;
            }

            Ragnarok.Effects.Classic.RoClassicEffectBridge.TimeCallbacks = false;
            double ms = Ragnarok.Effects.Classic.RoClassicEffectBridge.CallbackTicks * 1000.0 / Stopwatch.Frequency / Math.Max(1, frames);
            double joinMs = (Ragnarok.Effects.Classic.RoClassicEffectBridge.JoinWaitTicks - join0) * 1000.0 / Stopwatch.Frequency / Math.Max(1, frames);
            Log($"fxbridge over {frames} frames: {ms.ToString("F3", CultureInfo.InvariantCulture)} ms/frame in callbacks, " +
                $"{Ragnarok.Effects.Classic.RoClassicEffectBridge.CallbackCount / Math.Max(1, frames)} calls/frame; threaded " +
                $"{Ragnarok.Effects.Classic.RoClassicEffectBridge.ThreadedRender}: main thread waited {joinMs.ToString("F3", CultureInfo.InvariantCulture)} ms/frame for the job, " +
                $"{Ragnarok.Effects.Classic.RoClassicEffectBridge.CallerJoins - callers0} joins forced by calls, " +
                $"{Ragnarok.Effects.Classic.RoClassicEffectBridge.MainRequestsServed - requests0} main-thread requests, " +
                $"{Ragnarok.Effects.Classic.RoClassicEffectBridge.DeferredCalls - deferred0} calls deferred; waits by caller:" +
                string.Join(",", System.Linq.Enumerable.Select(Ragnarok.Effects.Classic.RoClassicEffectBridge.JoinsByCaller, kv => " " + kv.Key + "=" + kv.Value)) + "; deferred by caller:" +
                string.Join(",", System.Linq.Enumerable.Select(Ragnarok.Effects.Classic.RoClassicEffectBridge.DeferredByCaller, kv => " " + kv.Key + "=" + kv.Value)));
        }

        IEnumerator RecordMarkers(float seconds, string[] names) {
            var recorders = new Unity.Profiling.ProfilerRecorder[names.Length];
            var totalNs = new double[names.Length];
            var totalCalls = new long[names.Length];
            for (int i = 0; i < names.Length; i++) {
                recorders[i] = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, names[i], 1);
            }

            int frames = 0;
            float end = Now + seconds;
            yield return null;
            while (Now < end) {
                yield return null;
                frames++;
                for (int i = 0; i < recorders.Length; i++) {
                    if (!recorders[i].Valid || recorders[i].Count == 0) continue;
                    Unity.Profiling.ProfilerRecorderSample sample = recorders[i].GetSample(0);
                    totalNs[i] += sample.Value;
                    totalCalls[i] += sample.Count;
                }
            }

            var text = new System.Text.StringBuilder("markers over ").Append(frames).Append(" frames (ms/frame and calls/frame, nested markers included):");
            for (int i = 0; i < recorders.Length; i++) {
                text.AppendLine().Append("  ").Append(names[i]).Append(' ')
                    .Append(recorders[i].Valid ? (totalNs[i] / Math.Max(1, frames) / 1e6).ToString("F3", CultureInfo.InvariantCulture) : "not recorded")
                    .Append(' ').Append(totalCalls[i] / Math.Max(1, frames));
                recorders[i].Dispose();
            }

            Log(text.ToString());
        }

        /// <summary>!pvp: the players in sight with their battleground team (0x2dd) and whether a plain click attacks them.</summary>
        string DescribeBattlefieldTeams() {
            if (world == null) world = FindFirstObjectByType<RoOnlineWorldController>();
            if (world == null) return "no world";
            world.SnapshotActors(actors);
            var text = new System.Text.StringBuilder("own " + RoPickHostility.OwnBattlefieldTeam);
            foreach (RoOnlineActorSnapshot a in actors) {
                if (a.ObjectType != RoMapActorObjectType.Player || a.IsSelf) continue;
                text.Append(' ').Append(a.ServerId).Append('=').Append(a.BattlefieldTeam).Append(RoPickHostility.Attacks(a, false) ? "/atk" : "/sel")
                    .Append(" hp ").Append(a.Hp).Append('/').Append(a.MaxHp);
            }

            return text.ToString();
        }

        bool TryGetSelf(out RoOnlineActorSnapshot self) {
            self = default;
            if (world == null) return false;
            if (world.State.TryGetActor(world.OwnActorId, out self)) return true;
            world.SnapshotActors(actors);
            foreach (RoOnlineActorSnapshot a in actors) {
                if (a.IsSelf) {
                    self = a;
                    return true;
                }
            }

            return false;
        }

        bool TryGetActor(uint id, out RoOnlineActorSnapshot actor) {
            actor = default;
            return id != 0 && world != null && world.State.TryGetActor(id, out actor);
        }

        static int Chebyshev(in RoOnlineActorSnapshot a, in RoOnlineActorSnapshot b) =>
            Math.Max(Math.Abs(a.CellX - b.CellX), Math.Abs(a.CellY - b.CellY));

        bool TryFindTarget(in RoOnlineActorSnapshot self, out RoOnlineActorSnapshot target) {
            target = default;
            int best = int.MaxValue;
            world.SnapshotActors(actors);
            foreach (RoOnlineActorSnapshot a in actors) {
                if (a.ObjectType != RoMapActorObjectType.Monster || a.Motion == RoOnlineActorMotion.Dead) continue;
                int d = Chebyshev(self, a);
                if (d < best) {
                    best = d;
                    target = a;
                }
            }

            return best != int.MaxValue;
        }

        uint targetId;

        IEnumerator EnsureTarget() {
            for (int attempt = 0; attempt < 4; attempt++) {
                if (!TryGetSelf(out RoOnlineActorSnapshot self)) yield break;
                if (TryGetActor(targetId, out RoOnlineActorSnapshot current) && current.Motion != RoOnlineActorMotion.Dead &&
                    Chebyshev(self, current) <= 9 && NearHome(current, 6)) {
                    yield break;
                }

                if (TryFindTarget(self, out RoOnlineActorSnapshot found)) {
                    if (Chebyshev(self, found) <= 9 && NearHome(found, 6)) {
                        targetId = found.ServerId;
                        yield break;
                    }

                    shell.SweepSendChat("@killmonster2");
                    yield return new WaitForSecondsRealtime(1f);
                }

                shell.SweepSendChat("@monster " + plan.targetMobId.ToString(CultureInfo.InvariantCulture) + " 1");
                float end = Now + 4f;
                while (Now < end) {
                    yield return null;
                    if (TryGetSelf(out self) && TryFindTarget(self, out found) && Chebyshev(self, found) <= 9) {
                        targetId = found.ServerId;
                        Log("target " + targetId + " at " + found.CellX + "," + found.CellY);
                        yield return new WaitForSecondsRealtime(0.5f);
                        yield break;
                    }
                }
            }

            Log("no target");
        }

        bool NearHome(in RoOnlineActorSnapshot actor, int cells) =>
            !hasHome || Math.Max(Math.Abs(actor.CellX - home.x), Math.Abs(actor.CellY - home.y)) <= cells;

        /// <summary>
        /// Same-map @jump back to the start cell: undoes drift (knock-backs, Back Slide, Body Relocation...) and clears
        /// the server's work-in-progress state that menu skills (Hindsight, Vending, Identify...) leave behind, which
        /// otherwise rejects every following skill with "Any work in progress".
        /// </summary>
        IEnumerator Rehome(string why) {
            if (!hasHome) yield break;
            Log("rehome (" + why + ")");
            shell.SweepSendChat("@jump " + home.x.ToString(CultureInfo.InvariantCulture) + " " + home.y.ToString(CultureInfo.InvariantCulture));
            float end = Now + 8f;
            bool arrived = false;
            yield return new WaitForSecondsRealtime(1f);
            while (Now < end) {
                if (!shell.SweepIsBusy && shell.SweepStage == RoLoginShellStage.MapEntered && TryGetSelf(out RoOnlineActorSnapshot self) &&
                    self.CellX == home.x && self.CellY == home.y) {
                    arrived = true;
                    break;
                }

                yield return null;
            }

            if (!arrived && rehomeMovesClient) {
                // the server moved us but the client kept the old cell: stop chasing the drift (menu skills still get
                // the @jump, which clears the server-side work-in-progress state either way)
                rehomeMovesClient = false;
                Log("rehome: the client did not follow the same-map @jump; drift rehome disabled");
            }

            yield return new WaitForSecondsRealtime(2f);
        }

        IEnumerator RecordClip(float seconds, string name, int width) {
            string ffmpeg = !string.IsNullOrEmpty(plan.ffmpeg) ? plan.ffmpeg : "ffmpeg";
            string dir = Path.Combine(plan.outputDir, "clips", name);
            Directory.CreateDirectory(dir);
            recorder?.Stop(discard: true);
            var r = new ClipRecorder();
            if (!r.Start(ffmpeg, Path.Combine(dir, "f_%04d.png"), width, 60, plan.flipVertical, Log, imageSequence: true)) {
                Log("clip: recorder failed");
                yield break;
            }

            recorder = r;
            clipFrameIndex = 0;
            clipFrameLog = new StreamWriter(Path.Combine(dir, "frames.txt"));
            captureEveryFrame = true;
            int startFrame = Time.frameCount;
            Log("clip: " + name + " recording " + seconds + " s from frame " + startFrame);
            yield return new WaitForSecondsRealtime(seconds);
            captureEveryFrame = false;
            clipFrameLog.Dispose();
            clipFrameLog = null;
            recorder = null;
            yield return r.WaitForPendingReadbacks();
            int frames = r.FramesWritten;
            r.Stop(discard: false);
            Log("clip: " + name + " " + frames + " frames (" + startFrame + ".." + Time.frameCount + ") -> " + dir);
        }

        IEnumerator WalkLoop(int cells, float seconds, bool northSouth = false) {
            if (!TryGetSelf(out RoOnlineActorSnapshot start)) {
                Log("walkloop: no self");
                yield break;
            }

            float end = Now + seconds;
            int legs = 0;
            while (Now < end) {
                int step = legs % 2 == 0 ? cells : -cells;
                if (northSouth) shell.SweepMove(start.CellX, start.CellY + step);
                else shell.SweepMove(start.CellX + step, start.CellY);
                legs++;
                yield return new WaitForSecondsRealtime(0.4f);
                while (Now < end && TryGetSelf(out RoOnlineActorSnapshot self) && self.Motion == RoOnlineActorMotion.Moving) {
                    yield return null;
                }
            }

            Log("walkloop done: " + legs + " legs");
        }

        IEnumerator Approach() {
            for (int attempt = 0; attempt < 6; attempt++) {
                if (!TryGetSelf(out RoOnlineActorSnapshot self) || !TryGetActor(targetId, out RoOnlineActorSnapshot target)) yield break;
                if (Chebyshev(self, target) == 1 && self.Motion != RoOnlineActorMotion.Moving) yield break;

                Vector2Int offset = AdjacentOffsets[attempt % AdjacentOffsets.Length];
                int x = target.CellX + offset.x, y = target.CellY + offset.y;
                shell.SweepMove(x, y);
                float end = Now + 5f;
                yield return new WaitForSecondsRealtime(0.3f);
                while (Now < end && TryGetSelf(out self) && self.Motion == RoOnlineActorMotion.Moving) {
                    yield return null;
                }

                yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        IEnumerator ResetCharacter() {
            if (plan.resetCommands == null) yield break;
            foreach (string command in plan.resetCommands) {
                shell.SweepSendChat(command);
                yield return new WaitForSecondsRealtime(1.2f);
            }

            // let the resurrection effect finish so it does not show up in the next clip
            yield return new WaitForSecondsRealtime(2.5f);
        }

        bool Send(SweepSkill skill, SweepEntry entry, uint selfId) {
            if (skill.remote) {
                // labbot command: cast <id> <lv> self | aid:N | cell:x,y (the sweep's own character is the friend target)
                string target = skill.mode == "enemy" ? "aid:" + entry.targetAid
                    : skill.mode == "friend" ? "aid:" + selfId
                    : skill.mode == "ground" ? "cell:" + entry.groundX + "," + entry.groundY
                    : "self";
                File.AppendAllText(plan.botCommandFile, $"cast {skill.id} {Math.Max(1, skill.level)} {target}\n");
                return true;
            }

            if (skill.inject) {
                StartCoroutine(InjectSkill(skill, entry, selfId));
                return true;
            }

            switch (skill.mode) {
                case "enemy":
                    return shell.SweepUseTargetSkill(skill.id, skill.level, entry.targetAid);
                case "ground":
                    return shell.SweepUseGroundSkill(skill.id, skill.level, entry.groundX, entry.groundY);
                case "friend" when entry.targetAid != 0:
                    return shell.SweepUseTargetSkill(skill.id, skill.level, entry.targetAid);
                default:
                    return shell.SweepUseTargetSkill(skill.id, skill.level, (int) selfId);
            }
        }

        /// <summary>
        /// The packets a successful use of the skill brings, played client-side: ZC_USESKILL_ACK (cast), then after the
        /// cast ZC_NOTIFY_SKILL (enemy), ZC_NOTIFY_GROUNDSKILL (ground) or ZC_USE_SKILL (self / friend).
        /// </summary>
        IEnumerator InjectSkill(SweepSkill skill, SweepEntry entry, uint selfId) {
            ushort id = (ushort) skill.id;
            short level = (short) Math.Max(1, skill.level);
            bool ground = skill.mode == "ground";
            uint target = skill.mode == "enemy" || (skill.mode == "friend" && entry.targetAid != 0) ? (uint) entry.targetAid : ground ? 0u : selfId;
            short x = (short) (ground ? entry.groundX : 0), y = (short) (ground ? entry.groundY : 0);
            uint castMs = (uint) Math.Max(0, skill.castMs);
            world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(
                new RoZcUseSkillAck(selfId, target, (ushort) x, (ushort) y, id, (uint) Math.Max(0, skill.element), castMs, 1, 0)));
            yield return new WaitForSecondsRealtime(castMs / 1000f + 0.05f);
            if (skill.injectDamage || skill.injectResult || skill.injectGround) {
                if (skill.injectDamage && target != 0) {   // a trap's damage comes from its unit, not the cast
                    world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(
                        new RoZcNotifySkill(id, selfId, target, 0, 300, 230, 500, level, 1, 6)));
                }

                if (skill.injectResult) {
                    world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(
                        new RoZcUseSkillResult(id, level, target != 0 ? target : selfId, selfId, 1)));
                }

                if (skill.injectGround) {
                    world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(
                        new RoZcNotifyGroundSkill(id, selfId, level, x, y, 0)));
                }

                if (skill.unitType > 0) {
                    // units of a ground skill sit on its cell, those of a self skill (Desperado) around the caster
                    uint unitId = 0x7F000000u + (uint) (++injectedUnits);
                    world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(
                        new RoZcSkillEntry((ushort) skill.unitType, selfId, ground ? x : (short) entry.selfX, ground ? y : (short) entry.selfY, unitId, 0, 1, (byte) level)));
                    if (skill.triggerTrap) {
                        StartCoroutine(TriggerInjectedTrap(unitId, 1.2f));
                    }

                    StartCoroutine(RemoveInjectedUnit(unitId, plan.postSeconds - 0.5f));
                }
            } else if (skill.mode == "enemy") {
                world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(
                    new RoZcNotifySkill(id, selfId, target, 0, 300, 230, 500, level, 1, 6)));
            } else if (ground) {
                world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(
                    new RoZcNotifyGroundSkill(id, selfId, level, x, y, 0)));
                if (skill.unitType > 0) {
                    uint unitId = 0x7F000000u + (uint) (++injectedUnits);
                    world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(
                        new RoZcSkillEntry((ushort) skill.unitType, selfId, x, y, unitId, 0, 1, (byte) level)));
                    StartCoroutine(RemoveInjectedUnit(unitId, plan.postSeconds - 0.5f));
                }
            } else {
                world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(
                    new RoZcUseSkillResult(id, level, target, selfId, 1)));
            }

            Log($"injected {skill.code} {skill.mode} target={target} cell={x},{y}");
        }

        int injectedUnits;

        /// <summary>Hercules clif->changetraplook: the fired trap's unit turns into UNT_USED_TRAPS (0x8c, LOOK_BASE).</summary>
        IEnumerator TriggerInjectedTrap(uint unitId, float afterSeconds) {
            yield return new WaitForSecondsRealtime(afterSeconds);
            world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(new RoZcSpriteChange(unitId, 0, 0x8c, false)));
            Log("injected trap trigger unit=0x" + unitId.ToString("x"));
        }

        IEnumerator RemoveInjectedUnit(uint unitId, float afterSeconds) {
            yield return new WaitForSecondsRealtime(Math.Max(0.5f, afterSeconds));
            world.InjectFeedbackEventForDiagnostics(new RoMapEntryDrainEvent(new RoZcSkillDisappear(unitId)));
        }

        bool TryFindFriend(out RoOnlineActorSnapshot friend) {
            friend = default;
            if (world == null || string.IsNullOrEmpty(plan.friendName)) return false;
            world.SnapshotActors(actors);
            foreach (RoOnlineActorSnapshot a in actors) {
                if (a.ObjectType == RoMapActorObjectType.Player && !a.IsSelf &&
                    string.Equals(a.Name, plan.friendName, StringComparison.OrdinalIgnoreCase)) {
                    friend = a;
                    return true;
                }
            }

            return false;
        }

        IEnumerator RunSkill(int index, SweepSkill skill) {
            var entry = new SweepEntry {
                index = index, id = skill.id, code = skill.code, name = skill.name, level = skill.level, mode = skill.mode,
            };

            if (TryGetSelf(out RoOnlineActorSnapshot self) && self.Motion == RoOnlineActorMotion.Dead) {
                shell.SweepSendChat("@alive");
                yield return new WaitForSecondsRealtime(1.5f);
            } else if (TryGetSelf(out self) && self.MaxHp > 0 && self.Hp < self.MaxHp / 2) {
                shell.SweepSendChat("@heal");
                yield return new WaitForSecondsRealtime(0.6f);
            }

            if (rehomeMovesClient && TryGetSelf(out self) && !NearHome(self, 7)) {
                yield return Rehome("drifted to " + self.CellX + "," + self.CellY);
            }

            if (skill.mode == "enemy" || skill.mode == "ground") {
                yield return EnsureTarget();
                yield return Approach();
                if (TryGetActor(targetId, out RoOnlineActorSnapshot target)) {
                    entry.targetAid = (int) target.ServerId;
                    entry.targetX = target.CellX;
                    entry.targetY = target.CellY;
                    Vector2Int g = GroundOffsets[groundCounter++ % GroundOffsets.Length];
                    entry.groundX = target.CellX + g.x;
                    entry.groundY = target.CellY + g.y;
                    SnapGroundToWalkable(ref entry.groundX, ref entry.groundY);
                }
            }

            if (skill.mode == "friend") {
                if (TryFindFriend(out RoOnlineActorSnapshot friend)) {
                    entry.targetAid = (int) friend.ServerId;
                    entry.targetX = friend.CellX;
                    entry.targetY = friend.CellY;
                } else {
                    Log("friend '" + plan.friendName + "' not on screen: casting on self");
                }
            }

            if (!TryGetSelf(out self)) {
                entry.outcome = "no-self";
                Append(entry);
                yield break;
            }

            uint selfId = self.ServerId;
            entry.selfX = self.CellX;
            entry.selfY = self.CellY;
            uint casterId = selfId;
            if (skill.remote && !string.IsNullOrEmpty(plan.botCommandFile) && !TryFindFriend(out _)) {
                // the second client was moved away (Intimidate / Snatch teleports caster and target): call it back
                File.AppendAllText(plan.botCommandFile, $"chat @jump {self.CellX + 2} {self.CellY}\n");
                Log("bot off screen: @jump next to the caster");
                yield return new WaitForSecondsRealtime(3f);
            }

            if (skill.remote) {
                if (string.IsNullOrEmpty(plan.botCommandFile) || !TryFindFriend(out RoOnlineActorSnapshot bot)) {
                    entry.outcome = "no-bot";
                    Append(entry);
                    yield break;
                }

                // the second client casts: ground cells next to it, its own skills on itself
                casterId = bot.ServerId;
                if (skill.mode == "ground") {
                    entry.groundX = bot.CellX + 2;
                    entry.groundY = bot.CellY;
                    SnapGroundToWalkable(ref entry.groundX, ref entry.groundY);
                }
            } else if (skill.mode == "ground" && entry.targetAid == 0) {
                entry.groundX = self.CellX + 2;
                entry.groundY = self.CellY;
                SnapGroundToWalkable(ref entry.groundX, ref entry.groundY);
            }

            string clipName = skill.id.ToString(CultureInfo.InvariantCulture) + ".mp4";
            string clipPath = Path.Combine(plan.outputDir, clipName);
            RoSkillSweepProbe.Drain(out _, out _, out _, out _, out _);

            string idKey = "skill=" + skill.id.ToString(CultureInfo.InvariantCulture) + " ";
            string selfKey = "src=" + casterId.ToString(CultureInfo.InvariantCulture) + " ";
            float castEnd = -1f, sendTime = 0f, clipStart = 0f;
            bool failed = false, retryPending = false;
            var failCauses = new List<string>();
            for (int attempt = 0; attempt < 10; attempt++) {
                StartRecorder(clipPath);
                clipStart = Now;
                yield return new WaitForSecondsRealtime(plan.preRollSeconds);
                sendTime = Now;
                entry.sendAttempts++;
                bool accepted;
                try {
                    accepted = Send(skill, entry, selfId);
                } catch (Exception e) {
                    Log("send threw: " + e.Message);
                    accepted = false;
                }

                if (!accepted) {
                    failCauses.Add("send-refused");
                    failed = true;
                    castEnd = Now;
                    break;
                }

                bool retry = false, landed = false;
                int scanned = 0;
                float hardEnd = sendTime + plan.maxCastSeconds + plan.postSeconds;
                castEnd = -1f;
                while (Now < hardEnd) {
                    IReadOnlyList<string> events = RoSkillSweepProbe.Peek();
                    for (; scanned < events.Count; scanned++) {
                        string line = events[scanned];
                        float t = ParseTime(line);
                        if (line.Contains("fail " + idKey) && !line.Contains("success=True")) {
                            int cause = (int) ParseField(line, "cause=");
                            failCauses.Add(cause.ToString(CultureInfo.InvariantCulture));
                            if (landed) {
                                // e.g. Frost Diver's freeze failing after the hit: a note, not a failed cast
                            } else if (cause == SkillIntervalFailCause) {
                                retry = true;
                            } else {
                                failed = true;
                                castEnd = t;
                            }
                        } else if (line.Contains("packet Cast " + idKey) && line.Contains(selfKey)) {
                            float delay = ParseField(line, "delay=") / 1000f;
                            entry.castSeconds = delay;
                            castEnd = t + delay;
                        } else if (line.Contains(" " + idKey) && line.Contains("packet ")) {
                            landed = true;
                            if (castEnd < 0f) castEnd = t;
                        }
                    }

                    if (retry) break;
                    if (castEnd < 0f && Now - sendTime > 1.5f) castEnd = sendTime;
                    if (castEnd >= 0f && Now >= castEnd + (failed ? plan.failPostSeconds : plan.postSeconds)) break;
                    yield return null;
                }

                retryPending = retry;
                if (!retry) break;
                recorder?.Stop(discard: true);
                recorder = null;
                RoSkillSweepProbe.Drain(out _, out _, out _, out _, out _);
                yield return new WaitForSecondsRealtime(0.8f);
            }

            failed |= retryPending;
            entry.clipSeconds = Now - clipStart;
            if (recorder != null) {
                yield return recorder.WaitForPendingReadbacks();
                entry.frames = recorder.FramesWritten;
                recorder.Stop(discard: false);
                recorder = null;
                entry.clip = clipName;
            }

            entry.events = RoSkillSweepProbe.Drain(out entry.classicMaxEffects, out entry.classicMaxPrims,
                out entry.classicMaxBatches, out entry.classicMaxVertices, out entry.classicDrawnFrames);
            entry.outcome = failed ? "fail:" + string.Join(",", failCauses) : failCauses.Count > 0 ? "ok-note:" + string.Join(",", failCauses) : "ok";
            Append(entry);
            Log($"#{index} {skill.id} {skill.code} lv{skill.level} {skill.mode}: {entry.outcome} events={entry.events.Count} classic={entry.classicMaxEffects}/{entry.classicMaxBatches} frames={entry.frames}");

            if (skill.reset || failed) {
                yield return ResetCharacter();
            }

            if (skill.remote && (skill.reset || failed) && !string.IsNullOrEmpty(plan.botCommandFile)) {
                // the caster was the second client: its statuses (Hiding, Cloaking, Trick Dead ...) would block the next
                // casts, so it dies and comes back like the sweep's own character does
                File.AppendAllText(plan.botCommandFile, "chat @die\nwait 600\nchat @alive\n");
                yield return new WaitForSecondsRealtime(1.5f);
            }

            if (skill.rehome) {
                yield return Rehome("menu skill");
            }
        }

        void Append(SweepEntry entry) {
            File.AppendAllText(Path.Combine(plan.outputDir, "report.jsonl"), JsonUtility.ToJson(entry) + "\n");
        }

        static float ParseTime(string line) {
            int space = line.IndexOf(' ');
            return space > 0 && float.TryParse(line.Substring(0, space), NumberStyles.Float, CultureInfo.InvariantCulture, out float t) ? t : Now;
        }

        static long ParseField(string line, string key) {
            int at = line.IndexOf(key, StringComparison.Ordinal);
            if (at < 0) return 0;
            at += key.Length;
            int end = at;
            while (end < line.Length && (char.IsDigit(line[end]) || (end == at && line[end] == '-'))) end++;
            return long.TryParse(line.Substring(at, end - at), NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : 0;
        }

        IEnumerator FxKinds(float seconds) {
            var kinds = new long[6];
            var calls = new long[6];
            var byId = new Dictionary<int, long>();   // kind * 100000 + id
            Ragnarok.Effects.Classic.RoClassicEffectBridge.StepProfiler = (kind, id, ticks) => {
                kinds[(int) kind] += ticks;
                calls[(int) kind]++;
                int key = (int) kind * 100000 + id + 1;
                byId.TryGetValue(key, out long sum);
                byId[key] = sum + ticks;
            };
            int first = Time.frameCount;
            yield return new WaitForSecondsRealtime(seconds);
            Ragnarok.Effects.Classic.RoClassicEffectBridge.StepProfiler = null;
            int frames = Mathf.Max(1, Time.frameCount - first);
            double toMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency / frames;
            var text = new System.Text.StringBuilder("fxkinds over " + frames + " frames (ms/frame, steps/frame):");
            for (int k = 0; k < 6; k++) {
                text.AppendLine().Append("  ").Append((Ragnarok.Effects.Classic.FxProfileKind) k).Append(' ').Append((kinds[k] * toMs).ToString("F3", CultureInfo.InvariantCulture))
                    .Append(" ms, ").Append(calls[k] / frames);
            }

            var top = new List<KeyValuePair<int, long>>(byId);
            top.Sort((a, b) => b.Value.CompareTo(a.Value));
            text.AppendLine().Append("  top:");
            for (int i = 0; i < Mathf.Min(14, top.Count); i++) {
                text.Append(' ').Append((Ragnarok.Effects.Classic.FxProfileKind) (top[i].Key / 100000)).Append('#').Append(top[i].Key % 100000 - 1).Append('=')
                    .Append((top[i].Value * toMs).ToString("F2", CultureInfo.InvariantCulture));
            }

            Log(text.ToString());
        }

        IEnumerator ShotBurst(string name, int frames, RectInt crop) {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "PerformanceCaptures", "ui", "shots"));
            Directory.CreateDirectory(dir);
            var wait = new WaitForEndOfFrame();
            Texture2D cut = null;
            int first = Time.frameCount;
            for (int i = 0; i < frames; i++) {
                yield return wait;
                Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();
                RectInt r = crop.width > 0
                    ? new RectInt(Mathf.Clamp(crop.x, 0, screen.width - 1), 0, 0, 0)
                    : new RectInt(0, 0, screen.width, screen.height);
                if (crop.width > 0) {
                    r.width = Mathf.Min(crop.width, screen.width - r.x);
                    r.height = Mathf.Min(crop.height, screen.height);
                    r.y = Mathf.Clamp(screen.height - crop.y - r.height, 0, screen.height - r.height);
                }

                if (cut == null || cut.width != r.width || cut.height != r.height) {
                    if (cut != null) Destroy(cut);
                    cut = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false);
                }

                cut.SetPixels(screen.GetPixels(r.x, r.y, r.width, r.height));
                cut.Apply(false);
                File.WriteAllBytes(Path.Combine(dir, name + "_" + i.ToString("000", CultureInfo.InvariantCulture) + ".png"), cut.EncodeToPNG());
                Destroy(screen);
            }

            if (cut != null) Destroy(cut);
            Log("shotburst " + name + ": " + frames + " frames from frame " + first + " to " + Time.frameCount);
        }

        IEnumerator Spin(float degreesPerSecond, float seconds) {
            var camera = FindFirstObjectByType<Ragnarok.Camera.RoCameraController>();
            if (camera == null) {
                Log("spin: no camera");
                yield break;
            }

            float end = Now + seconds;
            while (Now < end) {
                camera.TurnForDiagnostics(degreesPerSecond * Time.unscaledDeltaTime);
                yield return null;
            }

            Log("spin done");
        }

        IEnumerator WatchMotion(string who, float seconds) {
            bool self = who == "self";
            uint aid = self ? 0u : uint.Parse(who, CultureInfo.InvariantCulture);
            var presenter = FindFirstObjectByType<Ragnarok.World.RoOnlineActorPresenter>();
            var own = FindFirstObjectByType<Ragnarok.Entities.RoClickToMoveController>();
            float end = Now + seconds, start = Now;
            string last = null;
            while (Now < end) {
                string state = self ? (own != null ? own.DescribeMotionForDiagnostics() : "no controller")
                    : (presenter != null ? presenter.DescribeMotionForDiagnostics(aid) : "no presenter");
                if (state != last) {
                    Log("motion " + who + " +" + ((Now - start) * 1000f).ToString("F0", CultureInfo.InvariantCulture) + "ms " + state);
                    last = state;
                }

                yield return null;
            }
        }

        void StartRecorder(string path) {
            recorder?.Stop(discard: true);
            recorder = null;
            if (!plan.record || string.IsNullOrEmpty(plan.ffmpeg)) return;
            var r = new ClipRecorder();
            if (r.Start(plan.ffmpeg, path, plan.width, plan.fps, plan.flipVertical, Log)) {
                recorder = r;
            }
        }

        IEnumerator CaptureLoop() {
            var endOfFrame = new WaitForEndOfFrame();
            float next = 0f;
            while (true) {
                yield return endOfFrame;
                if (recorder == null || (!captureEveryFrame && Now < next)) continue;
                next = Now + 1f / Mathf.Max(1, plan.fps);
                recorder.Capture();
                if (captureEveryFrame && clipFrameLog != null) {
                    clipFrameLog.WriteLine("f_" + (++clipFrameIndex).ToString("D4", CultureInfo.InvariantCulture) + " frame " + Time.frameCount + " " +
                        (Ragnarok.Entities.RoClientDrawOrder.DescribeCartsForDiagnostics() ?? "-"));
                }
            }
        }

        /// <summary>Screen → downscaled RT → async readback → raw RGBA into ffmpeg's stdin (wall-clock timestamps, CFR out).</summary>
        sealed class ClipRecorder {
            Process process;
            Stream input;
            RenderTexture full;
            RenderTexture small;
            byte[] buffer;
            int pending;
            string path;
            Action<string> log;

            public int FramesWritten { get; private set; }

            public bool Start(string ffmpeg, string outputPath, int width, int fps, bool flip, Action<string> logger, bool imageSequence = false) {
                log = logger;
                path = outputPath;
                int sw = Screen.width, sh = Screen.height;
                if (sw <= 0 || sh <= 0) return false;
                int w = Math.Min(width, sw) / 2 * 2;
                int h = Mathf.RoundToInt(w * sh / (float) sw / 2f) * 2;
                full = new RenderTexture(sw, sh, 0, RenderTextureFormat.ARGB32);
                small = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
                buffer = new byte[w * h * 4];
                string args = imageSequence
                    // one PNG per captured frame, none dropped or doubled (frame-by-frame inspection)
                    ? "-hide_banner -loglevel error -y -f rawvideo -pix_fmt rgba -s " + w + "x" + h + " -i - " + (flip ? "-vf vflip " : "") +
                      "-fps_mode passthrough \"" + outputPath + "\""
                    : "-hide_banner -loglevel error -y -f rawvideo -pix_fmt rgba -s " + w + "x" + h +
                      " -use_wallclock_as_timestamps 1 -i - " + (flip ? "-vf vflip " : "") +
                      "-fps_mode cfr -r " + fps + " -c:v libx264 -preset veryfast -crf 27 -pix_fmt yuv420p -movflags +faststart \"" + outputPath + "\"";
                try {
                    process = Process.Start(new ProcessStartInfo(ffmpeg, args) {
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    });
                    process.ErrorDataReceived += (_, e) => {
                        if (!string.IsNullOrEmpty(e.Data)) Debug.LogWarning("[SkillSweep] ffmpeg: " + e.Data);
                    };
                    process.BeginErrorReadLine();
                    input = process.StandardInput.BaseStream;
                    return true;
                } catch (Exception e) {
                    logger("ffmpeg failed to start: " + e.Message);
                    Release();
                    return false;
                }
            }

            public void Capture() {
                if (input == null) return;
                ScreenCapture.CaptureScreenshotIntoRenderTexture(full);
                Graphics.Blit(full, small);
                pending++;
                AsyncGPUReadback.Request(small, 0, TextureFormat.RGBA32, OnReadback);
            }

            void OnReadback(AsyncGPUReadbackRequest request) {
                pending--;
                if (input == null || request.hasError) return;
                NativeArray<byte> data = request.GetData<byte>();
                if (data.Length != buffer.Length) return;
                data.CopyTo(buffer);
                try {
                    input.Write(buffer, 0, buffer.Length);
                    FramesWritten++;
                } catch (Exception e) {
                    log?.Invoke("ffmpeg pipe: " + e.Message);
                    input = null;
                }
            }

            public IEnumerator WaitForPendingReadbacks() {
                for (int i = 0; i < 30 && pending > 0; i++) yield return null;
            }

            public void Stop(bool discard) {
                try {
                    input?.Flush();
                    input?.Close();
                } catch (Exception) {
                    // ffmpeg already gone
                }

                input = null;
                if (process != null) {
                    if (!process.WaitForExit(10000)) {
                        try { process.Kill(); } catch (Exception) { }
                    }

                    process.Dispose();
                    process = null;
                }

                if (discard && path != null && File.Exists(path)) {
                    File.Delete(path);
                }

                Release();
            }

            void Release() {
                if (full != null) { full.Release(); UnityEngine.Object.Destroy(full); full = null; }
                if (small != null) { small.Release(); UnityEngine.Object.Destroy(small); small = null; }
            }
        }
    }
}
