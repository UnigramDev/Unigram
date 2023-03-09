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

// Notes on thread-safety: All of the classes here are thread-compatible.  More
// specifically, the registry machinery is thread-safe, as long as each thread
// performs feature extraction on a different Sentence object.

#ifndef NLP_SAFT_COMPONENTS_COMMON_MOBILE_FEL_WORKSPACE_H_
#define NLP_SAFT_COMPONENTS_COMMON_MOBILE_FEL_WORKSPACE_H_

#include <stddef.h>

#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

#include "lang_id/common/lite_base/logging.h"
#include "lang_id/common/lite_base/macros.h"

namespace libtextclassifier3 {
namespace mobile {

// A base class for shared workspaces. Derived classes implement a static member
// function TypeName() which returns a human readable string name for the class.
class Workspace {
 public:
  // Polymorphic destructor.
  virtual ~Workspace() {}

 protected:
  // Create an empty workspace.
  Workspace() {}

 private:
  SAFTM_DISALLOW_COPY_AND_ASSIGN(Workspace);
};

// Returns a new, strictly increasing int every time it is invoked.
int GetFreshTypeId();

// Struct to simulate typeid, but without RTTI.
template <typename T>
struct TypeId {
  static int type_id;
};

template <typename T>
int TypeId<T>::type_id = GetFreshTypeId();

// A registry that keeps track of workspaces.
class WorkspaceRegistry {
 public:
  // Create an empty registry.
  WorkspaceRegistry() {}

  // Returns the index of a named workspace, adding it to the registry first
  // if necessary.
  template <class W>
  int Request(const std::string &name) {
    const int id = TypeId<W>::type_id;
    max_workspace_id_ = std::max(id, max_workspace_id_);
    workspace_types_[id] = W::TypeName();
    std::vector<std::string> &names = workspace_names_[id];
    for (int i = 0; i < names.size(); ++i) {
      if (names[i] == name) return i;
    }
    names.push_back(name);
    return names.size() - 1;
  }

  // Returns the maximum workspace id that has been registered.
  int MaxId() const {
    return max_workspace_id_;
  }

  const std::unordered_map<int, std::vector<std::string> > &WorkspaceNames()
      const {
    return workspace_names_;
  }

  // Returns a string describing the registered workspaces.
  std::string DebugString() const;

 private:
  // Workspace type names, indexed as workspace_types_[typeid].
  std::unordered_map<int, std::string> workspace_types_;

  // Workspace names, indexed as workspace_names_[typeid][workspace].
  std::unordered_map<int, std::vector<std::string> > workspace_names_;

  // The maximum workspace id that has been registered.
  int max_workspace_id_ = 0;

  SAFTM_DISALLOW_COPY_AND_ASSIGN(WorkspaceRegistry);
};

// A typed collected of workspaces. The workspaces are indexed according to an
// external WorkspaceRegistry. If the WorkspaceSet is const, the contents are
// also immutable.
class WorkspaceSet {
 public:
  ~WorkspaceSet() { Reset(WorkspaceRegistry()); }

  // Returns true if a workspace has been set.
  template <class W>
  bool Has(int index) const {
    const int id = TypeId<W>::type_id;
    SAFTM_DCHECK_GE(id, 0);
    SAFTM_DCHECK_LT(id, workspaces_.size());
    SAFTM_DCHECK_GE(index, 0);
    SAFTM_DCHECK_LT(index, workspaces_[id].size());
    if (id >= workspaces_.size()) return false;
    return workspaces_[id][index] != nullptr;
  }

  // Returns an indexed workspace; the workspace must have been set.
  template <class W>
  const W &Get(int index) const {
    SAFTM_DCHECK(Has<W>(index));
    const int id = TypeId<W>::type_id;
    const Workspace *w = workspaces_[id][index];
    return reinterpret_cast<const W &>(*w);
  }

  // Sets an indexed workspace; this takes ownership of the workspace, which
  // must have been new-allocated.  It is an error to set a workspace twice.
  template <class W>
  void Set(int index, W *workspace) {
    const int id = TypeId<W>::type_id;
    SAFTM_DCHECK_GE(id, 0);
    SAFTM_DCHECK_LT(id, workspaces_.size());
    SAFTM_DCHECK_GE(index, 0);
    SAFTM_DCHECK_LT(index, workspaces_[id].size());
    SAFTM_DCHECK(workspaces_[id][index] == nullptr);
    SAFTM_DCHECK(workspace != nullptr);
    workspaces_[id][index] = workspace;
  }

  void Reset(const WorkspaceRegistry &registry) {
    // Deallocate current workspaces.
    for (auto &it : workspaces_) {
      for (size_t index = 0; index < it.size(); ++index) {
        delete it[index];
      }
    }
    workspaces_.clear();
    workspaces_.resize(registry.MaxId() + 1, std::vector<Workspace *>());
    for (auto &it : registry.WorkspaceNames()) {
      workspaces_[it.first].resize(it.second.size());
    }
  }

 private:
  // The set of workspaces, indexed as workspaces_[typeid][index].
  std::vector<std::vector<Workspace *> > workspaces_;
};

// A workspace that wraps around a vector of int.
class VectorIntWorkspace : public Workspace {
 public:
  // Creates a vector of the given size.
  explicit VectorIntWorkspace(int size);

  // Creates a vector initialized with the given array.
  explicit VectorIntWorkspace(const std::vector<int> &elements);

  // Creates a vector of the given size, with each element initialized to the
  // given value.
  VectorIntWorkspace(int size, int value);

  // Returns the name of this type of workspace.
  static std::string TypeName();

  // Returns the i'th element.
  int element(int i) const { return elements_[i]; }

  // Sets the i'th element.
  void set_element(int i, int value) { elements_[i] = value; }

  // Returns the size of the underlying vector.
  int size() const { return elements_.size(); }

 private:
  // The enclosed vector.
  std::vector<int> elements_;
};

}  // namespace mobile
}  // namespace nlp_saft

#endif  // NLP_SAFT_COMPONENTS_COMMON_MOBILE_FEL_WORKSPACE_H_
