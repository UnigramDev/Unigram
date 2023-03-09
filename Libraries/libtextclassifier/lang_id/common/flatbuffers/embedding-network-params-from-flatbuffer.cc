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

#include "lang_id/common/flatbuffers/embedding-network-params-from-flatbuffer.h"

#include "lang_id/common/lite_base/endian.h"
#include "lang_id/common/lite_base/logging.h"
#include "lang_id/common/lite_base/macros.h"

namespace libtextclassifier3 {
namespace mobile {

namespace {
// Returns true if and only if ptr points to a location inside allowed_range.
bool IsPointerInRange(const char *ptr, StringPiece allowed_range) {
  return (ptr >= allowed_range.data()) &&
         (ptr < (allowed_range.data() + allowed_range.size()));
}

// Returns true if and only if the memory range [start, start +
// range_size_in_bytes) is included inside allowed_range.
//
// Special case: if range_size_in_bytes == 0 (empty range) then we require that
// start is nullptr or in the allowed_range.
bool IsMemoryRangeValid(const void *start, int range_size_in_bytes,
                        StringPiece allowed_range) {
  const char *begin = reinterpret_cast<const char *>(start);
  if (range_size_in_bytes < 0) {
    return false;
  }
  if (range_size_in_bytes == 0) {
    return (start == nullptr) || IsPointerInRange(begin, allowed_range);
  }
  const char *inclusive_end = begin + (range_size_in_bytes - 1);
  return (begin <= inclusive_end) && IsPointerInRange(begin, allowed_range) &&
         IsPointerInRange(inclusive_end, allowed_range);
}

bool VerifyQuantizationScales(EmbeddingNetworkParams::Matrix matrix,
                              StringPiece bytes) {
  if (matrix.quant_scales == nullptr) {
    SAFTM_LOG(ERROR) << "Quantization type "
                     << static_cast<int>(matrix.quant_type)
                     << "; but no quantization scales";
    return false;
  }
  bool valid_scales = IsMemoryRangeValid(matrix.quant_scales,
                                         matrix.rows * sizeof(float16), bytes);
  if (!valid_scales) {
    SAFTM_LOG(ERROR) << "quantization scales not fully inside bytes";
    return false;
  }
  return true;
}

// Returns false if we detect a problem with |matrix|, true otherwise.  E.g., we
// check that the array that starts at pointer matrix.elements is fully inside
// |bytes| (the range of bytes passed to the
// EmbeddingNetworkParamsFromFlatbuffer constructor).
bool VerifyMatrix(EmbeddingNetworkParams::Matrix matrix, StringPiece bytes) {
  if ((matrix.rows < 0) || (matrix.cols < 0)) {
    SAFTM_LOG(ERROR) << "Wrong matrix geometry: " << matrix.rows << " x "
                     << matrix.cols;
    return false;
  }

  const int num_elements = matrix.rows * matrix.cols;

  // Number of bytes occupied by the num_elements elements that start at address
  // matrix.elements.
  int element_range_size_in_bytes = 0;
  switch (matrix.quant_type) {
    case QuantizationType::NONE:
      element_range_size_in_bytes = num_elements * sizeof(float);
      break;
    case QuantizationType::UINT8: {
      element_range_size_in_bytes = num_elements;
      if (!VerifyQuantizationScales(matrix, bytes)) {
        return false;
      }
      break;
    }
    case QuantizationType::UINT4: {
      if (matrix.cols % 2 != 0) {
        SAFTM_LOG(ERROR) << "UINT4 doesn't work with odd #cols" << matrix.cols;
        return false;
      }
      element_range_size_in_bytes = num_elements / 2;
      if (!VerifyQuantizationScales(matrix, bytes)) {
        return false;
      }
      break;
    }
    case QuantizationType::FLOAT16: {
      element_range_size_in_bytes = num_elements * sizeof(float16);

      // No need to verify the scales: FLOAT16 quantization does not use scales.
      break;
    }
    default:
      SAFTM_LOG(ERROR) << "Unsupported quantization type "
                       << static_cast<int>(matrix.quant_type);
      return false;
  }
  if (matrix.elements == nullptr) {
    SAFTM_LOG(ERROR) << "matrix.elements == nullptr";
    return false;
  }
  bool valid =
      IsMemoryRangeValid(matrix.elements, element_range_size_in_bytes, bytes);
  if (!valid) {
    SAFTM_LOG(ERROR) << "elements not fully inside bytes";
    return false;
  }
  return true;
}

// Checks the geometry of the network layer represented by |weights| and |bias|,
// assuming the input to this layer has size |input_size|.  Returns false if we
// detect any problem, true otherwise.
bool GoodLayerGeometry(int input_size,
                       const EmbeddingNetworkParams::Matrix &weights,
                       const EmbeddingNetworkParams::Matrix &bias) {
  if (weights.rows != input_size) {
    SAFTM_LOG(ERROR) << "#rows " << weights.rows << " != " << input_size;
    return false;
  }
  if ((bias.rows != 1) && (bias.cols != 1)) {
    SAFTM_LOG(ERROR) << "bad bias vector geometry: " << bias.rows << " x "
                     << bias.cols;
    return false;
  }
  int bias_dimension = bias.rows * bias.cols;
  if (weights.cols != bias_dimension) {
    SAFTM_LOG(ERROR) << "#cols " << weights.cols << " != " << bias_dimension;
    return false;
  }
  return true;
}
}  // namespace

EmbeddingNetworkParamsFromFlatbuffer::EmbeddingNetworkParamsFromFlatbuffer(
    StringPiece bytes) {
  // We expect valid_ to be initialized to false at this point.  We set it to
  // true only if we successfully complete all initialization.  On error, we
  // return early, leaving valid_ set to false.
  SAFTM_DCHECK(!valid_);

  // NOTE: current EmbeddingNetworkParams API works only on little-endian
  // machines.  Fortunately, all modern devices are little-endian so, instead of
  // a costly API change, we support only the little-endian case.
  //
  // Technical explanation: for each Matrix, our API provides a pointer to the
  // matrix elements (see Matrix field |elements|).  For unquantized matrices,
  // that's a const float *pointer; the client code (e.g., Neurosis) uses those
  // floats directly.  That is correct if the EmbeddingNetworkParams come from a
  // proto, where the proto parsing already handled the endianness differences.
  // But in the flatbuffer case, that's a pointer to floats in little-endian
  // format (flatbuffers always use little-endian).  If our API provided access
  // to only one element at a time, the accessor method could swap the bytes "on
  // the fly", using temporary variables.  Instead, our API provides a pointer
  // to all elements: as their number is variable (and underlying data is
  // immutable), we can't ensure the bytes of all those elements are swapped
  // without extra memory allocation to store the swapped bytes (which is what
  // using flatbuffers is supposed to prevent).
  if (!LittleEndian::IsLittleEndian()) {
    SAFTM_LOG(INFO) << "Not a little-endian machine";
    return;
  }

  const uint8_t *start = reinterpret_cast<const uint8_t *>(bytes.data());
  if (start == nullptr) {
    // Note: as |bytes| is expected to be a valid EmbeddingNetwork flatbuffer,
    // it should contain the 4-char identifier "NS00" (or a later version).  It
    // can't be empty; hence StringPiece(nullptr, 0) is not legal here.
    SAFTM_LOG(ERROR) << "nullptr bytes";
    return;
  }
  flatbuffers::Verifier verifier(start, bytes.size());
  if (!saft_fbs::VerifyEmbeddingNetworkBuffer(verifier)) {
    SAFTM_LOG(ERROR) << "Not a valid EmbeddingNetwork flatbuffer";
    return;
  }
  network_ = saft_fbs::GetEmbeddingNetwork(start);
  if (network_ == nullptr) {
    SAFTM_LOG(ERROR) << "Unable to interpret bytes as a flatbuffer";
    return;
  }

  // Perform a few extra checks before declaring this object valid.
  valid_ = ValidityChecking(bytes);
}

bool EmbeddingNetworkParamsFromFlatbuffer::ValidityChecking(
    StringPiece bytes) const {
  int input_size = 0;
  for (int i = 0; i < embeddings_size(); ++i) {
    Matrix embeddings = GetEmbeddingMatrix(i);
    if (!VerifyMatrix(embeddings, bytes)) {
      SAFTM_LOG(ERROR) << "Bad embedding matrix #" << i;
      return false;
    }
    input_size += embedding_num_features(i) * embeddings.cols;
  }
  int current_size = input_size;
  for (int i = 0; i < hidden_size(); ++i) {
    Matrix weights = GetHiddenLayerMatrix(i);
    if (!VerifyMatrix(weights, bytes)) {
      SAFTM_LOG(ERROR) << "Bad weights matrix for hidden layer #" << i;
      return false;
    }
    Matrix bias = GetHiddenLayerBias(i);
    if (!VerifyMatrix(bias, bytes)) {
      SAFTM_LOG(ERROR) << "Bad bias vector for hidden layer #" << i;
      return false;
    }
    if (!GoodLayerGeometry(current_size, weights, bias)) {
      SAFTM_LOG(ERROR) << "Bad geometry for hidden layer #" << i;
      return false;
    }
    current_size = weights.cols;
  }

  if (HasSoftmax()) {
    Matrix weights = GetSoftmaxMatrix();
    if (!VerifyMatrix(weights, bytes)) {
      SAFTM_LOG(ERROR) << "Bad weights matrix for softmax";
      return false;
    }
    Matrix bias = GetSoftmaxBias();
    if (!VerifyMatrix(bias, bytes)) {
      SAFTM_LOG(ERROR) << "Bad bias vector for softmax";
      return false;
    }
    if (!GoodLayerGeometry(current_size, weights, bias)) {
      SAFTM_LOG(ERROR) << "Bad geometry for softmax layer";
      return false;
    }
  }
  return true;
}

// static
bool EmbeddingNetworkParamsFromFlatbuffer::InRangeIndex(int index, int limit,
                                                        const char *info) {
  if ((index >= 0) && (index < limit)) {
    return true;
  } else {
    SAFTM_LOG(ERROR) << info << " index " << index << " outside range [0, "
                     << limit << ")";
    return false;
  }
}

int EmbeddingNetworkParamsFromFlatbuffer::SafeGetNumInputChunks() const {
  const auto *input_chunks = network_->input_chunks();
  if (input_chunks == nullptr) {
    SAFTM_LOG(ERROR) << "nullptr input_chunks";
    return 0;
  }
  return input_chunks->size();
}

const saft_fbs::InputChunk *
EmbeddingNetworkParamsFromFlatbuffer::SafeGetInputChunk(int i) const {
  if (!InRangeIndex(i, SafeGetNumInputChunks(), "input chunks")) {
    return nullptr;
  }
  const auto *input_chunks = network_->input_chunks();
  if (input_chunks == nullptr) {
    // Execution should not reach this point, due to how SafeGetNumInputChunks()
    // is implemented.  Still, just to be sure:
    SAFTM_LOG(ERROR) << "nullptr input_chunks";
    return nullptr;
  }
  const saft_fbs::InputChunk *input_chunk = input_chunks->Get(i);
  if (input_chunk == nullptr) {
    SAFTM_LOG(ERROR) << "nullptr input chunk #" << i;
  }
  return input_chunk;
}

const saft_fbs::Matrix *
EmbeddingNetworkParamsFromFlatbuffer::SafeGetEmbeddingMatrix(int i) const {
  const saft_fbs::InputChunk *input_chunk = SafeGetInputChunk(i);
  if (input_chunk == nullptr) return nullptr;
  const saft_fbs::Matrix *matrix = input_chunk->embedding();
  if (matrix == nullptr) {
    SAFTM_LOG(ERROR) << "nullptr embeding matrix #" << i;
  }
  return matrix;
}

int EmbeddingNetworkParamsFromFlatbuffer::SafeGetNumLayers() const {
  const auto *layers = network_->layers();
  if (layers == nullptr) {
    SAFTM_LOG(ERROR) << "nullptr layers";
    return 0;
  }
  return layers->size();
}

const saft_fbs::NeuralLayer *EmbeddingNetworkParamsFromFlatbuffer::SafeGetLayer(
    int i) const {
  if (!InRangeIndex(i, SafeGetNumLayers(), "layer")) {
    return nullptr;
  }
  const auto *layers = network_->layers();
  if (layers == nullptr) {
    // Execution should not reach this point, due to how SafeGetNumLayers()
    // is implemented.  Still, just to be sure:
    SAFTM_LOG(ERROR) << "nullptr layers";
    return nullptr;
  }
  const saft_fbs::NeuralLayer *layer = layers->Get(i);
  if (layer == nullptr) {
    SAFTM_LOG(ERROR) << "nullptr layer #" << i;
  }
  return layer;
}

const saft_fbs::Matrix *
EmbeddingNetworkParamsFromFlatbuffer::SafeGetLayerWeights(int i) const {
  const saft_fbs::NeuralLayer *layer = SafeGetLayer(i);
  if (layer == nullptr) return nullptr;
  const saft_fbs::Matrix *weights = layer->weights();
  if (weights == nullptr) {
    SAFTM_LOG(ERROR) << "nullptr weights for layer #" << i;
  }
  return weights;
}

const saft_fbs::Matrix *EmbeddingNetworkParamsFromFlatbuffer::SafeGetLayerBias(
    int i) const {
  const saft_fbs::NeuralLayer *layer = SafeGetLayer(i);
  if (layer == nullptr) return nullptr;
  const saft_fbs::Matrix *bias = layer->bias();
  if (bias == nullptr) {
    SAFTM_LOG(ERROR) << "nullptr bias for layer #" << i;
  }
  return bias;
}

// static
const float *EmbeddingNetworkParamsFromFlatbuffer::SafeGetValues(
    const saft_fbs::Matrix *matrix) {
  if (matrix == nullptr) return nullptr;
  const flatbuffers::Vector<float> *values = matrix->values();
  if (values == nullptr) {
    SAFTM_LOG(ERROR) << "nullptr values";
  }
  return values->data();
}

// static
const uint8_t *EmbeddingNetworkParamsFromFlatbuffer::SafeGetQuantizedValues(
    const saft_fbs::Matrix *matrix) {
  if (matrix == nullptr) return nullptr;
  const flatbuffers::Vector<uint8_t> *quantized_values =
      matrix->quantized_values();
  if (quantized_values == nullptr) {
    SAFTM_LOG(ERROR) << "nullptr quantized_values";
  }
  return quantized_values->data();
}

// static
const float16 *EmbeddingNetworkParamsFromFlatbuffer::SafeGetScales(
    const saft_fbs::Matrix *matrix) {
  if (matrix == nullptr) return nullptr;
  const flatbuffers::Vector<uint16_t> *scales = matrix->scales();
  if (scales == nullptr) {
    SAFTM_LOG(ERROR) << "nullptr scales";
    return nullptr;
  }
  return scales->data();
}

const saft_fbs::NeuralLayer *
EmbeddingNetworkParamsFromFlatbuffer::SafeGetSoftmaxLayer() const {
  int num_layers = SafeGetNumLayers();
  if (num_layers <= 0) {
    SAFTM_LOG(ERROR) << "No softmax layer";
    return nullptr;
  }
  return SafeGetLayer(num_layers - 1);
}

QuantizationType EmbeddingNetworkParamsFromFlatbuffer::SafeGetQuantizationType(
    const saft_fbs::Matrix *matrix) const {
  if (matrix == nullptr) {
    return QuantizationType::NONE;
  }
  saft_fbs::QuantizationType quantization_type = matrix->quantization_type();

  // Conversion from nlp_saft::saft_fbs::QuantizationType to
  // nlp_saft::QuantizationType (due to legacy reasons, we have both).
  switch (quantization_type) {
    case saft_fbs::QuantizationType_NONE:
      return QuantizationType::NONE;
    case saft_fbs::QuantizationType_UINT8:
      return QuantizationType::UINT8;
    case saft_fbs::QuantizationType_UINT4:
      return QuantizationType::UINT4;
    case saft_fbs::QuantizationType_FLOAT16:
      return QuantizationType::FLOAT16;
    default:
      SAFTM_LOG(ERROR) << "Unsupported quantization type "
                       << static_cast<int>(quantization_type);
      return QuantizationType::NONE;
  }
}

const void *EmbeddingNetworkParamsFromFlatbuffer::SafeGetValuesOfMatrix(
    const saft_fbs::Matrix *matrix) const {
  if (matrix == nullptr) {
    return nullptr;
  }
  saft_fbs::QuantizationType quantization_type = matrix->quantization_type();
  switch (quantization_type) {
    case saft_fbs::QuantizationType_NONE:
      return SafeGetValues(matrix);
    case saft_fbs::QuantizationType_UINT8:
      SAFTM_FALLTHROUGH_INTENDED;
    case saft_fbs::QuantizationType_UINT4:
      SAFTM_FALLTHROUGH_INTENDED;
    case saft_fbs::QuantizationType_FLOAT16:
      return SafeGetQuantizedValues(matrix);
    default:
      SAFTM_LOG(ERROR) << "Unsupported quantization type "
                       << static_cast<int>(quantization_type);
      return nullptr;
  }
}

}  // namespace mobile
}  // namespace nlp_saft
