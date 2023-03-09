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

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_COMPACT_LOGGING_RAW_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_COMPACT_LOGGING_RAW_H_

#include <string>

#include "lang_id/common/lite_base/compact-logging-levels.h"

namespace libtextclassifier3 {
namespace mobile {
namespace internal_logging {

// Low-level logging primitive.  Logs a message, with the indicated log
// severity.  From android/log.h: "the tag normally corresponds to the component
// that emits the log message, and should be reasonably small".
void LowLevelLogging(LogSeverity severity, const std::string &tag,
                     const std::string &message);

}  // namespace internal_logging
}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_COMPACT_LOGGING_RAW_H_
