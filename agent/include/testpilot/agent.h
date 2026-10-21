#pragma once

// TestPilot agent: embedded in the game, lets external tests query state and
// send input. The real implementation (TCP server, state registry, command
// queue) is built in Phase 2. For now this only proves the library links.

namespace testpilot {

const char* version();

}  // namespace testpilot
