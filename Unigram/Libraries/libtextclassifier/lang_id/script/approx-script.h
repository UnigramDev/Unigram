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

#ifndef NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_SCRIPT_APPROX_SCRIPT_H_
#define NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_SCRIPT_APPROX_SCRIPT_H_

#include "lang_id/common/utf8.h"
#include "lang_id/script/script-detector.h"

namespace libtextclassifier3 {
namespace mobile {

// Returns script for the UTF-8 character that starts at address |s| and has
// |num_bytes| bytes.  Note: behavior is unspecified if s points to a UTF-8
// character that has a different number of bytes.  If you don't know
// |num_bytes|, call GetApproxScript(const char *s).
//
// NOTE: to keep BUILD deps small, this function returns an int, but you can
// assume it's an enum UScriptCode (unicode/uscript.h)
//
// If unable to determine the script, this function returns kUnknownUscript, the
// int value of USCRIPT_UNKNOWN from enum UScriptCode.
int GetApproxScript(const unsigned char *s, int num_bytes);

// See comments for GetApproxScript() above.
extern const int kUnknownUscript;

// Same as before, but s is a const char *pointer (no unsigned).  Internally, we
// prefer "unsigned char" (the signed status of char is ambiguous), so we cast
// and call the previous version (with const unsigned char *).
inline int GetApproxScript(const char *s, int num_bytes) {
  return GetApproxScript(reinterpret_cast<const unsigned char *>(s), num_bytes);
}

// Returns script for the UTF-8 character that starts at address |s|.  NOTE:
// UTF-8 is a var-length encoding, taking between 1 and 4 bytes per Unicode
// character.  We infer the number of bytes based on s[0].  If that number is k,
// we expect to be able to read k bytes starting from address |s|.  I.e., do not
// call this function on broken UTF-8.
inline int GetApproxScript(const char *s) {
  return GetApproxScript(s, utils::OneCharLen(s));
}

// Returns max value returned by the GetApproxScript() functions.
int GetMaxApproxScriptResult();

class ApproxScriptDetector : public ScriptDetector {
 public:
  ~ApproxScriptDetector() override = default;

  // Note: the int result of this method is actually a UScriptCode enum value.
  // We return int to match the general case from the base class ScriptDetector
  // (some script detectors do not use UScriptCode).
  int GetScript(const char *s, int num_bytes) const override {
    return GetApproxScript(s, num_bytes);
  }

  int GetMaxScript() const override {
    return GetMaxApproxScriptResult();
  }

  SAFTM_DEFINE_REGISTRATION_METHOD("approx-unicode-script-detector",
                                   ApproxScriptDetector);
};

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_SCRIPT_APPROX_SCRIPT_H_
