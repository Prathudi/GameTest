#include <gtest/gtest.h>

#include <atomic>
#include <chrono>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>

#include "gametest/agent/command_queue.h"

using gametest::agent::CommandQueue;

TEST(CommandQueue, RunsRequestsInOrderAndDeliversReplies) {
    CommandQueue queue;
    auto a = queue.submit("first");
    auto b = queue.submit("second");
    auto c = queue.submit("third");

    std::vector<std::string> handled;
    const std::size_t count = queue.drain([&](const std::string& request) {
        handled.push_back(request);
        return "echo:" + request;
    });

    EXPECT_EQ(count, 3u);
    EXPECT_EQ(handled, (std::vector<std::string>{"first", "second", "third"}));
    EXPECT_EQ(a.get(), "echo:first");
    EXPECT_EQ(b.get(), "echo:second");
    EXPECT_EQ(c.get(), "echo:third");
}

TEST(CommandQueue, DrainingAnEmptyQueueDoesNothing) {
    CommandQueue queue;
    bool called = false;
    EXPECT_EQ(queue.drain([&](const std::string&) { called = true; return std::string(); }), 0u);
    EXPECT_FALSE(called);
}

TEST(CommandQueue, ARequestIsOnlyRunOnce) {
    CommandQueue queue;
    auto reply = queue.submit("once");
    int calls = 0;
    auto handler = [&](const std::string&) { ++calls; return std::string("done"); };
    queue.drain(handler);
    queue.drain(handler);
    EXPECT_EQ(calls, 1);
    EXPECT_EQ(reply.get(), "done");
}

TEST(CommandQueue, HandlerExceptionReachesTheWaitingThread) {
    CommandQueue queue;
    auto reply = queue.submit("boom");
    queue.drain([](const std::string&) -> std::string { throw std::runtime_error("handler failed"); });
    EXPECT_THROW(reply.get(), std::runtime_error);
}

TEST(CommandQueue, WorksAcrossThreads) {
    CommandQueue queue;
    constexpr int kRequests = 200;
    std::atomic<bool> finished{false};
    int wrongReplies = 0;  // written by the worker, read after join()

    // The "network thread": submits a request, then waits for its reply.
    std::thread worker([&] {
        for (int i = 0; i < kRequests; ++i) {
            const std::string request = "req" + std::to_string(i);
            auto reply = queue.submit(request);
            if (reply.wait_for(std::chrono::seconds(5)) != std::future_status::ready ||
                reply.get() != "ok:" + request) {
                ++wrongReplies;
            }
        }
        finished = true;
    });

    // The "game thread": drains the queue over and over, like once per frame.
    int handledCount = 0;
    while (!finished) {
        queue.drain([&](const std::string& request) {
            ++handledCount;
            return "ok:" + request;
        });
        std::this_thread::sleep_for(std::chrono::milliseconds(1));
    }
    worker.join();

    EXPECT_EQ(wrongReplies, 0);
    EXPECT_EQ(handledCount, kRequests);
}