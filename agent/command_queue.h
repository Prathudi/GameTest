#pragma once

#include <cstddef>
#include <deque>
#include <exception>
#include <future>
#include <mutex>
#include <string>
#include <utility>

namespace gametest::agent {

// A thread-safe hand-off point between two threads:
//   - the network thread calls submit() with a request and gets a future back;
//   - the game thread calls drain() once per frame, which runs every waiting
//     request and delivers its reply through the future.
// Only the game thread ever touches the game, so there are no data races.
class CommandQueue {
public:
    // Network thread: queue a request. The future receives the reply later.
    std::future<std::string> submit(std::string request) {
        Job job;
        job.request = std::move(request);
        std::future<std::string> reply = job.reply.get_future();
        {
            std::lock_guard<std::mutex> lock(mutex_);
            jobs_.push_back(std::move(job));
        }
        return reply;
    }

    // Game thread: run `handler` (request -> reply text) for every waiting
    // request, in the order they were submitted. Returns how many were run.
    template <typename Handler>
    std::size_t drain(Handler&& handler) {
        std::deque<Job> batch;
        {
            std::lock_guard<std::mutex> lock(mutex_);
            batch.swap(jobs_);  // take everything, then release the lock quickly
        }
        for (Job& job : batch) {
            try {
                job.reply.set_value(handler(job.request));
            } catch (...) {
                job.reply.set_exception(std::current_exception());
            }
        }
        return batch.size();
    }

private:
    struct Job {
        std::string request;
        std::promise<std::string> reply;
    };

    std::mutex mutex_;
    std::deque<Job> jobs_;
};

}  // namespace gametest::agent