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

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_MATH_SOFTMAX_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_MATH_SOFTMAX_H_

#include <vector>

namespace libtextclassifier3 {
namespace mobile {

// Computes probability of a softmax label.  Parameter "scores" is the vector of
// softmax logits.  Returns 0.0f if "label" is outside the range [0,
// scores.size()).
float ComputeSoftmaxProbability(const std::vector<float> &scores, int label);

// Computes and returns a softmax for a given vector of floats.  Parameter
// "scores" is the vector of softmax logits.
//
// The alpha parameter is a scaling factor on the logits.
std::vector<float> ComputeSoftmax(const std::vector<float> &scores,
                                  float alpha = 1.0f);

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_MATH_SOFTMAX_H_
