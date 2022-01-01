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

#include "lang_id/features/relevant-script-feature.h"

#include <string>

#include "lang_id/common/fel/feature-types.h"
#include "lang_id/common/fel/task-context.h"
#include "lang_id/common/fel/workspace.h"
#include "lang_id/common/lite_base/logging.h"
#include "lang_id/common/utf8.h"
#include "lang_id/script/script-detector.h"

namespace libtextclassifier3 {
namespace mobile {
namespace lang_id {

bool RelevantScriptFeature::Setup(TaskContext *context) {
  std::string script_detector_name = GetParameter(
      "script_detector_name", /* default_value = */ "tiny-script-detector");

  // We don't use absl::WrapUnique, nor the rest of absl, see http://b/71873194
  script_detector_.reset(ScriptDetector::Create(script_detector_name));
  if (script_detector_ == nullptr) {
    // This means ScriptDetector::Create() could not find the requested
    // script_detector_name.  In that case, Create() already logged an error
    // message.
    return false;
  }

  // We use default value 172 because this is the number of scripts supported by
  // the first model we trained with this feature.  See http://b/70617713.
  // Newer models may support more scripts.
  num_supported_scripts_ = GetIntParameter("num_supported_scripts", 172);
  return true;
}

bool RelevantScriptFeature::Init(TaskContext *context) {
  set_feature_type(new NumericFeatureType(name(), num_supported_scripts_));
  return true;
}

void RelevantScriptFeature::Evaluate(
    const WorkspaceSet &workspaces, const LightSentence &sentence,
    FeatureVector *result) const {
  // counts[s] is the number of characters with script s.
  std::vector<int> counts(num_supported_scripts_);
  int total_count = 0;
  for (const std::string &word : sentence) {
    const char *const word_end = word.data() + word.size();
    const char *curr = word.data();

    // Skip over token start '^'.
    SAFTM_DCHECK_EQ(*curr, '^');
    curr += utils::OneCharLen(curr);
    while (true) {
      const int num_bytes = utils::OneCharLen(curr);

      int script = script_detector_->GetScript(curr, num_bytes);

      // We do this update and the if (...) break below *before* incrementing
      // counts[script] in order to skip the token end '$'.
      curr += num_bytes;
      if (curr >= word_end) {
        SAFTM_DCHECK_EQ(*(curr - num_bytes), '$');
        break;
      }
      SAFTM_DCHECK_GE(script, 0);

      if (script < num_supported_scripts_) {
        counts[script]++;
        total_count++;
      } else {
        // Unsupported script: this usually indicates a script that is
        // recognized by newer versions of the code, after the model was
        // trained.  E.g., new code running with old model.
      }
    }
  }

  for (int script_id = 0; script_id < num_supported_scripts_; ++script_id) {
    int count = counts[script_id];
    if (count > 0) {
      const float weight = static_cast<float>(count) / total_count;
      FloatFeatureValue value(script_id, weight);
      result->add(feature_type(), value.discrete_value);
    }
  }
}

SAFTM_STATIC_REGISTRATION(RelevantScriptFeature);

}  // namespace lang_id
}  // namespace mobile
}  // namespace nlp_saft
