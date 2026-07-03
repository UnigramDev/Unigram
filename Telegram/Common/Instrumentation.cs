//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Telegram.Common
{
    // Global registry + orphan analysis for the message tree.
    //
    // Detached controls have a null Parent (you can't walk up), so the tree is built TOP-DOWN from the
    // legitimate roots (the live panel containers + the recycle pools) via hardcoded per-type children
    // (each instrumented type implements DebugChildren). Anything registered but not reachable from the
    // roots is orphaned; the orphan set is descended the same way to rebuild the leaked subtrees.
    //
    // Register is [Conditional("INSTRUMENTATION")] so it compiles out of normal builds; the analysis
    // (Snapshot/Analyze, and every DebugChildren / ChatView.DebugAnalyzeOrphans) is #if INSTRUMENTATION.
    public static class Instrumentation
    {
        private static readonly List<WeakReference> s_all = new();
        private static readonly object s_lock = new();

        [Conditional("INSTRUMENTATION")]
        public static void Register(object instance)
        {
            lock (s_lock)
            {
                s_all.Add(new WeakReference(instance));
            }
        }

#if INSTRUMENTATION
        // Live instances; prunes collected ones.
        public static List<object> Snapshot()
        {
            var result = new List<object>();

            lock (s_lock)
            {
                for (int i = s_all.Count - 1; i >= 0; i--)
                {
                    var target = s_all[i].Target;
                    if (target == null)
                    {
                        s_all.RemoveAt(i);
                    }
                    else
                    {
                        result.Add(target);
                    }
                }
            }

            return result;
        }

        // roots: the legitimate holders (live panel containers + recycle pool).
        // getChildren: hardcoded per-type descent (returns instrumented children, nulls allowed).
        public static string Analyze(IEnumerable<object> roots, Func<object, IEnumerable<object>> getChildren)
        {
            var all = Snapshot();

            // 1. Mark everything reachable from the roots.
            var reached = new HashSet<object>(RefEq.Instance);
            var stack = new Stack<object>();

            foreach (var root in roots)
            {
                if (root != null && reached.Add(root))
                {
                    stack.Push(root);
                }
            }

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                foreach (var child in getChildren(node))
                {
                    if (child != null && reached.Add(child))
                    {
                        stack.Push(child);
                    }
                }
            }

            // 2. Orphans = registered but not reachable.
            var orphans = new HashSet<object>(RefEq.Instance);
            foreach (var instance in all)
            {
                if (!reached.Contains(instance))
                {
                    orphans.Add(instance);
                }
            }

            // 3. Rebuild the leaked subtrees by descending the orphan set.
            var childOfOrphan = new HashSet<object>(RefEq.Instance);
            var orphanChildren = new Dictionary<object, List<object>>(RefEq.Instance);

            foreach (var orphan in orphans)
            {
                foreach (var child in getChildren(orphan))
                {
                    if (child != null && orphans.Contains(child))
                    {
                        childOfOrphan.Add(child);

                        if (!orphanChildren.TryGetValue(orphan, out var list))
                        {
                            orphanChildren[orphan] = list = new List<object>();
                        }

                        list.Add(child);
                    }
                }
            }

            // 4. Format: per-type counts, then the orphan forest (roots = orphans with no orphan parent).
            var sb = new System.Text.StringBuilder();

            var liveByType = new Dictionary<string, int>();
            var orphanByType = new Dictionary<string, int>();

            foreach (var instance in all)
            {
                var name = instance.GetType().Name;
                liveByType[name] = liveByType.TryGetValue(name, out var c) ? c + 1 : 1;

                if (orphans.Contains(instance))
                {
                    orphanByType[name] = orphanByType.TryGetValue(name, out var o) ? o + 1 : 1;
                }
            }

            sb.AppendLine("=== Instrumentation: live / orphaned by type ===");
            foreach (var pair in liveByType)
            {
                orphanByType.TryGetValue(pair.Key, out var orph);
                sb.AppendLine($"  {pair.Key}: live={pair.Value}, orphaned={orph}");
            }

            sb.AppendLine("=== Orphan trees (topmost orphan = leak root) ===");
            foreach (var orphan in orphans)
            {
                if (!childOfOrphan.Contains(orphan))
                {
                    AppendTree(sb, orphan, orphanChildren, 0);
                }
            }

            return sb.ToString();
        }

        private static void AppendTree(System.Text.StringBuilder sb, object node, Dictionary<object, List<object>> children, int depth)
        {
            sb.Append(' ', depth * 2);
            sb.Append(node.GetType().Name);
            sb.Append(' ');
            sb.AppendLine(AddressOf(node).ToString("x16"));

            if (children.TryGetValue(node, out var list))
            {
                foreach (var child in list)
                {
                    AppendTree(sb, child, children, depth + 1);
                }
            }
        }

        private static unsafe long AddressOf(object o)
        {
            return (long)*(IntPtr*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref o);
        }

        // Reference-identity comparer (don't rely on overridden Equals/GetHashCode of XAML types).
        private sealed class RefEq : IEqualityComparer<object>
        {
            public static readonly RefEq Instance = new();
            bool IEqualityComparer<object>.Equals(object a, object b) => ReferenceEquals(a, b);
            int IEqualityComparer<object>.GetHashCode(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        }
#endif
    }
}
