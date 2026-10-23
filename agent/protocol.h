#pragma once

#include <string>

#include "gametest/game.h"

namespace gametest::agent {

// The result of handling one request.
struct Reply {
    std::string json;            // JSON text to send back (always a single line)
    bool quitRequested = false;  // true if the client asked the game to close
};

// Parses one request (a line of JSON), applies it to the game, and builds the
// reply. Call it on the game thread only: it reads and changes the Game.
//
// Requests            Replies (all contain "ok": true or false)
//   {"cmd":"ping"}                   {"ok":true,"reply":"pong"}
//   {"cmd":"key","key":"Right"}      {"ok":true}
//   {"cmd":"get_state"}              {"ok":true,"state":{...}}
//   {"cmd":"quit"}                   {"ok":true}  (and quitRequested is set)
// Anything invalid gives {"ok":false,"error":"..."}.
Reply handleRequest(const std::string& requestLine, gametest::Game& game);

}  // namespace gametest::agent