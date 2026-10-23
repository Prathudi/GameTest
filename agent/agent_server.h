#pragma once

#include <SFML/Network.hpp>

#include <atomic>
#include <thread>

#include "gametest/agent/command_queue.h"
#include "gametest/game.h"

namespace gametest::agent {

// Lets an outside program control the game over a local TCP connection.
//
// Two threads are involved:
//   - the network thread (owned by this class) accepts a client, reads one
//     JSON request per line, and waits for the reply;
//   - the game thread (your main loop) calls processPending() once per frame,
//     which is the ONLY place where requests touch the Game.
// The server only listens on 127.0.0.1, so it is reachable from this PC only.
class AgentServer {
public:
    AgentServer() = default;
    ~AgentServer();

    AgentServer(const AgentServer&) = delete;
    AgentServer& operator=(const AgentServer&) = delete;

    // Starts listening on 127.0.0.1:port (port 0 = let the OS pick a free one).
    // Returns false if it could not listen, for example when the port is taken.
    bool start(unsigned short port);

    // Stops the network thread and closes the connection. Safe to call twice.
    void stop();

    // The port actually in use (useful after start(0)).
    unsigned short port() const { return port_; }

    // Game thread, once per frame: runs every request that is waiting.
    // Returns true if a client asked the game to quit.
    bool processPending(gametest::Game& game);

private:
    void listenLoop();
    void serveClient(sf::TcpSocket& client);

    sf::TcpListener listener_;
    std::thread thread_;
    std::atomic<bool> running_{false};
    CommandQueue queue_;
    unsigned short port_ = 0;
};

}  // namespace gametest::agent