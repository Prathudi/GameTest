#include "gametest/agent/agent_server.h"

#include <chrono>
#include <exception>
#include <future>
#include <string>

#include "gametest/agent/line_buffer.h"
#include "gametest/agent/protocol.h"

namespace gametest::agent {

namespace {

constexpr int kReplyTimeoutMs = 5000;           // how long a client waits for the game
constexpr std::size_t kMaxLineBytes = 64 * 1024;  // a request longer than this is nonsense

// Waits for the game thread to answer, in short slices so that stop() is never
// stuck behind a game that has stopped answering.
std::string waitForReply(std::future<std::string> reply, const std::atomic<bool>& running) {
    for (int waited = 0; waited < kReplyTimeoutMs && running; waited += 20) {
        if (reply.wait_for(std::chrono::milliseconds(20)) == std::future_status::ready) {
            try {
                return reply.get();
            } catch (const std::exception& e) {
                return std::string(R"({"ok":false,"error":"internal error: )") + e.what() + "\"}";
            }
        }
    }
    return R"({"ok":false,"error":"the game did not answer in time"})";
}

}  // namespace

AgentServer::~AgentServer() { stop(); }

bool AgentServer::start(unsigned short port) {
    if (running_) return false;
    // Listening on LocalHost (127.0.0.1) keeps the agent private to this PC.
    if (listener_.listen(port, sf::IpAddress::LocalHost) != sf::Socket::Status::Done) {
        return false;
    }
    port_ = listener_.getLocalPort();
    running_ = true;
    thread_ = std::thread([this] { listenLoop(); });
    return true;
}

void AgentServer::stop() {
    running_ = false;
    if (thread_.joinable()) thread_.join();
    listener_.close();
}

bool AgentServer::processPending(Game& game) {
    bool quit = false;
    queue_.drain([&](const std::string& request) {
        const Reply reply = handleRequest(request, game);
        if (reply.quitRequested) quit = true;
        return reply.json;
    });
    return quit;
}

// Network thread: wait for a client, serve it until it disconnects, repeat.
void AgentServer::listenLoop() {
    sf::SocketSelector selector;
    selector.add(listener_);
    while (running_) {
        if (!selector.wait(sf::milliseconds(100))) continue;  // timeout: re-check running_
        sf::TcpSocket client;
        if (listener_.accept(client) == sf::Socket::Status::Done) serveClient(client);
    }
}

// Network thread: read request lines from one client and answer each in order.
void AgentServer::serveClient(sf::TcpSocket& client) {
    sf::SocketSelector selector;
    selector.add(client);
    LineBuffer buffer;
    char chunk[1024];

    while (running_) {
        if (!selector.wait(sf::milliseconds(100))) continue;  // nothing arrived yet

        std::size_t received = 0;
        const sf::Socket::Status status = client.receive(chunk, sizeof(chunk), received);
        if (status == sf::Socket::Status::Disconnected || status == sf::Socket::Status::Error) {
            return;
        }
        if (status != sf::Socket::Status::Done) continue;

        buffer.append(chunk, received);
        if (buffer.pending() > kMaxLineBytes) return;  // no line ending in sight: drop the client

        while (const std::optional<std::string> line = buffer.nextLine()) {
            if (line->empty()) continue;  // ignore blank lines
            const std::string reply = waitForReply(queue_.submit(*line), running_) + "\n";
            if (client.send(reply.data(), reply.size()) != sf::Socket::Status::Done) return;
        }
    }
}

}  // namespace gametest::agent