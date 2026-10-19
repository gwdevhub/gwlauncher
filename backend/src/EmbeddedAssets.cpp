#include "EmbeddedAssets.h"

#include "utils/Images.h"
#include "utils/Strings.h"

namespace gwl {

std::string embeddedPage() {
    return textOf(gzipInflate(bytesOf(embeddedPageGzip())));
}

}  // namespace gwl
