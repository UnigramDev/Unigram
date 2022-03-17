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

#include "lang_id/common/file/file-utils.h"

#include <fcntl.h>
#include <stdio.h>
#include <sys/stat.h>
#include <sys/types.h>

#include <string>

#include "lang_id/common/lite_base/logging.h"
#include "lang_id/common/lite_strings/stringpiece.h"

namespace libtextclassifier3 {
namespace mobile {

namespace file_utils {

bool GetFileContent(const std::string &filename, std::string *content) {
  ScopedMmap scoped_mmap(filename);
  const MmapHandle &handle = scoped_mmap.handle();
  if (!handle.ok()) {
    SAFTM_LOG(ERROR) << "Error opening " << filename;
    return false;
  }
  StringPiece sp = handle.to_stringpiece();
  content->assign(sp.data(), sp.size());
  return true;
}

bool FileExists(const std::string &filename) {
  struct stat s = {0};
  if (!stat(filename.c_str(), &s)) {
    return s.st_mode & S_IFREG;
  } else {
    return false;
  }
}

bool DirectoryExists(const std::string &dirpath) {
  struct stat s = {0};
  if (!stat(dirpath.c_str(), &s)) {
    return s.st_mode & S_IFDIR;
  } else {
    return false;
  }
}

}  // namespace file_utils

}  // namespace mobile
}  // namespace nlp_saft
