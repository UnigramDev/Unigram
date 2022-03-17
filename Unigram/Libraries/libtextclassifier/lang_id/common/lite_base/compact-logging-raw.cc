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

#include "lang_id/common/lite_base/compact-logging-raw.h"

#include <stdio.h>

#include <string>

// NOTE: this file contains two implementations: one for Android, one for all
// other cases.  We always build exactly one implementation.
#if defined(__ANDROID__)

// Compiled as part of Android.
#include <android/log.h>

namespace libtextclassifier3 {
namespace mobile {
namespace internal_logging {

namespace {
// Converts LogSeverity to level for __android_log_write.
int GetAndroidLogLevel(LogSeverity severity) {
  switch (severity) {
    case FATAL:
      return ANDROID_LOG_FATAL;
    case ERROR:
      return ANDROID_LOG_ERROR;
    case WARNING:
      return ANDROID_LOG_WARN;
    case INFO:
      return ANDROID_LOG_INFO;
    default:
      return ANDROID_LOG_DEBUG;
  }
}
}  // namespace

void LowLevelLogging(LogSeverity severity, const std::string &tag,
                     const std::string &message) {
  const int android_log_level = GetAndroidLogLevel(severity);
#if !defined(SAFTM_DEBUG_LOGGING)
  if (android_log_level != ANDROID_LOG_ERROR &&
      android_log_level != ANDROID_LOG_FATAL) {
    return;
  }
#endif
  __android_log_write(android_log_level, tag.c_str(), message.c_str());
}

}  // namespace internal_logging
}  // namespace mobile
}  // namespace nlp_saft

#else  // if defined(__ANDROID__)

// Not on Android: implement LowLevelLogging to print to stderr (see below).
namespace libtextclassifier3 {
namespace mobile {
namespace internal_logging {

namespace {
// Converts LogSeverity to human-readable text.
const char *LogSeverityToString(LogSeverity severity) {
  switch (severity) {
    case INFO:
      return "INFO";
    case WARNING:
      return "WARNING";
    case ERROR:
      return "ERROR";
    case FATAL:
      return "FATAL";
    default:
      return "UNKNOWN";
  }
}
}  // namespace

void LowLevelLogging(LogSeverity severity, const std::string &tag,
                     const std::string &message) {
  fprintf(stderr, "[%s] %s : %s\n", LogSeverityToString(severity), tag.c_str(),
          message.c_str());
  fflush(stderr);
}

}  // namespace internal_logging
}  // namespace mobile
}  // namespace nlp_saft

#endif  // if defined(__ANDROID__)
