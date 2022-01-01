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

#include "lang_id/common/math/checksum.h"

// Though we use the same zlib header on all platforms, the implementation used
// is from NDK on android and from third_party/zlib on iOS/linux.  See BUILD
// rule.
#include <zlib.h>

namespace libtextclassifier3 {
namespace mobile {

// static
uint32 Crc32::GetInitialCrc32() {
  static const uint32 kCrcInitZero = crc32(0L, nullptr, 0);
  return kCrcInitZero;
}

void Crc32::Update(const char *str, int len) {
  if (str == nullptr || len == 0) {
    return;
  }
  current_ = crc32(current_,
                   reinterpret_cast<const unsigned char *>(str),
                   len);
}

}  // namespace mobile
}  // namespace nlp_saft
