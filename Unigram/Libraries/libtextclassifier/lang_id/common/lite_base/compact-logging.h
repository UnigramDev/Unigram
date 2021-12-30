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

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_COMPACT_LOGGING_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_COMPACT_LOGGING_H_

#include <cassert>
#include <string>

#include "lang_id/common/lite_base/attributes.h"
#include "lang_id/common/lite_base/compact-logging-levels.h"
#include "lang_id/common/lite_strings/stringpiece.h"

namespace libtextclassifier3 {
namespace mobile {
namespace internal_logging {

// A tiny code footprint string stream for assembling log messages.
struct LoggingStringStream {
  LoggingStringStream() {}
  LoggingStringStream &stream() { return *this; }

  // Needed for invocation in SAFTM_CHECK macro.
  explicit operator bool() const { return true; }

  std::string message;
};

template <typename T>
inline LoggingStringStream &operator<<(LoggingStringStream &stream,
                                       const T &entry) {
  stream.message.append(std::to_string(entry));
  return stream;
}

inline LoggingStringStream &operator<<(LoggingStringStream &stream,
                                       const char *message) {
  stream.message.append(message);
  return stream;
}

inline LoggingStringStream &operator<<(LoggingStringStream &stream,
                                       const std::string &message) {
  stream.message.append(message);
  return stream;
}

inline LoggingStringStream &operator<<(LoggingStringStream &stream,
                                       StringPiece sp) {
  stream.message.append(sp.data(), sp.size());
  return stream;
}

// The class that does all the work behind our SAFTM_LOG(severity) macros.  Each
// SAFTM_LOG(severity) << obj1 << obj2 << ...; logging statement creates a
// LogMessage temporary object containing a stringstream.  Each operator<< adds
// info to that stringstream and the LogMessage destructor performs the actual
// logging.  The reason this works is that in C++, "all temporary objects are
// destroyed as the last step in evaluating the full-expression that (lexically)
// contains the point where they were created."  For more info, see
// http://en.cppreference.com/w/cpp/language/lifetime.  Hence, the destructor is
// invoked after the last << from that logging statement.
class LogMessage {
 public:
  SAFTM_ATTRIBUTE_NOINLINE LogMessage(LogSeverity severity, const char *file_name,
             int line_number);

  SAFTM_ATTRIBUTE_NOINLINE ~LogMessage();

  // Returns the stream associated with the logger object.
  LoggingStringStream &stream() { return stream_; }

 private:
  const LogSeverity severity_;

  // Stream that "prints" all info into a string (not to a file).  We construct
  // here the entire logging message and next print it in one operation.
  LoggingStringStream stream_;
};

// Pseudo-stream that "eats" the tokens <<-pumped into it, without printing
// anything.
class NullStream {
 public:
  NullStream() {}
  NullStream &stream() { return *this; }
};
template <typename T>
inline NullStream &operator<<(NullStream &str, const T &) {
  return str;
}

}  // namespace internal_logging
}  // namespace mobile
}  // namespace nlp_saft

#define SAFTM_LOG(severity)                                               \
  ::libtextclassifier3::mobile::internal_logging::LogMessage(                       \
      ::libtextclassifier3::mobile::internal_logging::severity, __FILE__, __LINE__) \
      .stream()

// If condition x is true, does nothing.  Otherwise, crashes the program (liek
// LOG(FATAL)) with an informative message.  Can be continued with extra
// messages, via <<, like any logging macro, e.g.,
//
// SAFTM_CHECK(my_cond) << "I think we hit a problem";
#define SAFTM_CHECK(x)                                                \
  (x) || SAFTM_LOG(FATAL) << __FILE__ << ":" << __LINE__              \
  << ": check failed: \"" << #x

#define SAFTM_CHECK_EQ(x, y) SAFTM_CHECK((x) == (y))
#define SAFTM_CHECK_LT(x, y) SAFTM_CHECK((x) < (y))
#define SAFTM_CHECK_GT(x, y) SAFTM_CHECK((x) > (y))
#define SAFTM_CHECK_LE(x, y) SAFTM_CHECK((x) <= (y))
#define SAFTM_CHECK_GE(x, y) SAFTM_CHECK((x) >= (y))
#define SAFTM_CHECK_NE(x, y) SAFTM_CHECK((x) != (y))

#define SAFTM_NULLSTREAM \
  ::libtextclassifier3::mobile::internal_logging::NullStream().stream()

// Debug checks: a SAFTM_DCHECK<suffix> macro should behave like
// SAFTM_CHECK<suffix> in debug mode an don't check / don't print anything in
// non-debug mode.
#ifdef NDEBUG

#define SAFTM_DCHECK(x) SAFTM_NULLSTREAM
#define SAFTM_DCHECK_EQ(x, y) SAFTM_NULLSTREAM
#define SAFTM_DCHECK_LT(x, y) SAFTM_NULLSTREAM
#define SAFTM_DCHECK_GT(x, y) SAFTM_NULLSTREAM
#define SAFTM_DCHECK_LE(x, y) SAFTM_NULLSTREAM
#define SAFTM_DCHECK_GE(x, y) SAFTM_NULLSTREAM
#define SAFTM_DCHECK_NE(x, y) SAFTM_NULLSTREAM

// In non-debug mode, SAFT_DLOG statements do not generate any logging.
#define SAFTM_DLOG(severity) SAFTM_NULLSTREAM

#else  // NDEBUG

// In debug mode, each SAFTM_DCHECK<suffix> is equivalent to
// SAFTM_CHECK<suffix>, i.e., a real check that crashes when the condition is
// not true.
#define SAFTM_DCHECK(x) SAFTM_CHECK(x)
#define SAFTM_DCHECK_EQ(x, y) SAFTM_CHECK_EQ(x, y)
#define SAFTM_DCHECK_LT(x, y) SAFTM_CHECK_LT(x, y)
#define SAFTM_DCHECK_GT(x, y) SAFTM_CHECK_GT(x, y)
#define SAFTM_DCHECK_LE(x, y) SAFTM_CHECK_LE(x, y)
#define SAFTM_DCHECK_GE(x, y) SAFTM_CHECK_GE(x, y)
#define SAFTM_DCHECK_NE(x, y) SAFTM_CHECK_NE(x, y)

// In debug mode, SAFT_DLOG statements are like SAFT_LOG.
#define SAFTM_DLOG SAFTM_LOG

#endif  // NDEBUG

#ifdef LIBTEXTCLASSIFIER_VLOG
#define SAFTM_VLOG(severity)                                              \
  ::libtextclassifier3::mobile::internal_logging::LogMessage(                     \
       ::libtextclassifier3::mobile::internal_logging::INFO, __FILE__, __LINE__)  \
  .stream()
#else
#define SAFTM_VLOG(severity) SAFTM_NULLSTREAM
#endif

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_LITE_BASE_COMPACT_LOGGING_H_
