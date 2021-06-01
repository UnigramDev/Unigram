using System;
using System.Collections.Generic;
using System.Linq;
using Unigram.Collections;

namespace Unigram.Common
{
    /*
     * Copyright 2018 The Android Open Source Project
     *
     * Licensed under the Apache License, Version 2.0 (the "License");
     * you may not use this file except in compliance with the License.
     * You may obtain a copy of the License at
     *
     *      http://www.apache.org/licenses/LICENSE-2.0
     *
     * Unless required by applicable law or agreed to in writing, software
     * distributed under the License is distributed on an "AS IS" BASIS,
     * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
     * See the License for the specific language governing permissions and
     * limitations under the License.
     */

    /**
     * DiffUtil is a utility class that calculates the difference between two lists and outputs a
     * list of update operations that converts the first list into the second one.
     * <p>
     * It can be used to calculate updates for a RecyclerView Adapter. See {@link ListAdapter} and
     * {@link AsyncListDiffer} which can simplify the use of DiffUtil on a background thread.
     * <p>
     * DiffUtil uses Eugene W. Myers's difference algorithm to calculate the minimal number of updates
     * to convert one list into another. Myers's algorithm does not handle items that are moved so
     * DiffUtil runs a second pass on the result to detect items that were moved.
     * <p>
     * Note that DiffUtil, ListAdapter, and AsyncListDiffer require the list to not mutate while in use.
     * This generally means that both the lists themselves and their elements (or at least, the
     * properties of elements used in diffing) should not be modified directly. Instead, new lists
     * should be provided any time content changes. It's common for lists passed to DiffUtil to share
     * elements that have not mutated, so it is not strictly required to reload all data to use
     * DiffUtil.
     * <p>
     * If the lists are large, this operation may take significant time so you are advised to run this
     * on a background thread, get the {@link DiffResult} then apply it on the RecyclerView on the main
     * thread.
     * <p>
     * This algorithm is optimized for space and uses O(N) space to find the minimal
     * number of addition and removal operations between the two lists. It has O(N + D^2) expected time
     * performance where D is the length of the edit script.
     * <p>
     * If move detection is enabled, it takes an additional O(N^2) time where N is the total number of
     * added and removed items. If your lists are already sorted by the same constraint (e.g. a created
     * timestamp for a list of posts), you can disable move detection to improve performance.
     * <p>
     * The actual runtime of the algorithm significantly depends on the number of changes in the list
     * and the cost of your comparison methods. Below are some average run times for reference:
     * (The test list is composed of random UUID Strings and the tests are run on Nexus 5X with M)
     * <ul>
     *     <li>100 items and 10 modifications: avg: 0.39 ms, median: 0.35 ms
     *     <li>100 items and 100 modifications: 3.82 ms, median: 3.75 ms
     *     <li>100 items and 100 modifications without moves: 2.09 ms, median: 2.06 ms
     *     <li>1000 items and 50 modifications: avg: 4.67 ms, median: 4.59 ms
     *     <li>1000 items and 50 modifications without moves: avg: 3.59 ms, median: 3.50 ms
     *     <li>1000 items and 200 modifications: 27.07 ms, median: 26.92 ms
     *     <li>1000 items and 200 modifications without moves: 13.54 ms, median: 13.36 ms
     * </ul>
     * <p>
     * Due to implementation constraints, the max size of the list can be 2^26.
     *
     * @see ListAdapter
     * @see AsyncListDiffer
     */
    public static class DiffUtil
    {
        private static readonly Comparison<Snake> SNAKE_COMPARATOR = (Snake o1, Snake o2) =>
        {
            int cmpX = o1.x - o2.x;
            return cmpX == 0 ? o1.y - o2.y : cmpX;
        };

        // Myers' algorithm uses two lists as axis labels. In DiffUtil's implementation, `x` axis is
        // used for old list and `y` axis is used for new list.

        /**
         * Calculates the list of update operations that can covert one list into the other one.
         *
         * @param cb The callback that acts as a gateway to the backing list data
         *
         * @return A DiffResult that contains the information about the edit sequence to convert the
         * old list into the new list.
         */
        public static DiffResult CalculateDiff(Callback cb)
        {
            return CalculateDiff(cb, true);
        }

        /**
         * Calculates the list of update operations that can covert one list into the other one.
         * <p>
         * If your old and new lists are sorted by the same constraint and items never move (swap
         * positions), you can disable move detection which takes <code>O(N^2)</code> time where
         * N is the number of added, moved, removed items.
         *
         * @param cb The callback that acts as a gateway to the backing list data
         * @param detectMoves True if DiffUtil should try to detect moved items, false otherwise.
         *
         * @return A DiffResult that contains the information about the edit sequence to convert the
         * old list into the new list.
         */
        public static DiffResult CalculateDiff(Callback cb, bool detectMoves)
        {
            int oldSize = cb.GetOldListSize();
            int newSize = cb.GetNewListSize();

            List<Snake> snakes = new List<Snake>();

            // instead of a recursive implementation, we keep our own stack to avoid potential stack
            // overflow exceptions
            List<Range> stack = new List<Range>();

            stack.Add(new Range(0, oldSize, 0, newSize));

            int max = oldSize + newSize + Math.Abs(oldSize - newSize);
            // allocate forward and backward k-lines. K lines are diagonal lines in the matrix. (see the
            // paper for details)
            // These arrays lines keep the max reachable position for each k-line.
            int[] forward = new int[max * 2];
            int[] backward = new int[max * 2];

            // We pool the ranges to avoid allocations for each recursive call.
            List<Range> rangePool = new List<Range>();
            while (stack.Count > 0)
            {
                Range range = stack.RemoveLast();
                Snake snake = DiffPartial(cb, range.oldListStart, range.oldListEnd,
                        range.newListStart, range.newListEnd, forward, backward, max);
                if (snake != null)
                {
                    if (snake.size > 0)
                    {
                        snakes.Add(snake);
                    }
                    // offset the snake to convert its coordinates from the Range's area to global
                    snake.x += range.oldListStart;
                    snake.y += range.newListStart;

                    // add new ranges for left and right
                    Range left = rangePool.Count > 0 ? rangePool.RemoveLast() : new Range();
                    left.oldListStart = range.oldListStart;
                    left.newListStart = range.newListStart;
                    if (snake.reverse)
                    {
                        left.oldListEnd = snake.x;
                        left.newListEnd = snake.y;
                    }
                    else
                    {
                        if (snake.removal)
                        {
                            left.oldListEnd = snake.x - 1;
                            left.newListEnd = snake.y;
                        }
                        else
                        {
                            left.oldListEnd = snake.x;
                            left.newListEnd = snake.y - 1;
                        }
                    }
                    stack.Add(left);

                    // re-use range for right
                    //noinspection UnnecessaryLocalVariable
                    Range right = range;
                    if (snake.reverse)
                    {
                        if (snake.removal)
                        {
                            right.oldListStart = snake.x + snake.size + 1;
                            right.newListStart = snake.y + snake.size;
                        }
                        else
                        {
                            right.oldListStart = snake.x + snake.size;
                            right.newListStart = snake.y + snake.size + 1;
                        }
                    }
                    else
                    {
                        right.oldListStart = snake.x + snake.size;
                        right.newListStart = snake.y + snake.size;
                    }
                    stack.Add(right);
                }
                else
                {
                    rangePool.Add(range);
                }

            }
            // sort snakes
            snakes.Sort(SNAKE_COMPARATOR);

            return new DiffResult(cb, snakes, forward, backward, detectMoves);
        }

        private static Snake DiffPartial(Callback cb, int startOld, int endOld,
                int startNew, int endNew, int[] forward, int[] backward, int kOffset)
        {
            int oldSize = endOld - startOld;
            int newSize = endNew - startNew;

            if (endOld - startOld < 1 || endNew - startNew < 1)
            {
                return null;
            }

            int delta = oldSize - newSize;
            int dLimit = (oldSize + newSize + 1) / 2;
            Array.Fill(forward, 0, kOffset - dLimit - 1, kOffset + dLimit + 1 - (kOffset - dLimit - 1));
            Array.Fill(backward, oldSize, kOffset - dLimit - 1 + delta, kOffset + dLimit + 1 + delta - (kOffset - dLimit - 1 + delta));
            bool checkInFwd = delta % 2 != 0;
            for (int d = 0; d <= dLimit; d++)
            {
                for (int k = -d; k <= d; k += 2)
                {
                    // find forward path
                    // we can reach k from k - 1 or k + 1. Check which one is further in the graph
                    int x;
                    bool removal;
                    if (k == -d || (k != d && forward[kOffset + k - 1] < forward[kOffset + k + 1]))
                    {
                        x = forward[kOffset + k + 1];
                        removal = false;
                    }
                    else
                    {
                        x = forward[kOffset + k - 1] + 1;
                        removal = true;
                    }
                    // set y based on x
                    int y = x - k;
                    // move diagonal as long as items match
                    while (x < oldSize && y < newSize
                            && cb.AreItemsTheSame(startOld + x, startNew + y))
                    {
                        x++;
                        y++;
                    }
                    forward[kOffset + k] = x;
                    if (checkInFwd && k >= delta - d + 1 && k <= delta + d - 1)
                    {
                        if (forward[kOffset + k] >= backward[kOffset + k])
                        {
                            Snake outSnake = new Snake();
                            outSnake.x = backward[kOffset + k];
                            outSnake.y = outSnake.x - k;
                            outSnake.size = forward[kOffset + k] - backward[kOffset + k];
                            outSnake.removal = removal;
                            outSnake.reverse = false;
                            return outSnake;
                        }
                    }
                }
                for (int k = -d; k <= d; k += 2)
                {
                    // find reverse path at k + delta, in reverse
                    int backwardK = k + delta;
                    int x;
                    bool removal;
                    if (backwardK == d + delta || (backwardK != -d + delta
                            && backward[kOffset + backwardK - 1] < backward[kOffset + backwardK + 1]))
                    {
                        x = backward[kOffset + backwardK - 1];
                        removal = false;
                    }
                    else
                    {
                        x = backward[kOffset + backwardK + 1] - 1;
                        removal = true;
                    }

                    // set y based on x
                    int y = x - backwardK;
                    // move diagonal as long as items match
                    while (x > 0 && y > 0
                            && cb.AreItemsTheSame(startOld + x - 1, startNew + y - 1))
                    {
                        x--;
                        y--;
                    }
                    backward[kOffset + backwardK] = x;
                    if (!checkInFwd && k + delta >= -d && k + delta <= d)
                    {
                        if (forward[kOffset + backwardK] >= backward[kOffset + backwardK])
                        {
                            Snake outSnake = new Snake();
                            outSnake.x = backward[kOffset + backwardK];
                            outSnake.y = outSnake.x - backwardK;
                            outSnake.size =
                                    forward[kOffset + backwardK] - backward[kOffset + backwardK];
                            outSnake.removal = removal;
                            outSnake.reverse = true;
                            return outSnake;
                        }
                    }
                }
            }
            throw new Exception("DiffUtil hit an unexpected case while trying to calculate"
                    + " the optimal path. Please make sure your data is not changing during the"
                    + " diff calculation.");
        }

        /**
         * A Callback class used by DiffUtil while calculating the diff between two lists.
         */
        public abstract class Callback
        {
            /**
             * Returns the size of the old list.
             *
             * @return The size of the old list.
             */
            public abstract int GetOldListSize();

            /**
             * Returns the size of the new list.
             *
             * @return The size of the new list.
             */
            public abstract int GetNewListSize();

            /**
             * Called by the DiffUtil to decide whether two object represent the same Item.
             * <p>
             * For example, if your items have unique ids, this method should check their id equality.
             *
             * @param oldItemPosition The position of the item in the old list
             * @param newItemPosition The position of the item in the new list
             * @return True if the two items represent the same object or false if they are different.
             */
            public abstract bool AreItemsTheSame(int oldItemPosition, int newItemPosition);

            /**
             * Called by the DiffUtil when it wants to check whether two items have the same data.
             * DiffUtil uses this information to detect if the contents of an item has changed.
             * <p>
             * DiffUtil uses this method to check equality instead of {@link Object#equals(Object)}
             * so that you can change its behavior depending on your UI.
             * For example, if you are using DiffUtil with a
             * {@link RecyclerView.Adapter RecyclerView.Adapter}, you should
             * return whether the items' visual representations are the same.
             * <p>
             * This method is called only if {@link #areItemsTheSame(int, int)} returns
             * {@code true} for these items.
             *
             * @param oldItemPosition The position of the item in the old list
             * @param newItemPosition The position of the item in the new list which replaces the
             *                        oldItem
             * @return True if the contents of the items are the same or false if they are different.
             */
            public abstract bool AreContentsTheSame(int oldItemPosition, int newItemPosition);

            /**
             * When {@link #areItemsTheSame(int, int)} returns {@code true} for two items and
             * {@link #areContentsTheSame(int, int)} returns false for them, DiffUtil
             * calls this method to get a payload about the change.
             * <p>
             * For example, if you are using DiffUtil with {@link RecyclerView}, you can return the
             * particular field that changed in the item and your
             * {@link RecyclerView.ItemAnimator ItemAnimator} can use that
             * information to run the correct animation.
             * <p>
             * Default implementation returns {@code null}.
             *
             * @param oldItemPosition The position of the item in the old list
             * @param newItemPosition The position of the item in the new list
             *
             * @return A payload object that represents the change between the two items.
             */
            public object GetChangePayload(int oldItemPosition, int newItemPosition)
            {
                return null;
            }
        }

        /**
         * Callback for calculating the diff between two non-null items in a list.
         * <p>
         * {@link Callback} serves two roles - list indexing, and item diffing. ItemCallback handles
         * just the second of these, which allows separation of code that indexes into an array or List
         * from the presentation-layer and content specific diffing code.
         *
         * @param <T> Type of items to compare.
         */
        public abstract class ItemCallback<T>
        {
            /**
             * Called to check whether two objects represent the same item.
             * <p>
             * For example, if your items have unique ids, this method should check their id equality.
             * <p>
             * Note: {@code null} items in the list are assumed to be the same as another {@code null}
             * item and are assumed to not be the same as a non-{@code null} item. This callback will
             * not be invoked for either of those cases.
             *
             * @param oldItem The item in the old list.
             * @param newItem The item in the new list.
             * @return True if the two items represent the same object or false if they are different.
             *
             * @see Callback#areItemsTheSame(int, int)
             */
            public abstract bool AreItemsTheSame(T oldItem, T newItem);

            /**
             * Called to check whether two items have the same data.
             * <p>
             * This information is used to detect if the contents of an item have changed.
             * <p>
             * This method to check equality instead of {@link Object#equals(Object)} so that you can
             * change its behavior depending on your UI.
             * <p>
             * For example, if you are using DiffUtil with a
             * {@link RecyclerView.Adapter RecyclerView.Adapter}, you should
             * return whether the items' visual representations are the same.
             * <p>
             * This method is called only if {@link #areItemsTheSame(T, T)} returns {@code true} for
             * these items.
             * <p>
             * Note: Two {@code null} items are assumed to represent the same contents. This callback
             * will not be invoked for this case.
             *
             * @param oldItem The item in the old list.
             * @param newItem The item in the new list.
             * @return True if the contents of the items are the same or false if they are different.
             *
             * @see Callback#areContentsTheSame(int, int)
             */
            public abstract bool AreContentsTheSame(T oldItem, T newItem);

            /**
             * When {@link #areItemsTheSame(T, T)} returns {@code true} for two items and
             * {@link #areContentsTheSame(T, T)} returns false for them, this method is called to
             * get a payload about the change.
             * <p>
             * For example, if you are using DiffUtil with {@link RecyclerView}, you can return the
             * particular field that changed in the item and your
             * {@link RecyclerView.ItemAnimator ItemAnimator} can use that
             * information to run the correct animation.
             * <p>
             * Default implementation returns {@code null}.
             *
             * @see Callback#getChangePayload(int, int)
             */
            public object GetChangePayload(T oldItem, T newItem)
            {
                return null;
            }
        }

        /**
         * Snakes represent a match between two lists. It is optionally prefixed or postfixed with an
         * add or remove operation. See the Myers' paper for details.
         */
        public class Snake
        {
            /**
             * Position in the old list
             */
            public int x;

            /**
             * Position in the new list
             */
            public int y;

            /**
             * Number of matches. Might be 0.
             */
            public int size;

            /**
             * If true, this is a removal from the original list followed by {@code size} matches.
             * If false, this is an addition from the new list followed by {@code size} matches.
             */
            public bool removal;

            /**
             * If true, the addition or removal is at the end of the snake.
             * If false, the addition or removal is at the beginning of the snake.
             */
            public bool reverse;
        }

        /**
         * Represents a range in two lists that needs to be solved.
         * <p>
         * This internal class is used when running Myers' algorithm without recursion.
         */
        private class Range
        {

            public int oldListStart, oldListEnd;

            public int newListStart, newListEnd;

            public Range()
            {
            }

            public Range(int oldListStart, int oldListEnd, int newListStart, int newListEnd)
            {
                this.oldListStart = oldListStart;
                this.oldListEnd = oldListEnd;
                this.newListStart = newListStart;
                this.newListEnd = newListEnd;
            }
        }

        /**
         * This class holds the information about the result of a
         * {@link DiffUtil#calculateDiff(Callback, bool)} call.
         * <p>
         * You can consume the updates in a DiffResult via
         * {@link #dispatchUpdatesTo(ListUpdateCallback)} or directly stream the results into a
         * {@link RecyclerView.Adapter} via {@link #dispatchUpdatesTo(RecyclerView.Adapter)}.
         */
        public class DiffResult
        {
            /**
             * Signifies an item not present in the list.
             */
            public const int NO_POSITION = -1;


            /**
             * While reading the flags below, keep in mind that when multiple items move in a list,
             * Myers's may pick any of them as the anchor item and consider that one NOT_CHANGED while
             * picking others as additions and removals. This is completely fine as we later detect
             * all moves.
             * <p>
             * Below, when an item is mentioned to stay in the same "location", it means we won't
             * dispatch a move/add/remove for it, it DOES NOT mean the item is still in the same
             * position.
             */
            // item stayed the same.
            private const int FLAG_NOT_CHANGED = 1;
            // item stayed in the same location but changed.
            private const int FLAG_CHANGED = FLAG_NOT_CHANGED << 1;
            // Item has moved and also changed.
            private const int FLAG_MOVED_CHANGED = FLAG_CHANGED << 1;
            // Item has moved but did not change.
            private const int FLAG_MOVED_NOT_CHANGED = FLAG_MOVED_CHANGED << 1;
            // Ignore this update.
            // If this is an addition from the new list, it means the item is actually removed from an
            // earlier position and its move will be dispatched when we process the matching removal
            // from the old list.
            // If this is a removal from the old list, it means the item is actually added back to an
            // earlier index in the new list and we'll dispatch its move when we are processing that
            // addition.
            private const int FLAG_IGNORE = FLAG_MOVED_NOT_CHANGED << 1;

            // since we are re-using the int arrays that were created in the Myers' step, we mask
            // change flags
            private const int FLAG_OFFSET = 5;

            private const int FLAG_MASK = (1 << FLAG_OFFSET) - 1;

            // The Myers' snakes. At this point, we only care about their diagonal sections.
            private readonly List<Snake> mSnakes;

            // The list to keep oldItemStatuses. As we traverse old items, we assign flags to them
            // which also includes whether they were a real removal or a move (and its new index).
            private readonly int[] mOldItemStatuses;
            // The list to keep newItemStatuses. As we traverse new items, we assign flags to them
            // which also includes whether they were a real addition or a move(and its old index).
            private readonly int[] mNewItemStatuses;
            // The callback that was given to calcualte diff method.
            private readonly Callback mCallback;

            private readonly int mOldListSize;

            private readonly int mNewListSize;

            private readonly bool mDetectMoves;

            /**
             * @param callback The callback that was used to calculate the diff
             * @param snakes The list of Myers' snakes
             * @param oldItemStatuses An int[] that can be re-purposed to keep metadata
             * @param newItemStatuses An int[] that can be re-purposed to keep metadata
             * @param detectMoves True if this DiffResult will try to detect moved items
             */
            public DiffResult(Callback callback, List<Snake> snakes, int[] oldItemStatuses,
                    int[] newItemStatuses, bool detectMoves)
            {
                mSnakes = snakes;
                mOldItemStatuses = oldItemStatuses;
                mNewItemStatuses = newItemStatuses;
                Array.Fill(mOldItemStatuses, 0);
                Array.Fill(mNewItemStatuses, 0);
                mCallback = callback;
                mOldListSize = callback.GetOldListSize();
                mNewListSize = callback.GetNewListSize();
                mDetectMoves = detectMoves;
                AddRootSnake();
                FindMatchingItems();
            }

            /**
             * We always add a Snake to 0/0 so that we can run loops from end to beginning and be done
             * when we run out of snakes.
             */
            private void AddRootSnake()
            {
                Snake firstSnake = mSnakes.Count > 0 ? mSnakes[0] : null;
                if (firstSnake == null || firstSnake.x != 0 || firstSnake.y != 0)
                {
                    Snake root = new Snake();
                    root.x = 0;
                    root.y = 0;
                    root.removal = false;
                    root.size = 0;
                    root.reverse = false;
                    mSnakes.Insert(0, root);
                }
            }

            /**
             * This method traverses each addition / removal and tries to match it to a previous
             * removal / addition. This is how we detect move operations.
             * <p>
             * This class also flags whether an item has been changed or not.
             * <p>
             * DiffUtil does this pre-processing so that if it is running on a big list, it can be moved
             * to background thread where most of the expensive stuff will be calculated and kept in
             * the statuses maps. DiffResult uses this pre-calculated information while dispatching
             * the updates (which is probably being called on the main thread).
             */
            private void FindMatchingItems()
            {
                int posOld = mOldListSize;
                int posNew = mNewListSize;
                // traverse the matrix from right bottom to 0,0.
                for (int i = mSnakes.Count - 1; i >= 0; i--)
                {
                    Snake snake = mSnakes[i];
                    int endX = snake.x + snake.size;
                    int endY = snake.y + snake.size;
                    if (mDetectMoves)
                    {
                        while (posOld > endX)
                        {
                            // this is a removal. Check remaining snakes to see if this was added before
                            FindAddition(posOld, posNew, i);
                            posOld--;
                        }
                        while (posNew > endY)
                        {
                            // this is an addition. Check remaining snakes to see if this was removed
                            // before
                            FindRemoval(posOld, posNew, i);
                            posNew--;
                        }
                    }
                    for (int j = 0; j < snake.size; j++)
                    {
                        // matching items. Check if it is changed or not
                        int oldItemPos = snake.x + j;
                        int newItemPos = snake.y + j;
                        bool theSame = mCallback
                                .AreContentsTheSame(oldItemPos, newItemPos);
                        int changeFlag = theSame ? FLAG_NOT_CHANGED : FLAG_CHANGED;
                        mOldItemStatuses[oldItemPos] = (newItemPos << FLAG_OFFSET) | changeFlag;
                        mNewItemStatuses[newItemPos] = (oldItemPos << FLAG_OFFSET) | changeFlag;
                    }
                    posOld = snake.x;
                    posNew = snake.y;
                }
            }

            private void FindAddition(int x, int y, int snakeIndex)
            {
                if (mOldItemStatuses[x - 1] != 0)
                {
                    return; // already set by a latter item
                }
                FindMatchingItem(x, y, snakeIndex, false);
            }

            private void FindRemoval(int x, int y, int snakeIndex)
            {
                if (mNewItemStatuses[y - 1] != 0)
                {
                    return; // already set by a latter item
                }
                FindMatchingItem(x, y, snakeIndex, true);
            }

            /**
             * Given a position in the old list, returns the position in the new list, or
             * {@code NO_POSITION} if it was removed.
             *
             * @param oldListPosition Position of item in old list
             *
             * @return Position of item in new list, or {@code NO_POSITION} if not present.
             *
             * @see #NO_POSITION
             * @see #convertNewPositionToOld(int)
             */
            public int ConvertOldPositionToNew(int oldListPosition)
            {
                if (oldListPosition < 0 || oldListPosition >= mOldListSize)
                {
                    throw new IndexOutOfRangeException("Index out of bounds - passed position = "
                            + oldListPosition + ", old list size = " + mOldListSize);
                }
                int status = mOldItemStatuses[oldListPosition];
                if ((status & FLAG_MASK) == 0)
                {
                    return NO_POSITION;
                }
                else
                {
                    return status >> FLAG_OFFSET;
                }
            }

            /**
             * Given a position in the new list, returns the position in the old list, or
             * {@code NO_POSITION} if it was removed.
             *
             * @param newListPosition Position of item in new list
             *
             * @return Position of item in old list, or {@code NO_POSITION} if not present.
             *
             * @see #NO_POSITION
             * @see #convertOldPositionToNew(int)
             */
            public int ConvertNewPositionToOld(int newListPosition)
            {
                if (newListPosition < 0 || newListPosition >= mNewListSize)
                {
                    throw new IndexOutOfRangeException("Index out of bounds - passed position = "
                            + newListPosition + ", new list size = " + mNewListSize);
                }
                int status = mNewItemStatuses[newListPosition];
                if ((status & FLAG_MASK) == 0)
                {
                    return NO_POSITION;
                }
                else
                {
                    return status >> FLAG_OFFSET;
                }
            }

            /**
             * Finds a matching item that is before the given coordinates in the matrix
             * (before : left and above).
             *
             * @param x The x position in the matrix (position in the old list)
             * @param y The y position in the matrix (position in the new list)
             * @param snakeIndex The current snake index
             * @param removal True if we are looking for a removal, false otherwise
             *
             * @return True if such item is found.
             */
            private bool FindMatchingItem(int x, int y, int snakeIndex, bool removal)
            {
                int myItemPos;
                int curX;
                int curY;
                if (removal)
                {
                    myItemPos = y - 1;
                    curX = x;
                    curY = y - 1;
                }
                else
                {
                    myItemPos = x - 1;
                    curX = x - 1;
                    curY = y;
                }
                for (int i = snakeIndex; i >= 0; i--)
                {
                    Snake snake = mSnakes[i];
                    int endX = snake.x + snake.size;
                    int endY = snake.y + snake.size;
                    if (removal)
                    {
                        // check removals for a match
                        for (int pos = curX - 1; pos >= endX; pos--)
                        {
                            if (mCallback.AreItemsTheSame(pos, myItemPos))
                            {
                                // found!
                                bool theSame = mCallback.AreContentsTheSame(pos, myItemPos);
                                int changeFlag = theSame ? FLAG_MOVED_NOT_CHANGED
                                        : FLAG_MOVED_CHANGED;
                                mNewItemStatuses[myItemPos] = (pos << FLAG_OFFSET) | FLAG_IGNORE;
                                mOldItemStatuses[pos] = (myItemPos << FLAG_OFFSET) | changeFlag;
                                return true;
                            }
                        }
                    }
                    else
                    {
                        // check for additions for a match
                        for (int pos = curY - 1; pos >= endY; pos--)
                        {
                            if (mCallback.AreItemsTheSame(myItemPos, pos))
                            {
                                // found
                                bool theSame = mCallback.AreContentsTheSame(myItemPos, pos);
                                int changeFlag = theSame ? FLAG_MOVED_NOT_CHANGED
                                        : FLAG_MOVED_CHANGED;
                                mOldItemStatuses[x - 1] = (pos << FLAG_OFFSET) | FLAG_IGNORE;
                                mNewItemStatuses[pos] = ((x - 1) << FLAG_OFFSET) | changeFlag;
                                return true;
                            }
                        }
                    }
                    curX = snake.x;
                    curY = snake.y;
                }
                return false;
            }

            /**
             * Dispatches the update events to the given adapter.
             * <p>
             * For example, if you have an {@link RecyclerView.Adapter Adapter}
             * that is backed by a {@link List}, you can swap the list with the new one then call this
             * method to dispatch all updates to the RecyclerView.
             * <pre>
             *     List oldList = mAdapter.getData();
             *     DiffResult result = DiffUtil.calculateDiff(new MyCallback(oldList, newList));
             *     mAdapter.setData(newList);
             *     result.dispatchUpdatesTo(mAdapter);
             * </pre>
             * <p>
             * Note that the RecyclerView requires you to dispatch adapter updates immediately when you
             * change the data (you cannot defer {@code notify*} calls). The usage above adheres to this
             * rule because updates are sent to the adapter right after the backing data is changed,
             * before RecyclerView tries to read it.
             * <p>
             * On the other hand, if you have another
             * {@link RecyclerView.AdapterDataObserver AdapterDataObserver}
             * that tries to process events synchronously, this may confuse that observer because the
             * list is instantly moved to its final state while the adapter updates are dispatched later
             * on, one by one. If you have such an
             * {@link RecyclerView.AdapterDataObserver AdapterDataObserver},
             * you can use
             * {@link #dispatchUpdatesTo(ListUpdateCallback)} to handle each modification
             * manually.
             *
             * @param adapter A RecyclerView adapter which was displaying the old list and will start
             *                displaying the new list.
             * @see AdapterListUpdateCallback
             */
            //public void dispatchUpdatesTo(@NonNull final RecyclerView.Adapter adapter)
            //{
            //    dispatchUpdatesTo(new AdapterListUpdateCallback(adapter));
            //}

            /**
             * Dispatches update operations to the given Callback.
             * <p>
             * These updates are atomic such that the first update call affects every update call that
             * comes after it (the same as RecyclerView).
             *
             * @param updateCallback The callback to receive the update operations.
             * @see #dispatchUpdatesTo(RecyclerView.Adapter)
             */
            public void DispatchUpdatesTo<T>(MvxObservableCollection<T> updateCallback, IList<T> source)
            {
                // These are add/remove ops that are converted to moves. We track their positions until
                // their respective update operations are processed.
                List<PostponedUpdate> postponedUpdates = new List<PostponedUpdate>();
                int posOld = mOldListSize;
                int posNew = mNewListSize;
                for (int snakeIndex = mSnakes.Count - 1; snakeIndex >= 0; snakeIndex--)
                {
                    Snake snake = mSnakes[snakeIndex];
                    int snakeSize = snake.size;
                    int endX = snake.x + snakeSize;
                    int endY = snake.y + snakeSize;
                    if (endX < posOld)
                    {
                        DispatchRemovals(postponedUpdates, updateCallback, source, endX, posOld - endX, endX);
                    }

                    if (endY < posNew)
                    {
                        DispatchAdditions(postponedUpdates, updateCallback, source, endX, posNew - endY,
                                endY);
                    }
                    for (int i = snakeSize - 1; i >= 0; i--)
                    {
                        if ((mOldItemStatuses[snake.x + i] & FLAG_MASK) == FLAG_CHANGED)
                        {
                            //updateCallback.onChanged(snake.x + i, 1,
                            //        mCallback.getChangePayload(snake.x + i, snake.y + i));
                            updateCallback.Change(snake.x + i);
                        }
                    }
                    posOld = snake.x;
                    posNew = snake.y;
                }
                //batchingCallback.dispatchLastEvent();
            }

            private static PostponedUpdate RemovePostponedUpdate(List<PostponedUpdate> updates,
                    int pos, bool removal)
            {
                for (int i = updates.Count - 1; i >= 0; i--)
                {
                    PostponedUpdate update = updates[i];
                    if (update.posInOwnerList == pos && update.removal == removal)
                    {
                        updates.RemoveAt(i);
                        for (int j = i; j < updates.Count; j++)
                        {
                            // offset other ops since they swapped positions
                            updates[j].currentPos += removal ? 1 : -1;
                        }
                        return update;
                    }
                }
                return null;
            }

            private void DispatchAdditions<T>(List<PostponedUpdate> postponedUpdates,
                    MvxObservableCollection<T> updateCallback, IList<T> source, int start, int count, int globalIndex)
            {
                if (!mDetectMoves)
                {
                    //updateCallback.onInserted(start, count);
                    updateCallback.InsertRange(start, source.Skip(start).Take(count));
                    return;
                }
                for (int i = count - 1; i >= 0; i--)
                {
                    int status = mNewItemStatuses[globalIndex + i] & FLAG_MASK;
                    switch (status)
                    {
                        case 0: // real addition
                            //updateCallback.onInserted(start, 1);
                            updateCallback.Insert(start, source[start]);
                            foreach (PostponedUpdate upd in postponedUpdates)
                            {
                                upd.currentPos += 1;
                            }
                            break;
                        case FLAG_MOVED_CHANGED:
                        case FLAG_MOVED_NOT_CHANGED:
                            int pos = mNewItemStatuses[globalIndex + i] >> FLAG_OFFSET;
                            PostponedUpdate update = RemovePostponedUpdate(postponedUpdates, pos,
                                                            true);
                            // the item was moved from that position
                            //noinspection ConstantConditions
                            //updateCallback.onMoved(update.currentPos, start);
                            updateCallback.RemoveAt(update.currentPos);
                            updateCallback.Insert(start, source[start]);
                            //if (status == FLAG_MOVED_CHANGED)
                            //{
                            //    // also dispatch a change
                            //    updateCallback.onChanged(start, 1,
                            //            mCallback.getChangePayload(pos, globalIndex + i));
                            //}
                            break;
                        case FLAG_IGNORE: // ignoring this
                            postponedUpdates.Add(new PostponedUpdate(globalIndex + i, start, false));
                            break;
                        default:
                            throw new Exception(
                                    "unknown flag for pos " + (globalIndex + i) + " " + status);
                    }
                }
            }

            private void DispatchRemovals<T>(List<PostponedUpdate> postponedUpdates,
                    MvxObservableCollection<T> updateCallback, IList<T> source, int start, int count, int globalIndex)
            {
                if (!mDetectMoves)
                {
                    //updateCallback.onRemoved(start, count);
                    updateCallback.RemoveRange(start, count);
                    return;
                }
                for (int i = count - 1; i >= 0; i--)
                {
                    int status = mOldItemStatuses[globalIndex + i] & FLAG_MASK;
                    switch (status)
                    {
                        case 0: // real removal
                            //updateCallback.onRemoved(start + i, 1);
                            updateCallback.RemoveAt(start + i);
                            foreach (PostponedUpdate upd in postponedUpdates)
                            {
                                upd.currentPos -= 1;
                            }
                            break;
                        case FLAG_MOVED_CHANGED:
                        case FLAG_MOVED_NOT_CHANGED:
                            int pos = mOldItemStatuses[globalIndex + i] >> FLAG_OFFSET;
                            PostponedUpdate update = RemovePostponedUpdate(postponedUpdates, pos,
                                                            false);
                            // the item was moved to that position. we do -1 because this is a move not
                            // add and removing current item offsets the target move by 1
                            //noinspection ConstantConditions
                            //updateCallback.onMoved(start + i, update.currentPos - 1);
                            updateCallback.RemoveAt(start + i);
                            updateCallback.Insert(update.currentPos - 1, source[update.currentPos - 1]);
                            //if (status == FLAG_MOVED_CHANGED)
                            //{
                            //    // also dispatch a change
                            //    updateCallback.onChanged(update.currentPos - 1, 1,
                            //            mCallback.getChangePayload(globalIndex + i, pos));
                            //}
                            break;
                        case FLAG_IGNORE: // ignoring this
                            postponedUpdates.Add(new PostponedUpdate(globalIndex + i, start + i, true));
                            break;
                        default:
                            throw new Exception(
                                    "unknown flag for pos " + (globalIndex + i) + " " + status);
                    }
                }
            }
        }

        /**
         * Represents an update that we skipped because it was a move.
         * <p>
         * When an update is skipped, it is tracked as other updates are dispatched until the matching
         * add/remove operation is found at which point the tracked position is used to dispatch the
         * update.
         */
        private class PostponedUpdate
        {
            public int posInOwnerList;

            public int currentPos;

            public bool removal;

            public PostponedUpdate(int posInOwnerList, int currentPos, bool removal)
            {
                this.posInOwnerList = posInOwnerList;
                this.currentPos = currentPos;
                this.removal = removal;
            }
        }
    }
}
