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

#include "lang_id/script/approx-script.h"

#include "lang_id/common/lite_base/integral-types.h"
#include "lang_id/common/lite_base/logging.h"
#include "lang_id/common/utf8.h"
#include "lang_id/script/approx-script-data.h"

namespace libtextclassifier3 {
namespace mobile {

// int value of USCRIPT_UNKNOWN from enum UScriptCode (from
// unicode/uscript.h).  Note: we do have a test that
// USCRIPT_UNKNOWN evaluates to 103.
const int kUnknownUscript = 103;

namespace {
using approx_script_internal::kNumRanges;
using approx_script_internal::kRangeFirst;
using approx_script_internal::kRangeScript;
using approx_script_internal::kRangeSizeMinusOne;

uint32 Utf8ToCodepoint(const unsigned char *s, int num_bytes) {
  switch (num_bytes) {
    case 1:
      return s[0];
    case 2:
      return ((s[0] & 0x1F) << 6) | (s[1] & 0x3F);
    case 3:
      return (((s[0] & 0x0F) << 12) | ((s[1] & 0x3F) << 6) | (s[2] & 0x3F));
    case 4:
      return (((s[0] & 0x07) << 18) | ((s[1] & 0x3F) << 12) |
              ((s[2] & 0x3F) << 6) | (s[3] & 0x3F));
    default:
      SAFTM_DLOG(FATAL) << "Illegal num_bytes: " << num_bytes;
      return 0;
  }
}

inline int BinarySearch(uint32 codepoint, int start, int end) {
  while (end > start + 1) {
    // Due to the while loop condition, middle > start and middle < end.  Hence,
    // on both branches of the if below, we strictly reduce the end - start
    // value, so we eventually get that difference below 1 and complete the
    // while loop.
    int middle = (start + end) / 2;
    if (codepoint < kRangeFirst[middle]) {
      end = middle;
    } else {
      start = middle;
    }
  }

  if (end == start + 1) {
    const uint32 range_start = kRangeFirst[start];
    if ((codepoint >= range_start) &&
        (codepoint <= range_start + kRangeSizeMinusOne[start])) {
      return kRangeScript[start];
    }
  }

  return kUnknownUscript;
}
}  // namespace

int GetApproxScript(const unsigned char *s, int num_bytes) {
  SAFTM_DCHECK_NE(s, nullptr);
  SAFTM_DCHECK_EQ(num_bytes,
                  utils::OneCharLen(reinterpret_cast<const char *>(s)));
  uint32 codepoint = Utf8ToCodepoint(s, num_bytes);
  return BinarySearch(codepoint, 0, kNumRanges);
}

int GetMaxApproxScriptResult() { return approx_script_internal::kMaxScript; }

SAFTM_STATIC_REGISTRATION(ApproxScriptDetector);

}  // namespace mobile
}  // namespace nlp_saft
