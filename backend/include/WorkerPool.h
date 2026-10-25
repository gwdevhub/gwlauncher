// A fixed pool of worker threads. Anything in the launcher can queue a task on it, and a task can pass its result on to a
// callback. The callback runs on the worker that finished the task, so it must not touch a window: its window work goes
// back to the UI thread with runOnUiThread.

#pragma once

#include <condition_variable>
#include <cstddef>
#include <deque>
#include <functional>
#include <mutex>
#include <thread>
#include <utility>
#include <vector>

namespace gwl {

// The number of workers the launcher runs.
inline constexpr std::size_t kWorkerThreads = 20;

class WorkerPool {
public:
    explicit WorkerPool(std::size_t threads = kWorkerThreads);
    // Runs the tasks already queued, then stops the workers.
    ~WorkerPool();

    WorkerPool(const WorkerPool&) = delete;
    WorkerPool& operator=(const WorkerPool&) = delete;

    // Tasks handle recoverable failures themselves; an uncaught exception reports a fatal error on the UI thread.
    void submit(std::function<void()> task);

    // Queues work, then passes its result to done on the same worker. work must not throw: a failure belongs in its result.
    template <typename Work, typename Done>
    void submit(Work work, Done done) {
        submit([work = std::move(work), done = std::move(done)]() mutable { done(work()); });
    }

private:
    void runWorker();

    // mutex_ guards queue_ and stopping_. Nothing else is shared between the workers.
    std::mutex mutex_;
    std::condition_variable wake_;
    std::deque<std::function<void()>> queue_;
    bool stopping_ = false;
    std::vector<std::thread> threads_;
};

}  // namespace gwl
