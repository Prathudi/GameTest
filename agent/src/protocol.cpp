#include "gametest/agent/protocol.h"

#include <nlohmann/json.hpp>

#include <optional>

namespace gametest::agent {

namespace {

using json = nlohmann::json;

Reply makeError(const std::string& message) {
    json reply;
    reply["ok"] = false;
    reply["error"] = message;
    return Reply{reply.dump(), false};
}

Reply makeOk(json reply = json::object()) {
    reply["ok"] = true;
    return Reply{reply.dump(), false};
}

std::optional<Key> parseKey(const std::string& name) {
    if (name == "Up") return Key::Up;
    if (name == "Down") return Key::Down;
    if (name == "Left") return Key::Left;
    if (name == "Right") return Key::Right;
    if (name == "Enter") return Key::Enter;
    if (name == "Escape") return Key::Escape;
    return std::nullopt;
}

const char* sceneName(Scene scene) {
    switch (scene) {
        case Scene::MainMenu: return "main_menu";
        case Scene::Playing: return "playing";
        case Scene::Paused: return "paused";
    }
    return "unknown";
}

json describeState(const Game& game) {
    json state;
    state["scene"] = sceneName(game.scene());
    state["player"]["x"] = game.player().x;
    state["player"]["y"] = game.player().y;
    state["score"] = game.score();
    state["coins_remaining"] = game.coinsRemaining();
    state["width"] = game.width();
    state["height"] = game.height();
    return state;
}

}  // namespace

Reply handleRequest(const std::string& requestLine, Game& game) {
    json request;
    try {
        request = json::parse(requestLine);
    } catch (const json::parse_error& e) {
        return makeError(std::string("invalid json: ") + e.what());
    }

    if (!request.is_object() || !request.contains("cmd") || !request["cmd"].is_string()) {
        return makeError("request must be a JSON object with a string field \"cmd\"");
    }
    const std::string cmd = request["cmd"].get<std::string>();

    if (cmd == "ping") {
        json reply;
        reply["reply"] = "pong";
        return makeOk(reply);
    }

    if (cmd == "key") {
        if (!request.contains("key") || !request["key"].is_string()) {
            return makeError("the \"key\" command needs a string field \"key\"");
        }
        const std::string name = request["key"].get<std::string>();
        const std::optional<Key> key = parseKey(name);
        if (!key) return makeError("unknown key: " + name);
        game.handleKey(*key);
        return makeOk();
    }

    if (cmd == "get_state") {
        json reply;
        reply["state"] = describeState(game);
        return makeOk(reply);
    }

    if (cmd == "quit") {
        Reply reply = makeOk();
        reply.quitRequested = true;
        return reply;
    }

    return makeError("unknown command: " + cmd);
}

}  // namespace gametest::agent