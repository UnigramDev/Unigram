//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Td.Api;

namespace Telegram.Controls.RichEditor
{
    public sealed partial class RichEditorEngine
    {
        /// <summary>Media in the document, a collage's items each: what rich_message_media_count_max counts.</summary>
        public int CountMedia()
        {
            var count = 0;
            foreach (var block in _document.AllBlocks)
            {
                if (block is RichMediaBlock)
                {
                    count++;
                }
            }

            return count;
        }

        public void SetSpoiler(RichMediaBlock media, bool value)
        {
            if (media.HasSpoiler == value)
            {
                return;
            }

            BeginChange();
            media.HasSpoiler = value;
            EndChange();
        }

        /// <summary>The file behind a media block, swapped: Replace, or the media editor's result.</summary>
        public void SetSource(RichMediaBlock media, RichMediaSource source)
        {
            if (source == null)
            {
                return;
            }

            BeginChange();
            media.Source = source;
            EndChange();
        }

        /// <summary>The media block right after <paramref name="block"/> among its siblings, if it is one.</summary>
        public RichBlock GetAdjacentMedia(RichBlock block, int direction)
        {
            var container = _document.GetContainer(block, out int index);
            if (container == null)
            {
                return null;
            }

            var target = index + direction;
            if (target < 0 || target >= container.Count)
            {
                return null;
            }

            return container[target] is RichMediaBlock { Source.Kind: RichMediaKind.Photo or RichMediaKind.Video or RichMediaKind.Animation } or RichMediaGroupBlock
                ? container[target]
                : null;
        }

        /// <summary>
        /// A photo or video joined with the one beside it into a collage - or into the collage
        /// beside it. Captions of joined items move under the group, a line each, as a group
        /// has one caption and its items none.
        /// </summary>
        public void Group(RichBlock block, int direction)
        {
            var other = GetAdjacentMedia(block, direction);
            if (other == null || !IsVisual(block))
            {
                return;
            }

            BeginChange();

            var container = _document.GetContainer(block, out int index);
            var first = direction < 0 ? other : block;
            var second = direction < 0 ? block : other;

            RichMediaGroupBlock group;

            if (first is RichMediaGroupBlock existing)
            {
                group = existing;
            }
            else
            {
                group = new RichMediaGroupBlock(false);
                container[container.IndexOf(first)] = group;
                group.Parent = first.Parent;
                AddToGroup(group, first);
            }

            container.Remove(second);
            AddToGroup(group, second);

            _node = group;

            EndChange();
            Raise(RichEditorChanges.Selection, null);
        }

        private static bool IsVisual(RichBlock block)
        {
            return block is RichMediaBlock media && CanGroup(media.Source) || block is RichMediaGroupBlock;
        }

        /// <summary>Whether it can be an item of a collage: a photo or a video, a GIF among them.</summary>
        public static bool CanGroup(RichMediaSource source)
        {
            return source?.Kind is RichMediaKind.Photo or RichMediaKind.Video or RichMediaKind.Animation;
        }

        private static void AddToGroup(RichMediaGroupBlock group, RichBlock block)
        {
            switch (block)
            {
                case RichMediaBlock media:
                    MoveCaption(media.Caption, group.Caption);
                    MoveCaption(media.Credit, group.Credit);
                    media.Caption.Delete(0, media.Caption.Length);
                    media.Credit.Delete(0, media.Credit.Length);
                    group.Items.Add(media);
                    break;
                case RichMediaGroupBlock other:
                    MoveCaption(other.Caption, group.Caption);
                    MoveCaption(other.Credit, group.Credit);
                    group.Items.AddRange(other.Items);
                    break;
            }
        }

        private static void MoveCaption(RichTextLeaf from, RichTextLeaf to)
        {
            if (from.IsEmpty)
            {
                return;
            }

            if (!to.IsEmpty)
            {
                to.Insert(to.Length, "\n", RichMarks.None, null);
            }

            to.Append(from);
        }

        /// <summary>A collage or slideshow back to one block per item; the group's caption goes to the first.</summary>
        public void Ungroup(RichMediaGroupBlock group)
        {
            BeginChange();

            var container = _document.GetContainer(group, out int index);
            container.RemoveAt(index);

            for (int i = 0; i < group.Items.Count; i++)
            {
                if (group.Items[i] is RichMediaBlock media)
                {
                    if (i == 0)
                    {
                        media.Caption.SetContent(group.Caption);
                        media.Credit.SetContent(group.Credit);
                    }

                    container.Insert(index + i, media);
                }
            }

            _node = null;
            EndChange();

            if (group.Items.Count > 0)
            {
                SelectNode(group.Items[0]);
            }
        }

        public void SetSlideshow(RichMediaGroupBlock group, bool value)
        {
            if (group.IsSlideshow == value)
            {
                return;
            }

            BeginChange();
            group.IsSlideshow = value;
            EndChange();
        }

        /// <summary>More photos or videos onto one standing alone: together they become a collage, its caption the collage's.</summary>
        public void GroupWith(RichMediaBlock media, IList<RichMediaSource> sources)
        {
            if (sources == null || sources.Count == 0 || media.IsGroupItem)
            {
                return;
            }

            BeginChange();

            var container = _document.GetContainer(media, out int index);
            var group = new RichMediaGroupBlock(false);
            container[index] = group;
            group.Parent = media.Parent;
            AddToGroup(group, media);

            foreach (var source in sources)
            {
                group.Items.Add(new RichMediaBlock(source));
            }

            _node = group;

            EndChange();
            Raise(RichEditorChanges.Selection, null);
        }

        /// <summary>More photos or videos into a group, after its last item.</summary>
        public void AddToGroup(RichMediaGroupBlock group, IList<RichMediaSource> sources)
        {
            if (sources == null || sources.Count == 0)
            {
                return;
            }

            BeginChange();

            foreach (var source in sources)
            {
                group.Items.Add(new RichMediaBlock(source));
            }

            EndChange();
        }

        /// <summary>One item out of its collage, to stand on its own right after it.</summary>
        public void ExtractFromGroup(RichMediaBlock item)
        {
            if (item.Parent is not RichMediaGroupBlock group)
            {
                return;
            }

            BeginChange();

            group.Items.Remove(item);
            InsertAfter(group, item);

            _node = null;
            EndChange();

            SelectNode(item);
        }

        /// <summary>One item out of a group; a group left with one item becomes that item, in the reindex.</summary>
        public void RemoveFromGroup(RichMediaGroupBlock group, int index)
        {
            if (index < 0 || index >= group.Items.Count)
            {
                return;
            }

            BeginChange();
            group.Items.RemoveAt(index);

            // A group left with one item or none goes in the reindex, and with it the selection.
            if (group.Items.Count <= 1)
            {
                _node = null;
            }

            EndChange();

            if (group.Items.Count == 1)
            {
                SelectNode(group.Items[0]);
            }
        }

        /// <summary>The buttons of a row, replaced wholesale: what the button editor hands back.</summary>
        public void SetButtons(RichButtonRowBlock row, IList<InlineButton> buttons, PageBlockHorizontalAlignment align)
        {
            BeginChange();

            row.Buttons.Clear();
            row.Buttons.AddRange(buttons);
            row.Align = align;

            // A row with no buttons left is no row.
            if (row.Buttons.Count == 0)
            {
                DeleteNode(row);
            }

            EndChange();
        }
    }
}
