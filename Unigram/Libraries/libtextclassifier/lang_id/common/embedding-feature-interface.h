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

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_EMBEDDING_FEATURE_INTERFACE_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_EMBEDDING_FEATURE_INTERFACE_H_

#include <string>
#include <vector>

#include "lang_id/common/embedding-feature-extractor.h"
#include "lang_id/common/fel/feature-extractor.h"
#include "lang_id/common/fel/task-context.h"
#include "lang_id/common/fel/workspace.h"
#include "lang_id/common/lite_base/attributes.h"

namespace libtextclassifier3 {
namespace mobile {

template <class EXTRACTOR, class OBJ, class... ARGS>
class EmbeddingFeatureInterface {
 public:
  // Constructs this EmbeddingFeatureInterface.
  //
  // |arg_prefix| is a string prefix for the TaskContext parameters, passed to
  // |the underlying EmbeddingFeatureExtractor.
  explicit EmbeddingFeatureInterface(const std::string &arg_prefix)
      : feature_extractor_(arg_prefix) {}

  // Sets up feature extractors and flags for processing (inference).
  SAFTM_MUST_USE_RESULT bool SetupForProcessing(TaskContext *context) {
    return feature_extractor_.Setup(context);
  }

  // Initializes feature extractor resources for processing (inference)
  // including requesting a workspace for caching extracted features.
  SAFTM_MUST_USE_RESULT bool InitForProcessing(TaskContext *context) {
    if (!feature_extractor_.Init(context)) return false;
    feature_extractor_.RequestWorkspaces(&workspace_registry_);
    return true;
  }

  // Preprocesses *obj using the internal workspace registry.
  void Preprocess(WorkspaceSet *workspace, OBJ *obj) const {
    workspace->Reset(workspace_registry_);
    feature_extractor_.Preprocess(workspace, obj);
  }

  // Extract features from |obj|.  On return, FeatureVector features[i]
  // contains the features for the embedding space #i.
  //
  // This function uses the precomputed info from |workspace|.  Usage pattern:
  //
  //   EmbeddingFeatureInterface<...> feature_interface;
  //   ...
  //   OBJ obj;
  //   WorkspaceSet workspace;
  //   feature_interface.Preprocess(&workspace, &obj);
  //
  //   // For the same obj, but with different args:
  //   std::vector<FeatureVector> features;
  //   feature_interface.GetFeatures(obj, args, workspace, &features);
  //
  // This pattern is useful (more efficient) if you can pre-compute some info
  // for the entire |obj|, which is reused by the feature extraction performed
  // for different args.  If that is not the case, you can use the simpler
  // version GetFeaturesNoCaching below.
  void GetFeatures(const OBJ &obj, ARGS... args, const WorkspaceSet &workspace,
                   std::vector<FeatureVector> *features) const {
    feature_extractor_.ExtractFeatures(workspace, obj, args..., features);
  }

  // Simpler version of GetFeatures(), for cases when there is no opportunity to
  // reuse computation between feature extractions for the same |obj|, but with
  // different |args|.  Returns the extracted features.  For more info, see the
  // doc for GetFeatures().
  std::vector<FeatureVector> GetFeaturesNoCaching(OBJ *obj,
                                                  ARGS... args) const {
    // Technically, we still use a workspace, because
    // feature_extractor_.ExtractFeatures requires one.  But there is no real
    // caching here, as we start from scratch for each call to ExtractFeatures.
    WorkspaceSet workspace;
    Preprocess(&workspace, obj);
    std::vector<FeatureVector> features(NumEmbeddings());
    GetFeatures(*obj, args..., workspace, &features);
    return features;
  }

  // Returns number of embedding spaces.
  int NumEmbeddings() const { return feature_extractor_.NumEmbeddings(); }

 private:
  // Typed feature extractor for embeddings.
  EmbeddingFeatureExtractor<EXTRACTOR, OBJ, ARGS...> feature_extractor_;

  // The registry of shared workspaces in the feature extractor.
  WorkspaceRegistry workspace_registry_;
};

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_EMBEDDING_FEATURE_INTERFACE_H_
