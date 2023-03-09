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

#ifndef NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_FEATURES_CHAR_NGRAM_FEATURE_H_
#define NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_FEATURES_CHAR_NGRAM_FEATURE_H_

#include <mutex>  // NOLINT: see comments for state_mutex_
#include <string>

#include "lang_id/common/fel/feature-extractor.h"
#include "lang_id/common/fel/task-context.h"
#include "lang_id/common/fel/workspace.h"
#include "lang_id/features/light-sentence-features.h"
#include "lang_id/light-sentence.h"

// TODO(abakalov): Add a test.
namespace libtextclassifier3 {
namespace mobile {
namespace lang_id {

// Class for computing continuous char ngram features.
//
// Feature function descriptor parameters:
//   include_terminators(bool, false):
//     If 'true', then splits the text based on spaces to get tokens, adds "^"
//     to the beginning of each token, and adds "$" to the end of each token.
//     NOTE: currently, we support only include_terminators=true.
//   include_spaces(bool, false):
//     If 'true', then includes char ngrams containing spaces.
//     NOTE: currently, we support only include_spaces=false.
//   use_equal_weight(bool, false):
//     If 'true', then weighs each unique ngram by 1.0 / (number of unique
//     ngrams in the input). Otherwise, weighs each unique ngram by (ngram
//     count) / (total number of ngrams).
//     NOTE: currently, we support only use_equal_weight=false.
//   id_dim(int, 10000):
//     The integer id of each char ngram is computed as follows:
//     Hash32WithDefault(char ngram) % id_dim.
//   size(int, 3):
//     Only ngrams of this size will be extracted.
//
// NOTE: this class is not thread-safe.  TODO(salcianu): make it thread-safe.
class ContinuousBagOfNgramsFunction : public LightSentenceFeature {
 public:
  bool Setup(TaskContext *context) override;
  bool Init(TaskContext *context) override;

  // Appends the features computed from the sentence to the feature vector.
  void Evaluate(const WorkspaceSet &workspaces, const LightSentence &sentence,
                FeatureVector *result) const override;

  SAFTM_DEFINE_REGISTRATION_METHOD("continuous-bag-of-ngrams",
                                   ContinuousBagOfNgramsFunction);

 private:
  // Auxiliary for Evaluate().  Fills counts_ and non_zero_count_indices_ (see
  // below), and returns the total ngram count.
  int ComputeNgramCounts(const LightSentence &sentence) const;

  // Guards counts_ and non_zero_count_indices_.  NOTE: we use std::* constructs
  // (instead of absl::Mutex & co) to simplify porting to Android and to avoid
  // pulling in absl (which increases our code size).
  mutable std::mutex state_mutex_;

  // counts_[i] is the count of all ngrams with id i.  Work data for Evaluate().
  // NOTE: we declare this vector as a field, such that its underlying capacity
  // stays allocated in between calls to Evaluate().
  mutable std::vector<int> counts_;

  // Indices of non-zero elements of counts_.  See comments for counts_.
  mutable std::vector<int> non_zero_count_indices_;

  // The integer id of each char ngram is computed as follows:
  // Hash32WithDefaultSeed(char_ngram) % ngram_id_dimension_.
  int ngram_id_dimension_;

  // Only ngrams of size ngram_size_ will be extracted.
  int ngram_size_;
};

}  // namespace lang_id
}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_FEATURES_CHAR_NGRAM_FEATURE_H_
