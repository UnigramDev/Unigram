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

// Fast approximation for exp.
//

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_MATH_FASTEXP_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_MATH_FASTEXP_H_

#include <cassert>
#include <cmath>
#include <limits>

#include "lang_id/common/lite_base/casts.h"
#include "lang_id/common/lite_base/integral-types.h"
#include "lang_id/common/lite_base/logging.h"

namespace libtextclassifier3 {
namespace mobile {

class FastMathClass {
 private:
  static constexpr int kBits = 7;
  static constexpr int kMask1 = (1 << kBits) - 1;
  static constexpr int kMask2 = 0xFF << kBits;
  static constexpr float kLogBase2OfE = 1.44269504088896340736f;

  struct Table {
    int32 exp1[1 << kBits];
  };

 public:
  float VeryFastExp2(float f) const {
    SAFTM_DCHECK_LE(fabs(f), 126);
    const float g = f + (127 + (1 << (23 - kBits)));
    const int32 x = bit_cast<int32>(g);
    int32 ret = ((x & kMask2) << (23 - kBits))
      | cache_.exp1[x & kMask1];
    return bit_cast<float>(ret);
  }

  float VeryFastExp(float f) const {
    return VeryFastExp2(f * kLogBase2OfE);
  }

 private:
  static const Table cache_;
};

extern FastMathClass FastMathInstance;

inline float VeryFastExp(float f) { return FastMathInstance.VeryFastExp(f); }

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_MATH_FASTEXP_H_
