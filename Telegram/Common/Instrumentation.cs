//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
#if INSTRUMENTATION
using System.Threading.Tasks;
using Telegram.Navigation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
#endif

namespace Telegram.Common
{
    // Global registry + orphan analysis for the app's controls.
    //
    // Every UIElement the app declares registers itself: Tools\Instrument weaves the call into the
    // constructor of the topmost app type of each hierarchy, so there is nothing to keep in sync by
    // hand. Register is [Conditional("INSTRUMENTATION")] and the weaver only runs under the same
    // opt-in, so a normal build has neither the calls nor the registry traffic.
    //
    // Detached controls have a null Parent (you can't walk up), so reachability is built TOP-DOWN,
    // from each window's XamlRoot and its open popups, through VisualTreeHelper. Anything registered
    // and not reached is an orphan, and the orphan set is descended the same way to rebuild the
    // leaked subtrees (topmost orphan = leak root).
    //
    // XAML objects are thread-affine and every view has its own thread, so both halves are per
    // thread: the registry is [ThreadStatic], which is also what keeps the hot path lock-free, and
    // the mark phase is dispatched to each window's thread and merged.
    public static class Instrumentation
    {
        // One per thread that has ever registered. Appended to, and compacted, ONLY by its own
        // thread - a lock here would be taken once per control constructed.
        private sealed class ThreadRegistry
        {
            public readonly int ThreadId = Environment.CurrentManagedThreadId;

            public readonly List<WeakReference> Instances = new(1024);

            // Dead entries are dropped once the list has doubled: every weak handle is walked by
            // every GC, so a session that scrolls a few hundred thousand controls cannot keep them.
            public int Threshold = 1024;

#if INSTRUMENTATION
            // Filled by an analysis pass, on the owning thread wherever there is still a window to
            // dispatch to. A registry with live instances and no window is itself the leak.
            public List<object> Live;
            public List<object> Orphans;
            public bool Claimed;
            public bool Walked;

            // The tree as the thread last saw it, taken at teardown and held WEAKLY: a strong map
            // here would pin every control it describes and make the diagnostic the leak.
            // Entry [0] is the parent, the rest are its registered children.
            public List<WeakReference[]> Captured;

            // A queue that refused a walk once is a thread that has stopped pumping; it will not
            // start again, so it is not asked twice.
            public bool Unreachable;

            // What the thread is, for the report: the windows it was found running, main view first.
            public string Windows;
            public bool IsMain;
#endif
        }

        /// <summary>
        /// The children a node keeps OFF the visual tree - the recycle pools. Set once by MainPage;
        /// both the analysis and the teardown capture read it.
        /// </summary>
        public static Func<object, IEnumerable<object>> Detached;

        [ThreadStatic]
        private static ThreadRegistry t_registry;

        private static readonly List<ThreadRegistry> s_registries = new();

        [Conditional("INSTRUMENTATION")]
        public static void Register(object instance)
        {
            var registry = t_registry;
            if (registry == null)
            {
                t_registry = registry = new ThreadRegistry();

                // Once per thread rather than once per object, which is the whole point of the
                // thread-static list.
                lock (s_registries)
                {
                    s_registries.Add(registry);
                }
            }

            registry.Instances.Add(new WeakReference(instance));

            if (registry.Instances.Count >= registry.Threshold)
            {
                Compact(registry);
            }
        }

        /// <summary>
        /// Records the tree this thread holds, on the way out. Call it from the window's own close
        /// path, on the window's own thread.
        /// </summary>
        /// <remarks>
        /// A closed view stops pumping its DispatcherQueue, and from any other thread its controls
        /// can then be counted but never walked - every DependencyObject read is wrong-thread. So
        /// the structure is taken here, at the last moment it can be, and the analysis uses it to
        /// rebuild what the thread left behind. Without it a leaked view reports as one orphan per
        /// control rather than as the one tree it is.
        /// </remarks>
        [Conditional("INSTRUMENTATION")]
        public static void Capture()
        {
#if INSTRUMENTATION
            var registry = t_registry;
            if (registry == null)
            {
                return;
            }

            var captured = new List<WeakReference[]>();

            foreach (var pair in ChildMap(Snapshot(registry), Detached))
            {
                var node = new WeakReference[pair.Value.Count + 1];
                node[0] = new WeakReference(pair.Key);

                for (int i = 0; i < pair.Value.Count; i++)
                {
                    node[i + 1] = new WeakReference(pair.Value[i]);
                }

                captured.Add(node);
            }

            registry.Captured = captured;
#endif
        }

        private static void Compact(ThreadRegistry registry)
        {
            var instances = registry.Instances;
            var count = 0;

            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i].IsAlive)
                {
                    instances[count++] = instances[i];
                }
            }

            instances.RemoveRange(count, instances.Count - count);
            registry.Threshold = Math.Max(1024, count * 2);
        }

#if INSTRUMENTATION
        /// <summary>
        /// Registered controls that no window can reach, as a report.
        /// </summary>
        public static async Task<string> AnalyzeAsync()
        {
            var detached = Detached ?? Nothing;

            ThreadRegistry[] registries;

            lock (s_registries)
            {
                registries = s_registries.ToArray();
            }

            // Nothing at all is registered when the weaver did not run, and the report would say the
            // whole app has leaked. It is a step of Telegram.Modern.csproj and of no other flavour.
            if (registries.Length == 0)
            {
                return "=== Instrumentation: nothing registered, so Tools\\Instrument did not run ===" + Environment.NewLine;
            }

            // Windows is appended to per window, so a second pass in the same session would report
            // the same view twice without this.
            foreach (var registry in registries)
            {
                registry.Live = null;
                registry.Orphans = null;
                registry.Windows = null;
                registry.Claimed = false;
                registry.Walked = false;
                registry.IsMain = false;
            }

            var reached = new HashSet<object>(RefEq.Instance);
            var children = new Dictionary<object, List<object>>(RefEq.Instance);

            // Once per window, on the window's own thread: VisualTreeHelper answers only for the
            // thread that owns the element, and a secondary view is a thread of its own.
            var marking = WindowContext.ForEachAsync(window => MarkWindow(window, detached, reached, children));

            // A view thread that never answers is a hang, not a reason to lose the report - and the
            // merge is locked, so one arriving late costs nothing but its own window's subtree.
            var completed = await Task.WhenAny(marking, Task.Delay(5000));

            var note = completed != marking
                ? "!! a window did not answer in time, so its subtree is reported as orphaned"
                : marking.IsFaulted
                ? "!! a window's walk threw, so its subtree is reported as orphaned: " + marking.Exception?.GetBaseException().Message
                : null;

            // A window can be gone from WindowContext.All while its thread is still pumping - Detach
            // runs long before a view is consolidated - and only the owning thread may walk what it
            // holds. Worth one ask each: it is the difference between one leaked view and 172
            // unrelated orphans, and whether the thread answers at all is itself the finding.
            var pending = new List<Task>();

            if (pending.Count > 0)
            {
                // Short: a closed view's queue refuses the work outright, and one that takes it and
                // never runs it has stopped pumping, which no amount of waiting fixes.
                await Task.WhenAny(Task.WhenAll(pending), Task.Delay(1500));
            }

            foreach (var registry in registries)
            {
                // Never answered: whatever it managed to tell us on the way out is all there is.
                if (!registry.Walked)
                {
                    Restore(registry, children);
                }
            }

            var live = 0;
            var orphans = new HashSet<object>(RefEq.Instance);

            // Still locked: a thread that answers after the timeout is writing into the same sets.
            lock (children)
            {
                foreach (var registry in registries)
                {
                    // The owning thread had no window to dispatch to, so nobody snapshotted it.
                    registry.Live ??= SnapshotForeign(registry);
                    registry.Orphans = new List<object>();

                    live += registry.Live.Count;

                    foreach (var instance in registry.Live)
                    {
                        if (!reached.Contains(instance))
                        {
                            registry.Orphans.Add(instance);
                            orphans.Add(instance);
                        }
                    }
                }
            }

            // Main view first, then by thread id, so the same window reads in the same place twice
            // running. A thread that registered nothing is not worth a section.
            var threads = new List<ThreadRegistry>();

            foreach (var registry in registries)
            {
                if (registry.Live.Count > 0)
                {
                    threads.Add(registry);
                }
            }

            threads.Sort(CompareThreads);

            var report = Format(threads, live, orphans, children, note);

            // NOTHING may outlive the report. A strong list of every control the pass saw roots all
            // of it until the next pass, which then finds it alive and reports it again - the tool
            // describing its own grip, and a leak that cannot be disproved. Proven from a dump: the
            // only root of a "leaked" ChatView was this list. Captured is weak for the same reason.
            foreach (var registry in registries)
            {
                registry.Live = null;
                registry.Orphans = null;
            }

            return report;
        }

        // Everything this window's thread owns: what it can reach, and what it holds.
        private static void MarkWindow(WindowContext window, Func<object, IEnumerable<object>> detached, HashSet<object> reached, Dictionary<object, List<object>> children)
        {
            var registry = t_registry;

            // Two windows can share a thread, and they share its registry with it.
            var live = Claim(registry);

            if (registry != null)
            {
                // WindowContext.Content here, not the shell the walk starts from: MainPage and
                // WebAppWindow name the thread, WindowContent does not.
                var name = (window.Content ?? window.XamlRoot?.Content)?.GetType().Name ?? "no content";

                registry.Windows = registry.Windows == null ? name : registry.Windows + ", " + name;
                registry.IsMain |= window.IsInMainView;
            }

            var local = new HashSet<object>(RefEq.Instance);
            var stack = new Stack<object>();

            // XamlRoot.Content, not WindowContext.Content: that one returns what is inside the shell,
            // and the shell - title bar, popup host - is instrumented too.
            var root = window.XamlRoot;
            Push(local, stack, root?.Content);

            if (root != null)
            {
                foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(root))
                {
                    Push(local, stack, popup);
                }
            }

            while (stack.Count > 0)
            {
                foreach (var child in ChildrenOf(stack.Pop(), detached))
                {
                    Push(local, stack, child);
                }
            }

            var owned = ChildMap(live, detached);

            // children is the monitor for both, so that the pass below - which has no reachability
            // to merge and never sees this set - takes the same one.
            lock (children)
            {
                foreach (var node in local)
                {
                    reached.Add(node);
                }

                Merge(children, owned);
            }
        }

        // What the thread recorded on its way out, for the threads that can no longer be asked.
        // Weak, so resolving it is also what drops whatever has been collected since.
        private static void Restore(ThreadRegistry registry, Dictionary<object, List<object>> children)
        {
            if (registry.Captured == null)
            {
                return;
            }

            foreach (var node in registry.Captured)
            {
                if (node[0].Target is not object parent)
                {
                    continue;
                }

                List<object> list = null;

                for (int i = 1; i < node.Length; i++)
                {
                    if (node[i].Target is object child)
                    {
                        list ??= new List<object>();
                        list.Add(child);
                    }
                }

                if (list != null)
                {
                    children[parent] = list;
                }
            }
        }

        // The thread's own registrations, snapshotted on the thread that owns them. Null when this
        // thread was already claimed - by an earlier window of the same thread, or by the pass below.
        private static List<object> Claim(ThreadRegistry registry)
        {
            if (registry == null || registry.Claimed)
            {
                return null;
            }

            registry.Claimed = true;
            registry.Walked = true;

            return registry.Live = Snapshot(registry);
        }

        // The parent-child relation, needed to rebuild the leaked subtrees later. It is built on the
        // owning thread, because by the time the orphans are known the analysis is back on its own.
        //
        // A registered object's children are the NEAREST REGISTERED DESCENDANTS, not its immediate
        // visual children: between any two of the app's own controls there are always framework
        // elements - a Grid, a ContentPresenter, an ItemsStackPanel - and stopping at those leaves
        // every control a root of its own. Each unregistered node is still walked once, by the one
        // registered ancestor that reaches it.
        private static Dictionary<object, List<object>> ChildMap(List<object> live, Func<object, IEnumerable<object>> detached)
        {
            detached ??= Nothing;

            var owned = new Dictionary<object, List<object>>(RefEq.Instance);

            if (live == null)
            {
                return owned;
            }

            var registered = new HashSet<object>(live, RefEq.Instance);
            var seen = new HashSet<object>(RefEq.Instance);
            var stack = new Stack<object>();

            foreach (var instance in live)
            {
                List<object> list = null;

                seen.Clear();
                stack.Clear();

                foreach (var child in ChildrenOf(instance, detached))
                {
                    if (child != null && seen.Add(child))
                    {
                        stack.Push(child);
                    }
                }

                while (stack.Count > 0)
                {
                    var node = stack.Pop();

                    // Its own entry carries everything below it, so the descent stops here.
                    if (registered.Contains(node))
                    {
                        list ??= new List<object>();
                        list.Add(node);
                        continue;
                    }

                    foreach (var child in ChildrenOf(node, detached))
                    {
                        if (child != null && seen.Add(child))
                        {
                            stack.Push(child);
                        }
                    }
                }

                if (list != null)
                {
                    owned[instance] = list;
                }
            }

            return owned;
        }

        private static void Merge(Dictionary<object, List<object>> children, Dictionary<object, List<object>> owned)
        {
            foreach (var pair in owned)
            {
                children[pair.Key] = pair.Value;
            }
        }

        private static IEnumerable<object> Nothing(object node)
        {
            return Array.Empty<object>();
        }

        private static void Push(HashSet<object> reached, Stack<object> stack, object node)
        {
            if (node != null && reached.Add(node))
            {
                stack.Push(node);
            }
        }

        // The visual tree, plus the three places a control is held where the visual tree does not
        // reach it: a popup's child, a flyout's content, and the caller's own pools.
        private static IEnumerable<object> ChildrenOf(object node, Func<object, IEnumerable<object>> detached)
        {
            if (node is DependencyObject element)
            {
                var count = VisualTreeHelper.GetChildrenCount(element);

                for (int i = 0; i < count; i++)
                {
                    yield return VisualTreeHelper.GetChild(element, i);
                }
            }

            // A popup's child is not a visual child of it, and a closed popup still owns one.
            if (node is Popup popup)
            {
                yield return popup.Child;
            }
            else if (node is RichTextBlock rich)
            {
                foreach (var block in rich.Blocks)
                {
                    if (block is Paragraph paragraph)
                    {
                        foreach (var child in HostedBy(paragraph.Inlines))
                        {
                            yield return child;
                        }
                    }
                }
            }
            else if (node is MenuFlyoutSubItem sub)
            {
                foreach (var item in sub.Items)
                {
                    yield return item;
                }
            }
            else if (node is FrameworkElement framework)
            {
                foreach (var child in ContentOf(FlyoutBase.GetAttachedFlyout(framework)))
                {
                    yield return child;
                }

                if (framework is Button button)
                {
                    foreach (var child in ContentOf(button.Flyout))
                    {
                        yield return child;
                    }
                }
            }

            foreach (var child in detached(node))
            {
                yield return child;
            }
        }

        // A UIElement in a text run is hosted by an InlineUIContainer, which is a TextElement and so
        // not a visual child of the block that draws it: every custom emoji in a message is here.
        private static IEnumerable<object> HostedBy(InlineCollection inlines)
        {
            foreach (var inline in inlines)
            {
                if (inline is InlineUIContainer container)
                {
                    yield return container.Child;
                }
                else if (inline is Span span)
                {
                    foreach (var child in HostedBy(span.Inlines))
                    {
                        yield return child;
                    }
                }
            }
        }

        // A closed flyout is in no tree at all, and it outlives the opening that built its content:
        // it is reached through the element it hangs off instead.
        private static IEnumerable<object> ContentOf(FlyoutBase flyout)
        {
            if (flyout is Flyout content)
            {
                yield return content.Content;
            }
            else if (flyout is MenuFlyout menu)
            {
                foreach (var item in menu.Items)
                {
                    yield return item;
                }
            }
        }

        // On the owning thread, so the list can be compacted while it is read.
        private static List<object> Snapshot(ThreadRegistry registry)
        {
            Compact(registry);

            var result = new List<object>(registry.Instances.Count);

            foreach (var reference in registry.Instances)
            {
                if (reference.Target is object instance)
                {
                    result.Add(instance);
                }
            }

            return result;
        }

        // The owning thread has no window to dispatch to, so this races whatever it is still doing.
        // Appends only grow the list at the end, and the indexer bound-checks, so the worst of it is
        // a missed entry - except against a compaction, which does shrink it.
        private static List<object> SnapshotForeign(ThreadRegistry registry)
        {
            var instances = registry.Instances;
            var result = new List<object>();

            try
            {
                for (int i = 0; i < instances.Count; i++)
                {
                    if (instances[i].Target is object instance)
                    {
                        result.Add(instance);
                    }
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // Compacted under us: what was collected up to here is still worth reporting.
            }

            return result;
        }

        // Main view first, then by thread id: the same window then reads in the same place from one
        // report to the next.
        private static int CompareThreads(ThreadRegistry x, ThreadRegistry y)
        {
            if (x.IsMain != y.IsMain)
            {
                return x.IsMain ? -1 : 1;
            }

            return x.ThreadId.CompareTo(y.ThreadId);
        }

        // Most orphaned first: the row to read is the one with a number in it.
        private static int CompareTypes(KeyValuePair<string, int[]> x, KeyValuePair<string, int[]> y)
        {
            if (x.Value[1] != y.Value[1])
            {
                return y.Value[1].CompareTo(x.Value[1]);
            }

            if (x.Value[0] != y.Value[0])
            {
                return y.Value[0].CompareTo(x.Value[0]);
            }

            return string.CompareOrdinal(x.Key, y.Key);
        }

        // One section per thread. XAML objects are thread-affine, so a thread is the whole story of
        // a window: mixing them only makes two views look like one leaking app.
        private static string Format(List<ThreadRegistry> threads, int live, HashSet<object> orphans, Dictionary<object, List<object>> children, string note)
        {
            // The leaked subtrees: an orphan whose parent is an orphan hangs under it, so what is left
            // at the top of a tree is the object actually holding the rest alive.
            var childOfOrphan = new HashSet<object>(RefEq.Instance);
            var orphanChildren = new Dictionary<object, List<object>>(RefEq.Instance);

            foreach (var orphan in orphans)
            {
                if (!children.TryGetValue(orphan, out var all))
                {
                    continue;
                }

                foreach (var child in all)
                {
                    if (orphans.Contains(child))
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

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Instrumentation: {live} live, {orphans.Count} orphaned, over {threads.Count} thread(s) ===");

            if (note != null)
            {
                sb.AppendLine("  " + note);
            }

            var byType = new Dictionary<string, int[]>();
            var rows = new List<KeyValuePair<string, int[]>>();

            foreach (var registry in threads)
            {
                var roots = 0;

                foreach (var orphan in registry.Orphans)
                {
                    if (!childOfOrphan.Contains(orphan))
                    {
                        roots++;
                    }
                }

                sb.Append("--- thread ").Append(registry.ThreadId).Append(": ");

                // No window means nothing roots this thread and all of it is orphaned by definition.
                // Where the STRUCTURE came from is the other half, and it decides how the trees below
                // read: walked now, taken at teardown, or nowhere - in which case every object is a
                // tree of its own and the count above says far more than there is.
                sb.Append(registry.Windows ?? (registry.Walked
                    ? "no window, walked on its own thread"
                    : registry.Captured != null
                    ? "no window, tree as its thread left it"
                    : "NO WINDOW, AND NO TREE: every object below is its own root"));

                if (registry.IsMain)
                {
                    sb.Append(" (main view)");
                }

                sb.Append(" - ").Append(registry.Live.Count).Append(" live, ");
                sb.Append(registry.Orphans.Count).Append(" orphaned in ");
                sb.Append(roots).AppendLine(" tree(s) ---");

                byType.Clear();
                rows.Clear();

                foreach (var instance in registry.Live)
                {
                    var name = instance.GetType().Name;

                    if (!byType.TryGetValue(name, out var counts))
                    {
                        byType[name] = counts = new int[2];
                    }

                    counts[0]++;
                }

                foreach (var orphan in registry.Orphans)
                {
                    byType[orphan.GetType().Name][1]++;
                }

                foreach (var pair in byType)
                {
                    rows.Add(pair);
                }

                rows.Sort(CompareTypes);

                foreach (var row in rows)
                {
                    sb.AppendLine($"  {row.Key}: live={row.Value[0]}, orphaned={row.Value[1]}");
                }

                if (registry.Orphans.Count > 0)
                {
                    sb.AppendLine("  orphan trees (topmost orphan = leak root):");

                    foreach (var orphan in registry.Orphans)
                    {
                        if (!childOfOrphan.Contains(orphan))
                        {
                            AppendTree(sb, orphan, orphanChildren, 1);
                        }
                    }
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
