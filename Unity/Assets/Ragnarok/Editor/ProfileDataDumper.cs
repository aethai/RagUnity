using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

public static class ProfileDataDumper {
    [MenuItem("Tools/Dump Profile to Text")]
    public static void Dump() {
        var path = EditorUtility.SaveFilePanel("Save profile dump", "", "profile-dump.txt", "txt");
        if (string.IsNullOrEmpty(path)) return;

        var sb = new StringBuilder();
        int firstFrame = ProfilerDriver.firstFrameIndex;
        int lastFrame = ProfilerDriver.lastFrameIndex;
        sb.AppendLine($"Profile range: frames {firstFrame}..{lastFrame}");
        sb.AppendLine($"Total frames: {lastFrame - firstFrame + 1}");
        sb.AppendLine();

        int step = Mathf.Max(1, (lastFrame - firstFrame + 1) / 40);
        for (int frame = firstFrame; frame <= lastFrame; frame += step) {
            sb.AppendLine($"========== FRAME {frame} ==========");
            DumpFrame(sb, frame, HierarchyFrameDataView.columnGcMemory, "BY GC ALLOC");
            DumpFrame(sb, frame, HierarchyFrameDataView.columnTotalTime, "BY TOTAL TIME");
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString());
        Debug.Log($"Profile dump written: {path}");
        EditorUtility.RevealInFinder(path);
    }

    private static void DumpFrame(StringBuilder sb, int frame, int sortColumn, string label) {
        using (var data = ProfilerDriver.GetHierarchyFrameDataView(
                   frame, 0,
                   HierarchyFrameDataView.ViewModes.Default,
                   sortColumn,
                   false)) {
            if (data == null || !data.valid) {
                sb.AppendLine($"-- {label}: frame data not valid --");
                return;
            }

            sb.AppendLine($"-- {label} --");
            var root = data.GetRootItemID();
            var children = new List<int>();
            data.GetItemChildren(root, children);
            DumpItems(sb, data, children, depth: 0, maxItems: 20, maxDepth: 4);
        }
    }

    private static void DumpItems(StringBuilder sb, HierarchyFrameDataView data,
                                   List<int> items, int depth, int maxItems, int maxDepth) {
        int count = Mathf.Min(items.Count, maxItems);
        for (int i = 0; i < count; i++) {
            int id = items[i];
            string name = data.GetItemName(id);
            float self = data.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnSelfTime);
            float total = data.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnTotalTime);
            float gc = data.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnGcMemory);
            float calls = data.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnCalls);

            sb.Append(' ', depth * 2);
            sb.AppendLine($"{name}  self={self:F2}ms total={total:F2}ms gc={gc:F0}B calls={calls:F0}");

            if (depth < maxDepth) {
                var children = new List<int>();
                data.GetItemChildren(id, children);
                if (children.Count > 0) {
                    DumpItems(sb, data, children, depth + 1, maxItems: 8, maxDepth);
                }
            }
        }
    }
}