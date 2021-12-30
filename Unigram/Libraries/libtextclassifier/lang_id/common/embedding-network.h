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

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_EMBEDDING_NETWORK_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_EMBEDDING_NETWORK_H_

#include <vector>

#include "lang_id/common/embedding-network-params.h"
#include "lang_id/common/fel/feature-extractor.h"

namespace libtextclassifier3 {
namespace mobile {

// Classifier using a hand-coded feed-forward neural network.
//
// No gradient computation, just inference.
//
// Based on the more general nlp_saft::EmbeddingNetwork (without ::mobile).
//
// Classification works as follows:
//
// Discrete features -> Embeddings -> Concatenation -> Hidden+ -> Softmax
//
// In words: given some discrete features, this class extracts the embeddings
// for these features, concatenates them, passes them through one or more hidden
// layers (each layer uses Relu) and next through a softmax layer that computes
// an unnormalized score for each possible class.  Note: there is always a
// softmax layer at the end.
class EmbeddingNetwork {
 public:
  // Constructs an embedding network using the parameters from model.
  //
  // Note: model should stay alive for at least the lifetime of this
  // EmbeddingNetwork object.
  explicit EmbeddingNetwork(const EmbeddingNetworkParams *model);

  virtual ~EmbeddingNetwork() {}

  // Runs forward computation to fill scores with unnormalized output unit
  // scores. This is useful for making predictions.
  void ComputeFinalScores(const std::vector<FeatureVector> &features,
                          std::vector<float> *scores) const;

  // Same as above, but allows specification of extra extra neural network
  // inputs that will be appended to the embedding vector build from features.
  void ComputeFinalScores(const std::vector<FeatureVector> &features,
                          const std::vector<float> &extra_inputs,
                          std::vector<float> *scores) const;

 private:
  // Constructs the concatenated input embedding vector in place in output
  // vector concat.
  void ConcatEmbeddings(const std::vector<FeatureVector> &features,
                        std::vector<float> *concat) const;

  // Pointer to the model object passed to the constructor.  Not owned.
  const EmbeddingNetworkParams *model_;

  // Network parameters.

  // One weight matrix for each embedding.
  std::vector<EmbeddingNetworkParams::Matrix> embedding_matrices_;

  // embedding_row_size_in_bytes_[i] is the size (in bytes) of a row from
  // embedding_matrices_[i].  We precompute this in order to quickly find the
  // beginning of the k-th row from an embedding matrix (which is stored in
  // row-major order).
  std::vector<int> embedding_row_size_in_bytes_;

  // concat_offset_[i] is the input layer offset for i-th embedding space.
  std::vector<int> concat_offset_;

  // Size of the input ("concatenation") layer.
  int concat_layer_size_ = 0;

  // One weight matrix and one vector of bias weights for each layer of neurons.
  // Last layer is the softmax layer, the previous ones are the hidden layers.
  std::vector<EmbeddingNetworkParams::Matrix> layer_weights_;
  std::vector<EmbeddingNetworkParams::Matrix> layer_bias_;
};

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_EMBEDDING_NETWORK_H_
