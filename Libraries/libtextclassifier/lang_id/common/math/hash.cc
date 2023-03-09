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

#include "lang_id/common/math/hash.h"

#include "lang_id/common/lite_base/macros.h"

namespace libtextclassifier3 {
namespace mobile {
namespace utils {

namespace {
// Lower-level versions of Get... that read directly from a character buffer
// without any bounds checking.
inline uint32 DecodeFixed32(const char *ptr) {
  return ((static_cast<uint32>(static_cast<unsigned char>(ptr[0]))) |
          (static_cast<uint32>(static_cast<unsigned char>(ptr[1])) << 8) |
          (static_cast<uint32>(static_cast<unsigned char>(ptr[2])) << 16) |
          (static_cast<uint32>(static_cast<unsigned char>(ptr[3])) << 24));
}

// 0xff is in case char is signed.
static inline uint32 ByteAs32(char c) { return static_cast<uint32>(c) & 0xff; }
}  // namespace

uint32 Hash32(const char *data, size_t n, uint32 seed) {
  // 'm' and 'r' are mixing constants generated offline.
  // They're not really 'magic', they just happen to work well.
  const uint32 m = 0x5bd1e995;
  const int r = 24;

  // Initialize the hash to a 'random' value
  uint32 h = seed ^ n;

  // Mix 4 bytes at a time into the hash
  while (n >= 4) {
    uint32 k = DecodeFixed32(data);
    k *= m;
    k ^= k >> r;
    k *= m;
    h *= m;
    h ^= k;
    data += 4;
    n -= 4;
  }

  // Handle the last few bytes of the input array
  switch (n) {
    case 3:
      h ^= ByteAs32(data[2]) << 16;
      SAFTM_FALLTHROUGH_INTENDED;
    case 2:
      h ^= ByteAs32(data[1]) << 8;
      SAFTM_FALLTHROUGH_INTENDED;
    case 1:
      h ^= ByteAs32(data[0]);
      h *= m;
  }

  // Do a few final mixes of the hash to ensure the last few
  // bytes are well-incorporated.
  h ^= h >> 13;
  h *= m;
  h ^= h >> 15;
  return h;
}

}  // namespace utils
}  // namespace mobile
}  // namespace nlp_saft
