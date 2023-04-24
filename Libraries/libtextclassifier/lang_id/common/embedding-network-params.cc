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

#include "lang_id/common/embedding-network-params.h"

#include <string>

#include "lang_id/common/lite_base/logging.h"

namespace libtextclassifier3 {

QuantizationType ParseQuantizationType(const std::string &s) {
  if (s == "NONE") {
    return QuantizationType::NONE;
  }
  if (s == "UINT8") {
    return QuantizationType::UINT8;
  }
  if (s == "UINT4") {
    return QuantizationType::UINT4;
  }
  if (s == "FLOAT16") {
    return QuantizationType::FLOAT16;
  }
  SAFTM_LOG(FATAL) << "Unsupported quantization type: " << s;

  // Execution should never reach this point; just to keep the compiler happy.
  // TODO(salcianu): implement SAFTM_LOG(FATAL) in a way that doesn't require
  // this trick.
  return QuantizationType::NONE;
}

}  // namespace nlp_saft
