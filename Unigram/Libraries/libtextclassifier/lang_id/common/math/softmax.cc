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

#include "lang_id/common/math/softmax.h"

#include <algorithm>

#include "lang_id/common/lite_base/logging.h"
#include "lang_id/common/math/fastexp.h"

namespace libtextclassifier3 {
namespace mobile {

float ComputeSoftmaxProbability(const std::vector<float> &scores, int label) {
  if ((label < 0) || (label >= scores.size())) {
    SAFTM_LOG(ERROR) << "label " << label << " outside range "
                     << "[0, " << scores.size() << ")";
    return 0.0f;
  }

  // Standard softmax formula for label's probability is
  //
  //   exp(scores[label]) / sum_i exp(scores[i])
  //
  // We compute the mathematically equivalent
  //
  //   1 / (1 + sum_{i != label} exp(scores[i] - scores[label]))
  //
  // which saves two calls to exp().
  const float label_score = scores[label];
  float denominator = 1.0f;  // Contribution of i == label.
  for (int i = 0; i < scores.size(); ++i) {
    if (i == label) continue;
    const float delta_score = scores[i] - label_score;

    // TODO(salcianu): one can optimize the test below, to avoid any float
    // operation: extract exponent (via bit mask + shift) and check it's >= 4.
    if (fabs(delta_score) >= 16.0f) {
      if (delta_score > 0.0f) {
        // If delta_score >= 16, the denominator (e^delta_score + other positive
        // terms) is very big and its inverse can be approximated with 0.
        return 0.0f;
      } else {
        // If delta_score <= -16, then e^delta_score < 1.2e-7.  Even if we have
        // 1000 such labels i, their sum is < 1.2e-4 (which gets summed with
        // 1.0f for i == label).  Hence, we can approximate each such label with
        // 0 and skip the call to VeryFastExp and the update to denominator.
        continue;
      }
    }

    // At this point, delta_score is in (-16.0, 16.0).  For such values, vfexp
    // works fine: no under/overflows (we have tests for that in fastexp_test).
    // Also, even for 1000 labels, denominator will not overflow.
    denominator += VeryFastExp(delta_score);
  }
  return 1.0f / denominator;
}

std::vector<float> ComputeSoftmax(const std::vector<float> &scores,
                                  float alpha) {
  std::vector<float> softmax;
  softmax.reserve(scores.size());
  if (scores.empty()) {
    return softmax;
  }

  std::vector<float> exp_scores;
  exp_scores.reserve(scores.size());

  // Find max value in "scores" vector and rescale to avoid overflows.
  const float max_score = *std::max_element(scores.begin(), scores.end());
  float denominator = 0;
  for (const float score : scores) {
    // See comments above in ComputeSoftmaxProbability for the reasoning behind
    // this approximation.
    const float delta_score = alpha * (score - max_score);
    const float exp_score = delta_score < -16.0f ? 0 : VeryFastExp(delta_score);
    exp_scores.push_back(exp_score);
    denominator += exp_score;
  }

  for (int i = 0; i < scores.size(); ++i) {
    softmax.push_back(exp_scores[i] / denominator);
  }
  return softmax;
}

}  // namespace mobile
}  // namespace nlp_saft
