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

#ifndef NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_SCRIPT_SCRIPT_DETECTOR_H_
#define NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_SCRIPT_SCRIPT_DETECTOR_H_

#include "lang_id/common/registry.h"

namespace libtextclassifier3 {
namespace mobile {

// Base class for Unicode script detectors.  Individual detectors may differ in
// code size, speed, precision, etc.  You can use the registration mechanism to
// get the ScriptDetector that's most appropriate to your application.
class ScriptDetector : public RegisterableClass<ScriptDetector> {
 public:
  virtual ~ScriptDetector() = default;

  // Returns a number between 0 and GetMaxScript() (inclusive on both ends) that
  // indicates the script of the UTF8 character that starts at address |s| and
  // has |num_bytes|.
  virtual int GetScript(const char *s, int num_bytes) const = 0;

  // Returns max result that can be returned by GetScript().
  virtual int GetMaxScript() const = 0;
};

//SAFTM_DECLARE_CLASS_REGISTRY_NAME(ScriptDetector);
SAFTM_DEFINE_CLASS_REGISTRY_NAME("script detector", ScriptDetector);

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_SCRIPT_SCRIPT_DETECTOR_H_
