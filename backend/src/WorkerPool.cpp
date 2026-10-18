#include "WorkerPool.h"

#include <cstdio>
#include <exception>

namespace gwl {

WorkerPool::WorkerPool(std::size_t threads) {
    threads_.reserve(threads);
    for (std::size_t i = 0; i < threads; ++i) {
        threads_.emplace_back([this] { runWorker(); });
    }
}

WorkerPool::~WorkerPool() {
    {
        std::lock_guard<std::mutex> lock(mutex_);
        stopping_ = true;
    }
    wake_.notify_all();
    for (std::thread& thread : threads_) {
        thread.join();
    }
}

void WorkerPool::submit(std::function<void()> task) {
    {
        std::lock_guard<std::mutex> lock(mutex_);
        queue_.push_back(std::move(task));
    }
    wake_.notify_one();
}

void WorkerPool::runWorker() {
    for (;;) {
        std::function<void()> task;
        {
            std::unique_lock<std::mutex> lock(mutex_);
            wake_.wait(lock, [this] { return stopping_ || !queue_.empty(); });
            if (queue_.empty()) {
                return;
            }
            task = std::move(queue_.front());
            queue_.pop_front();
        }
        // The task runs with no lock held, so a slow one never holds up the queue.
        try {
            task();
        } catch (const std::exception& error) {
            std::fprintf(stderr, "Worker task failed: %s\n", error.what());
        } catch (...) {
            std::fprintf(stderr, "Worker task failed.\n");
        }
    }
}

}  // namespace gwl
