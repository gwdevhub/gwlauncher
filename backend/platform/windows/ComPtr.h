#pragma once

#include <unknwn.h>
#include <memory>

namespace gwl {

struct ReleaseCom {
    void operator()(IUnknown* object) const { object->Release(); }
};

// Adopts an owned reference; borrowed pointers must be AddRef'd before storing them here.
template <typename Interface>
using ComPtr = std::unique_ptr<Interface, ReleaseCom>;

}  // namespace gwl
