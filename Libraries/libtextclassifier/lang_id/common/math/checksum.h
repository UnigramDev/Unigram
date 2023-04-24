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

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_MATH_CHECKSUM_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_MATH_CHECKSUM_H_

#include "lang_id/common/lite_base/integral-types.h"
#include "lang_id/common/lite_strings/stringpiece.h"

namespace libtextclassifier3 {
namespace mobile {

// Class to compute a 32bit Cyclic Redundancy Check (CRC) in a cummulative way.
//
// To use, create an instance of this class, repeatedly call Update() to "feed"
// it your pieces of data, and, when done, call Get().
class Crc32 {
 public:
  Crc32() : current_(GetInitialCrc32()) {}

  // Updates current CRC32 code to also take into account the |len| bytes that
  // start at address |str|.
  void Update(const char *str, int len);

  // Updates current CRC32 code to also take into account the bytes from |s|.
  void Update(StringPiece s) { Update(s.data(), s.size()); }

  // Returns the CRC32 code for the data so far.
  uint32 Get() const { return current_; }

 private:
  // Returns the initial value for current_.
  static uint32 GetInitialCrc32();

  // CRC32 for the data so far.
  uint32 current_;
};

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_MATH_CHECKSUM_H_
