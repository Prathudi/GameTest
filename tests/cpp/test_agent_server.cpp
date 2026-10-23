#include <gtest/gtest.h>

#include <SFML/Network.hpp>

#include <atomic>
#include <chrono>
#include <cstdint>
#include <nlohmann/json.hpp>
#include <optional>
#include <string>
#include <thread>
#include <vector>

#include "gametest/agent/agent_server.h"
#include "gametest/agent/line_buffer.h"
#include "gametest/game.h"

using gametest::Game;
using gametest::Scene;
using gametest::agent::AgentServer;
using gametest::agent::LineBuffer;
using nlohmann::json;

namespace {

// A tiny TCP client for the tests. Every wait has a time limit, so a broken
// server makes a test fail instead of hanging forever.
class TestClient {
public:
    bool connect(unsigned short port) {
        return socket_.connect(sf::IpAddress::LocalHost, port, sf::seconds(2)) ==
               sf::Socket::Status::Done;
    }

    bool send(const std::string& text) {
        return socket_.send(text.data(), text.size()) == sf::Socket::Status::Done;
    }

    std::optional<std::string> readLine(int timeoutMs = 3000) {
        const auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(timeoutMs);
        for (;;) {
            if (auto line = buffer_.nextLine()) return line;
            const auto left = std::chrono::duration_cast<std::chrono::milliseconds>(
                deadline - std::chrono::steady_clock::now());
            if (left.count() <= 0) return std::nullopt;

            sf::SocketSelector selector;
            selector.add(socket_);
            if (!selector.wait(sf::milliseconds(static_cast<std::int32_t>(left.count())))) {
                return std::nullopt;
            }
            char chunk[512];
            std::size_t received = 0;
            if (socket_.receive(chunk, sizeof(chunk), received) != sf::Socket::Status::Done) {
                return std::nullopt;
            }
            buffer_.append(chunk, received);
        }
    }

private:
    sf::TcpSocket socket_;
    LineBuffer buffer_;
};

// Plays the part of the game thread: keeps calling processPending() while
// `clientWork` runs on another thread. Returns true if a client asked to quit.
template <typename Fn>
bool pump(AgentServer& server, Game& game, Fn clientWork) {
    std::atomic<bool> done{false};
    bool quit = false;
    std::thread client([&] {
        clientWork();
        done = true;
    });
    while (!done) {
        if (server.processPending(game)) quit = true;
        std::this_thread::sleep_for(std::chrono::milliseconds(1));
    }
    client.join();
    return quit;
}

Game newGame() { return Game(Game::defaultLevel()); }

}  // namespace

TEST(AgentServer, AnswersPingOverTcp) {
    AgentServer server;
    ASSERT_TRUE(server.start(0));
    ASSERT_NE(server.port(), 0);
    Game game = newGame();

    bool connected = false;
    std::optional<std::string> reply;
    pump(server, game, [&] {
        TestClient client;
        connected = client.connect(server.port());
        if (connected) {
            client.send("{\"cmd\":\"ping\"}\n");
            reply = client.readLine();
        }
    });

    ASSERT_TRUE(connected);
    ASSERT_TRUE(reply.has_value());
    EXPECT_EQ(json::parse(*reply)["reply"].get<std::string>(), "pong");
}

TEST(AgentServer, KeyPressesChangeTheGameAndStateReportsIt) {
    AgentServer server;
    ASSERT_TRUE(server.start(0));
    Game game = newGame();

    std::vector<std::string> replies;
    pump(server, game, [&] {
        TestClient client;
        if (!client.connect(server.port())) return;
        for (const char* request : {"{\"cmd\":\"key\",\"key\":\"Enter\"}\n",
                                    "{\"cmd\":\"key\",\"key\":\"Right\"}\n",
                                    "{\"cmd\":\"get_state\"}\n"}) {
            client.send(request);
            if (const auto line = client.readLine()) replies.push_back(*line);
        }
    });

    ASSERT_EQ(replies.size(), 3u);
    EXPECT_EQ(game.scene(), Scene::Playing);
    const json state = json::parse(replies[2])["state"];
    EXPECT_EQ(state["scene"].get<std::string>(), "playing");
    EXPECT_EQ(state["player"]["x"].get<int>(), 2);
}

TEST(AgentServer, ReassemblesARequestSplitAcrossTwoPackets) {
    AgentServer server;
    ASSERT_TRUE(server.start(0));
    Game game = newGame();

    std::optional<std::string> reply;
    pump(server, game, [&] {
        TestClient client;
        if (!client.connect(server.port())) return;
        client.send("{\"cmd\":\"pi");
        std::this_thread::sleep_for(std::chrono::milliseconds(150));
        client.send("ng\"}\n");
        reply = client.readLine();
    });

    ASSERT_TRUE(reply.has_value());
    EXPECT_EQ(json::parse(*reply)["reply"].get<std::string>(), "pong");
}

TEST(AgentServer, AnswersTwoRequestsSentInOnePacketInOrder) {
    AgentServer server;
    ASSERT_TRUE(server.start(0));
    Game game = newGame();

    std::optional<std::string> first;
    std::optional<std::string> second;
    pump(server, game, [&] {
        TestClient client;
        if (!client.connect(server.port())) return;
        client.send("{\"cmd\":\"ping\"}\n{\"cmd\":\"get_state\"}\n");
        first = client.readLine();
        second = client.readLine();
    });

    ASSERT_TRUE(first.has_value());
    ASSERT_TRUE(second.has_value());
    EXPECT_EQ(json::parse(*first)["reply"].get<std::string>(), "pong");
    EXPECT_TRUE(json::parse(*second).contains("state"));
}

TEST(AgentServer, ConnectionSurvivesAnInvalidRequest) {
    AgentServer server;
    ASSERT_TRUE(server.start(0));
    Game game = newGame();

    std::optional<std::string> bad;
    std::optional<std::string> good;
    pump(server, game, [&] {
        TestClient client;
        if (!client.connect(server.port())) return;
        client.send("garbage\n");
        bad = client.readLine();
        client.send("{\"cmd\":\"ping\"}\r\n");  // Windows-style line ending
        good = client.readLine();
    });

    ASSERT_TRUE(bad.has_value());
    ASSERT_TRUE(good.has_value());
    EXPECT_FALSE(json::parse(*bad)["ok"].get<bool>());
    EXPECT_TRUE(json::parse(*good)["ok"].get<bool>());
}

TEST(AgentServer, QuitCommandSetsTheQuitFlag) {
    AgentServer server;
    ASSERT_TRUE(server.start(0));
    Game game = newGame();

    std::optional<std::string> reply;
    const bool quit = pump(server, game, [&] {
        TestClient client;
        if (!client.connect(server.port())) return;
        client.send("{\"cmd\":\"quit\"}\n");
        reply = client.readLine();
    });

    EXPECT_TRUE(quit);
    ASSERT_TRUE(reply.has_value());
    EXPECT_TRUE(json::parse(*reply)["ok"].get<bool>());
}

TEST(AgentServer, AcceptsASecondClientAfterTheFirstDisconnects) {
    AgentServer server;
    ASSERT_TRUE(server.start(0));
    Game game = newGame();

    std::optional<std::string> firstReply;
    std::optional<std::string> secondReply;
    pump(server, game, [&] {
        {
            TestClient first;
            if (!first.connect(server.port())) return;
            first.send("{\"cmd\":\"ping\"}\n");
            firstReply = first.readLine();
        }  // the first client disconnects here
        TestClient second;
        if (!second.connect(server.port())) return;
        second.send("{\"cmd\":\"ping\"}\n");
        secondReply = second.readLine();
    });

    EXPECT_TRUE(firstReply.has_value());
    EXPECT_TRUE(secondReply.has_value());
}

TEST(AgentServer, StartFailsWhenThePortIsAlreadyTaken) {
    AgentServer a;
    ASSERT_TRUE(a.start(0));
    AgentServer b;
    EXPECT_FALSE(b.start(a.port()));
}

TEST(AgentServer, StopsPromptlyEvenWithAnIdleClientConnected) {
    AgentServer server;
    ASSERT_TRUE(server.start(0));

    TestClient client;
    ASSERT_TRUE(client.connect(server.port()));
    std::this_thread::sleep_for(std::chrono::milliseconds(200));  // let the server accept it

    const auto start = std::chrono::steady_clock::now();
    server.stop();
    const auto elapsed = std::chrono::steady_clock::now() - start;
    EXPECT_LT(std::chrono::duration_cast<std::chrono::milliseconds>(elapsed).count(), 2000);
}