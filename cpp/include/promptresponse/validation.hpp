#pragma once

#include "models.hpp"
#include <string>
#include <vector>

namespace promptresponse {

enum class ValidationSeverity {
    Error,
    Warning
};

struct ValidationError {
    std::string field;
    std::string message;
    ValidationSeverity severity;

    ValidationError(std::string field, std::string message,
                   ValidationSeverity severity = ValidationSeverity::Error)
        : field(std::move(field)), message(std::move(message)), severity(severity) {}
};

class ValidationResult {
public:
    bool isValid;
    std::vector<ValidationError> errors;

    ValidationResult(bool valid = true) : isValid(valid) {}

    void addError(const std::string& field, const std::string& message,
                 ValidationSeverity severity = ValidationSeverity::Error) {
        errors.emplace_back(field, message, severity);
        if (severity == ValidationSeverity::Error) {
            isValid = false;
        }
    }

    operator bool() const { return isValid; }
};

class AprValidator {
public:
    // Validate document structure
    static ValidationResult validate(const AprDocument& document);

    // Validate for publishing (requires signatures)
    static ValidationResult validateForPublishing(const AprDocument& document);

private:
    static void validateSection(const Section& section, size_t index,
                               std::set<std::string>& sectionIds,
                               ValidationResult& result);
    static void validatePrompt(const Prompt& prompt, const std::string& prefix,
                              std::set<std::string>& promptIds,
                              ValidationResult& result);
};

} // namespace promptresponse
