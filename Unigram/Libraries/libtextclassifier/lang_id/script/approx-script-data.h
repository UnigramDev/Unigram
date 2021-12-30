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

#ifndef NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_SCRIPT_APPROX_SCRIPT_DATA_H_
#define NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_SCRIPT_APPROX_SCRIPT_DATA_H_

#include "lang_id/common/lite_base/integral-types.h"

namespace libtextclassifier3 {
namespace mobile {
namespace approx_script_internal {

// Number of contiguous ranges of same-script codepoints (see below).
extern const int kNumRanges;

// Non-overlapping ranges of unicode characters.  Characters from each range has
// the same script (see kRangeScripts below).  Multiple ranges may have the same
// script.  Note: we represent the kNumRanges ranges as an array with their
// first codepoints, and a separate array with their sizes (see kRangeSize
// below).  This leads to better memory locality during the binary search (which
// uses only the first codepoints, up until the very end).
//
// kRangeFirst[i] = first codepoint from range #i, \forall 0 <= i < kNumRanges.
extern const uint32 kRangeFirst[];

// kRangeSize[i] > 0 is the number of consecutive codepoints in range #i *minus*
// 1, \forall 0 <= i < kNumRanges.  I.e., 0 means that the range contains 1
// codepoints.  Since we don't have empty ranges, this "minus one" convention
// allows us to use all 2^16 values here.
extern const uint16 kRangeSizeMinusOne[];

// Scripts for the ranges from kRanges.  For each i such that 0 <= i <
// kNumRanges, the range #i has the script kRangeScript[i].  Each uint8 element
// can be casted to an UScriptCode enum value (see
// unicode/uscript.h).
//
// NOTE: we don't use directly UScriptCode here, as that requires a full int
// (due to USCRIPT_INVALID_CODE = -1).  uint8 is enough for us (and shorter!)
extern const uint8 kRangeScript[];

// Max value from kRangeScript[].  Scripts are guaranteed to be in the interval
// [0, kMaxScript] (inclusive on both sides).  Can be used to e.g., set the
// number of rows in an embedding table for a script-based feature.
extern const uint8 kMaxScript;

}  // namespace approx_script_internal
}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_SCRIPT_APPROX_SCRIPT_DATA_H_
