#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;

namespace AssetDoctor;

/// <summary>Finds direct-reference cycles using an iterative depth-first traversal.</summary>
public static class AssetReferenceCycleDetector
{
    /// <summary>Represents the traversal state for one active graph node.</summary>
    private sealed class Frame
    {
        /// <summary>Initializes a frame with the supplied dependency list.</summary>
        public Frame(string node, string[] dependencies) { Node = node; Dependencies = dependencies; }
        /// <summary>Gets the active node path.</summary>
        public string Node { get; }
        /// <summary>Gets dependencies to visit.</summary>
        public string[] Dependencies { get; }
        /// <summary>Gets or sets the next dependency index.</summary>
        public int NextIndex { get; set; }
    }

    /// <summary>Finds cycles in a graph and returns a single finding per back-edge path.</summary>
    public static List<Finding> Find(IReadOnlyDictionary<string, HashSet<string>> graph, CancellationToken token = default)
    {
        if(graph == null) throw new ArgumentNullException(nameof(graph));
        var findings = new List<Finding>();
        var states = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var active = new List<string>();
        var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var frames = new List<Frame>();
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var start in graph.Keys)
        {
            token.ThrowIfCancellationRequested();
            if(states.ContainsKey(start)) continue;
            Push(start);
            while(frames.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var frame = frames[^1];
                if(frame.NextIndex >= frame.Dependencies.Length)
                {
                    frames.RemoveAt(frames.Count - 1); positions.Remove(frame.Node); active.RemoveAt(active.Count - 1); states[frame.Node] = 2; continue;
                }
                var dependency = frame.Dependencies[frame.NextIndex++];
                if(!states.TryGetValue(dependency, out var state)) { Push(dependency); continue; }
                if(state != 1 || !positions.TryGetValue(dependency, out var startIndex)) continue;
                var cycle = active.GetRange(startIndex, active.Count - startIndex);
                var signature = string.Join("\u001F", cycle);
                if(!emitted.Add(signature)) continue;
                cycle.Add(dependency);
                findings.Add(new Finding("AD104", FindingSeverity.Error, "Circular asset reference detected.", frame.Node, dependency, Details: string.Join(" → ", cycle)));
            }
        }
        return findings;

        void Push(string node)
        {
            states[node] = 1; positions[node] = active.Count; active.Add(node);
            var dependencies = graph.TryGetValue(node, out var values) && values != null ? new List<string>(values).ToArray() : Array.Empty<string>();
            Array.Sort(dependencies, StringComparer.OrdinalIgnoreCase);
            frames.Add(new Frame(node, dependencies));
        }
    }
}
