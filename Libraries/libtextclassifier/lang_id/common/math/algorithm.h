/*
 * Copyright (C) 2018 The Android Open Source Project
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

// Generic utils similar to those from the C++ header <algorithm>.

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_MATH_ALGORITHM_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_MATH_ALGORITHM_H_

#include <algorithm>
#include <queue>
#include <vector>

namespace libtextclassifier3 {
namespace mobile {

// Returns index of max element from the vector |elements|.  Returns 0 if
// |elements| is empty.  T should be a type that can be compared by operator<.
template<typename T>
inline int GetArgMax(const std::vector<T> &elements) {
  return std::distance(
      elements.begin(),
      std::max_element(elements.begin(), elements.end()));
}

// Returns index of min element from the vector |elements|.  Returns 0 if
// |elements| is empty.  T should be a type that can be compared by operator<.
template<typename T>
inline int GetArgMin(const std::vector<T> &elements) {
  return std::distance(
      elements.begin(),
      std::min_element(elements.begin(), elements.end()));
}

// Returns indices of greatest k elements from |v|.
//
// The order between elements is indicated by |smaller|, which should be an
// object like std::less<T>, std::greater<T>, etc.  If smaller(a, b) is true,
// that means that "a is smaller than b".  Intuitively, |smaller| is a
// generalization of operator<.  Formally, it is a strict weak ordering, see
// https://en.cppreference.com/w/cpp/named_req/Compare
//
// Calling this function with std::less<T>() returns the indices of the larger k
// elements; calling it with std::greater<T>() returns the indices of the
// smallest k elements.  This is similar to e.g., std::priority_queue: using the
// default std::less gives you a max-heap, while using std::greater results in a
// min-heap.
//
// Returned indices are sorted in decreasing order of the corresponding elements
// (e.g., first element of the returned array is the index of the largest
// element).  In case of ties (e.g., equal elements) we select the one with the
// smallest index.  E.g., getting the indices of the top-2 elements from [3, 2,
// 1, 3, 0, 3] returns [0, 3] (the indices of the first and the second 3).
//
// Corner cases: If k <= 0, this function returns an empty vector.  If |v| has
// only n < k elements, this function returns all n indices [0, 1, 2, ..., n -
// 1], sorted according to the comp order of the indicated elements.
//
// Assuming each comparison is O(1), this function uses O(k) auxiliary space,
// and runs in O(n * log k) time.  Note: it is possible to use std::nth_element
// and obtain an O(n + k * log k) time algorithm, but that uses O(n) auxiliary
// space.  In our case, k << n, e.g., we may want to select the top-3 most
// likely classes from a set of 100 classes, so the time complexity difference
// should not matter in practice.
template <typename T, typename Smaller>
std::vector<int> GetTopKIndices(int k, const std::vector<T> &v,
                                Smaller smaller) {
  if (k <= 0) {
    return std::vector<int>();
  }

  if (k > v.size()) {
    k = v.size();
  }

  // An order between indices.  Intuitively, rev_vcomp(i1, i2) iff v[i2] is
  // smaller than v[i1].  No typo: this inversion is necessary for Invariant B
  // below.  "vcomp" stands for "value comparator" (we compare the values
  // indicates by the two indices) and "rev_" stands for the reverse order.
  const auto rev_vcomp = [&v, &smaller](int i1, int i2) -> bool {
    if (smaller(v[i2], v[i1])) return true;
    if (smaller(v[i1], v[i2])) return false;

    // Break ties in favor of earlier elements.
    return i1 < i2;
  };

  // Indices of the top-k elements seen so far.
  std::vector<int> heap(k);

  // First, we fill |heap| with the first k indices.
  for (int i = 0; i < k; ++i) {
    heap[i] = i;
  }
  std::make_heap(heap.begin(), heap.end(), rev_vcomp);

  // Next, we explore the rest of the vector v.  Loop invariants:
  //
  // Invariant A: |heap| contains the indices of the top-k elements from v[0:i].
  //
  // Invariant B: heap[0] is the index of the smallest element from all elements
  // indicated by the indices from |heap|.
  //
  // Invariant C: |heap| is a max heap, according to order rev_vcomp.
  for (int i = k; i < v.size(); ++i) {
    // We have to update |heap| iff v[i] is larger than the smallest of the
    // top-k seen so far.  This test is easy to do, due to Invariant B above.
    if (smaller(v[heap[0]], v[i])) {
      // Next lines replace heap[0] with i and re-"heapify" heap[0:k-1].
      heap.push_back(i);
      std::pop_heap(heap.begin(), heap.end(), rev_vcomp);
      heap.pop_back();
    }
  }

  // Arrange indices from |heap| in decreasing order of corresponding elements.
  //
  // More info: in iteration #0, we extract the largest heap element (according
  // to rev_vcomp, i.e., the index of the smallest of the top-k elements) and
  // place it at the end of heap, i.e., in heap[k-1].  In iteration #1, we
  // extract the second largest and place it in heap[k-2], etc.
  for (int i = 0; i < k; ++i) {
    std::pop_heap(heap.begin(), heap.end() - i, rev_vcomp);
  }
  return heap;
}

template <typename T>
std::vector<int> GetTopKIndices(int k, const std::vector<T> &elements) {
  return GetTopKIndices(k, elements, std::less<T>());
}

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_MATH_ALGORITHM_H_
