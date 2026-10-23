#pragma once

#include <cstddef>
#include <optional>
#include <string>

namespace gametest::agent {

// TCP delivers a stream of bytes, not messages: one request may arrive split
// across several chunks, or several requests may arrive in one chunk.
// LineBuffer collects incoming bytes and hands back complete lines.
class LineBuffer {
public:
    // Add bytes that just arrived from the network.
    void append(const char* data, std::size_t size) { buffer_.append(data, size); }

    // The next complete line (without its line ending), or nothing if no
    // complete line has arrived yet. Both "\n" and "\r\n" end a line.
    std::optional<std::string> nextLine() {
        const std::size_t end = buffer_.find('\n');
        if (end == std::string::npos) return std::nullopt;
        std::string line = buffer_.substr(0, end);
        buffer_.erase(0, end + 1);
        if (!line.empty() && line.back() == '\r') line.pop_back();
        return line;
    }

    // How many bytes are waiting for a line ending.
    std::size_t pending() const { return buffer_.size(); }

private:
    std::string buffer_;
};

}  // namespace gametest::agent