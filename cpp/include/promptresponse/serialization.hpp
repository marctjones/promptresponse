#pragma once

#include "models.hpp"
#include <string>
#include <stdexcept>

namespace promptresponse {

class AprSerializer {
public:
    // Serialize document to JSON string
    static std::string serialize(const AprDocument& document, int indent = 2);

    // Deserialize document from JSON string
    static AprDocument deserialize(const std::string& json);

    // Load from file
    static AprDocument loadFile(const std::string& filePath);

    // Save to file
    static void saveFile(const AprDocument& document, const std::string& filePath, int indent = 2);
};

class SerializationException : public std::runtime_error {
public:
    explicit SerializationException(const std::string& message)
        : std::runtime_error(message) {}
};

} // namespace promptresponse
