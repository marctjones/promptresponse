#pragma once

#include "models.hpp"
#include <string>
#include <utility>

namespace promptresponse {

class SignatureException : public std::runtime_error {
public:
    explicit SignatureException(const std::string& message)
        : std::runtime_error(message) {}
};

class TemplateSigner {
public:
    // Generate self-signed certificate
    static std::pair<std::string, std::string> generateCertificate(
        const std::string& name,
        const std::string& email,
        const std::string& organization = "",
        int validityDays = 365
    );

    // Sign a template
    AprDocument signTemplate(
        const AprDocument& document,
        const std::string& privateKeyPem,
        const std::string& certificatePem,
        const std::string& signerName,
        const std::string& signerEmail
    );

private:
    static std::string computeTemplateHash(const AprDocument& document);
};

class SignatureVerifier {
public:
    // Verify a single signature
    std::pair<bool, std::string> verifySignature(
        const AprDocument& document,
        const DigitalSignature& signature
    );

    // Verify all signatures on a template
    std::pair<bool, std::string> verifyTemplate(const AprDocument& document);
};

} // namespace promptresponse
