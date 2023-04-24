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

#include "lang_id/common/flatbuffers/model-utils.h"

#include <string.h>

#include <string>

#include "lang_id/common/lite_base/logging.h"
#include "lang_id/common/math/checksum.h"

namespace libtextclassifier3 {
namespace saft_fbs {

bool ClearlyFailsChecksum(const Model &model) {
  if (!flatbuffers::IsFieldPresent(&model, Model::VT_CRC32)) {
    SAFTM_LOG(WARNING)
        << "No CRC32, most likely an old model; skip CRC32 check";
    return false;
  }
  const mobile::uint32 expected_crc32 = model.crc32();
  const mobile::uint32 actual_crc32 = ComputeCrc2Checksum(&model);
  if (actual_crc32 != expected_crc32) {
    SAFTM_LOG(ERROR) << "Corrupt model: different CRC32: " << actual_crc32
                     << " vs " << expected_crc32;
    return true;
  }
  SAFTM_DLOG(INFO) << "Successfully checked CRC32 " << actual_crc32;
  return false;
}

const Model *GetVerifiedModelFromBytes(const char *data, size_t num_bytes) {
  if ((data == nullptr) || (num_bytes == 0)) {
    SAFTM_LOG(ERROR) << "GetModel called on an empty sequence of bytes";
    return nullptr;
  }
  const uint8_t *start = reinterpret_cast<const uint8_t *>(data);
  flatbuffers::Verifier verifier(start, num_bytes);
  if (!VerifyModelBuffer(verifier)) {
    SAFTM_LOG(ERROR) << "Not a valid Model flatbuffer";
    return nullptr;
  }
  const Model *model = GetModel(start);
  if (model == nullptr) {
    return nullptr;
  }
  if (ClearlyFailsChecksum(*model)) {
    return nullptr;
  }
  return model;
}

const ModelInput *GetInputByName(const Model *model, const std::string &name) {
  if (model == nullptr) {
    SAFTM_LOG(ERROR) << "GetInputByName called with model == nullptr";
    return nullptr;
  }
  const auto *inputs = model->inputs();
  if (inputs == nullptr) {
    // We should always have a list of inputs; maybe an empty one, if no inputs,
    // but the list should be there.
    SAFTM_LOG(ERROR) << "null inputs";
    return nullptr;
  }
  for (const ModelInput *input : *inputs) {
    if (input != nullptr) {
      const flatbuffers::String *input_name = input->name();
      if (input_name && input_name->str() == name) {
        return input;
      }
    }
  }
  return nullptr;
}

mobile::StringPiece GetInputBytes(const ModelInput *input) {
  if ((input == nullptr) || (input->data() == nullptr)) {
    SAFTM_LOG(ERROR) << "ModelInput has no content";
    return mobile::StringPiece(nullptr, 0);
  }
  const flatbuffers::Vector<uint8_t> *input_data = input->data();
  if (input_data == nullptr) {
    SAFTM_LOG(ERROR) << "null input data";
    return mobile::StringPiece(nullptr, 0);
  }
  return mobile::StringPiece(reinterpret_cast<const char *>(input_data->data()),
                             input_data->size());
}

bool FillParameters(const Model &model, mobile::TaskContext *context) {
  if (context == nullptr) {
    SAFTM_LOG(ERROR) << "null context";
    return false;
  }
  const auto *parameters = model.parameters();
  if (parameters == nullptr) {
    // We should always have a list of parameters; maybe an empty one, if no
    // parameters, but the list should be there.
    SAFTM_LOG(ERROR) << "null list of parameters";
    return false;
  }
  for (const ModelParameter *p : *parameters) {
    if (p == nullptr) {
      SAFTM_LOG(ERROR) << "null parameter";
      return false;
    }
    if (p->name() == nullptr) {
      SAFTM_LOG(ERROR) << "null parameter name";
      return false;
    }
    const std::string name = p->name()->str();
    if (name.empty()) {
      SAFTM_LOG(ERROR) << "empty parameter name";
      return false;
    }
    if (p->value() == nullptr) {
      SAFTM_LOG(ERROR) << "null parameter name";
      return false;
    }
    context->SetParameter(name, p->value()->str());
  }
  return true;
}

namespace {
// Updates |*crc| with the information from |s|.  Auxiliary for
// ComputeCrc2Checksum.
//
// The bytes from |info| are also used to update the CRC32 checksum.  |info|
// should be a brief tag that indicates what |s| represents.  The idea is to add
// some structure to the information that goes into the CRC32 computation.
template <typename T>
void UpdateCrc(mobile::Crc32 *crc, const flatbuffers::Vector<T> *s,
               mobile::StringPiece info) {
  crc->Update("|");
  crc->Update(info.data(), info.size());
  crc->Update(":");
  if (s == nullptr) {
    crc->Update("empty");
  } else {
    crc->Update(reinterpret_cast<const char *>(s->data()),
                s->size() * sizeof(T));
  }
}
}  // namespace

mobile::uint32 ComputeCrc2Checksum(const Model *model) {
  // Implementation note: originally, I (salcianu@) thought we can just compute
  // a CRC32 checksum of the model bytes.  Unfortunately, the expected checksum
  // is there too (and because we don't control the flatbuffer format, we can't
  // "arrange" for it to be placed at the head / tail of those bytes).  Instead,
  // we traverse |model| and feed into the CRC32 computation those parts we are
  // interested in (which excludes the crc32 field).
  //
  // Note: storing the checksum outside the Model would be too disruptive for
  // the way we currently ship our models.
  mobile::Crc32 crc;
  if (model == nullptr) {
    return crc.Get();
  }
  crc.Update("|Parameters:");
  const auto *parameters = model->parameters();
  if (parameters != nullptr) {
    for (const ModelParameter *p : *parameters) {
      if (p != nullptr) {
        UpdateCrc(&crc, p->name(), "name");
        UpdateCrc(&crc, p->value(), "value");
      }
    }
  }
  crc.Update("|Inputs:");
  const auto *inputs = model->inputs();
  if (inputs != nullptr) {
    for (const ModelInput *input : *inputs) {
      if (input != nullptr) {
        UpdateCrc(&crc, input->name(), "name");
        UpdateCrc(&crc, input->type(), "type");
        UpdateCrc(&crc, input->sub_type(), "sub-type");
        UpdateCrc(&crc, input->data(), "data");
      }
    }
  }
  return crc.Get();
}

}  // namespace saft_fbs
}  // namespace nlp_saft
