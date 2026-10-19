// Removes the temp files the launcher leaves behind when it's killed or a job is cancelled. Run it at startup, before any job can be
// running, so no file in use is removed.

#pragma once

namespace gwl {

void removeLeftoverTempFiles();

}  // namespace gwl
