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

#ifndef NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_FEATURES_LIGHT_SENTENCE_FEATURES_H_
#define NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_FEATURES_LIGHT_SENTENCE_FEATURES_H_

#include "lang_id/common/fel/feature-extractor.h"
#include "lang_id/common/registry.h"
#include "lang_id/light-sentence.h"

namespace libtextclassifier3 {
namespace mobile {
namespace lang_id {

// Feature function that extracts features from LightSentences.
typedef FeatureFunction<LightSentence> LightSentenceFeature;

// Feature extractor for LightSentences.
typedef FeatureExtractor<LightSentence> LightSentenceExtractor;

}  // namespace lang_id

//SAFTM_DECLARE_CLASS_REGISTRY_NAME(lang_id::LightSentenceFeature);
SAFTM_DEFINE_CLASS_REGISTRY_NAME("light sentence feature function",
                                 lang_id::LightSentenceFeature);

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_LANG_ID_MOBILE_FEATURES_LIGHT_SENTENCE_FEATURES_H_
