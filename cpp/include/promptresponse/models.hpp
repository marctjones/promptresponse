#pragma once

#include <string>
#include <vector>
#include <optional>
#include <memory>
#include <chrono>
#include <map>

namespace promptresponse {

enum class DocumentType {
    Template,
    FilledForm
};

struct PromptHints {
    std::optional<std::string> expectedDataType;
    std::optional<std::string> placeholder;
    std::optional<std::string> helpText;
    std::vector<std::string> suggestedValues;
    std::optional<int> minLength;
    std::optional<int> maxLength;
    std::optional<std::string> pattern;
};

struct ResponseMetadata {
    std::optional<std::chrono::system_clock::time_point> lastModified;
};

struct Prompt {
    std::string id;
    std::string label;
    std::string response;
    std::optional<PromptHints> hints;
    ResponseMetadata responseMetadata;

    Prompt() = default;
    Prompt(std::string id, std::string label)
        : id(std::move(id)), label(std::move(label)), response("") {}
};

struct Section {
    std::string id;
    std::string title;
    std::optional<std::string> description;
    std::vector<Prompt> prompts;
    std::vector<Section> sections;

    Section() = default;
    Section(std::string id, std::string title)
        : id(std::move(id)), title(std::move(title)) {}

    // Get all prompts in this section and nested sections (flattened)
    std::vector<Prompt*> getAllPrompts();
    std::vector<const Prompt*> getAllPrompts() const;
};

struct DigitalSignature {
    std::string signerName;
    std::string signerEmail;
    std::string signatureAlgorithm;
    std::string signatureValue;
    std::string certificate;
    std::chrono::system_clock::time_point signedDate;
    std::string templateHash;
};

struct SubmissionConfig {
    std::string type;
    std::string url;
    std::map<std::string, std::string> fields;
    std::optional<std::chrono::system_clock::time_point> expiresAt;
    std::optional<std::map<std::string, std::string>> headers;

    bool isExpired() const;
};

struct Metadata {
    std::optional<std::string> title;
    std::optional<std::string> description;
    std::optional<std::string> author;
    std::optional<std::chrono::system_clock::time_point> created;
    std::optional<std::chrono::system_clock::time_point> modified;
    std::optional<std::string> templateId;
    std::optional<std::string> templateVersion;
    std::optional<std::string> filledBy;
    std::optional<std::chrono::system_clock::time_point> filledDate;
    std::vector<DigitalSignature> templateSignatures;
    std::optional<SubmissionConfig> submissionConfig;
};

class AprDocument {
public:
    std::string version;
    DocumentType documentType;
    std::vector<Section> sections;
    std::optional<Metadata> metadata;

    AprDocument() : version("1.0"), documentType(DocumentType::Template) {}

    // Get all prompts (flattened)
    std::vector<Prompt*> getAllPrompts();
    std::vector<const Prompt*> getAllPrompts() const;

    // Find prompt by ID
    Prompt* getPromptById(const std::string& promptId);
    const Prompt* getPromptById(const std::string& promptId) const;

    // Calculate completion percentage
    double getCompletionPercentage() const;
};

} // namespace promptresponse
