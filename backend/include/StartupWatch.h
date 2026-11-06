// Windows only: a watchdog for the launcher's startup. It's started first, and says where a startup that hasn't finished is stuck.

#pragma once

namespace gwl {

// Starts the watchdog. A startup that hasn't finished within 30 seconds shows the step it's on and exits.
void startStartupMonitoring();
// Names the step the startup is on, for the watchdog's message.
void monitorConfigurationStartup();
void finishStartupMonitoring();
// A dialog pauses the count, since the user may be answering it. Calls nest.
void pauseStartupMonitoring(bool paused);

}  // namespace gwl
