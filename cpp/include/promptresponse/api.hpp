#pragma once

#include "models.hpp"
#include <functional>
#include <memory>

namespace promptresponse {

class SectionBuilder;
class SubsectionBuilder;

class TemplateBuilder {
public:
    explicit TemplateBuilder(std::string title, std::string templateId = "");

    TemplateBuilder& setDescription(const std::string& description);
    TemplateBuilder& setAuthor(const std::string& author);
    TemplateBuilder& setVersion(const std::string& version);

    SectionBuilder addSection(const std::string& title, const std::string& description = "");

    AprDocument build();

private:
    friend class SectionBuilder;
    Metadata metadata;
    std::vector<Section> sections;
    int sectionCounter = 0;
    int subsectionCounter = 0;
    int promptCounter = 0;
};

class SectionBuilder {
public:
    SectionBuilder(TemplateBuilder& builder, Section section)
        : templateBuilder(builder), currentSection(std::move(section)) {}

    SectionBuilder& addPrompt(
        const std::string& label,
        const std::string& expectedType = "",
        const std::string& placeholder = "",
        const std::string& helpText = ""
    );

    SubsectionBuilder addSubsection(const std::string& title, const std::string& description = "");

    TemplateBuilder& done();

private:
    friend class SubsectionBuilder;
    TemplateBuilder& templateBuilder;
    Section currentSection;
};

class SubsectionBuilder {
public:
    SubsectionBuilder(SectionBuilder& builder, Subsection subsection)
        : sectionBuilder(builder), currentSubsection(std::move(subsection)) {}

    SubsectionBuilder& addPrompt(
        const std::string& label,
        const std::string& expectedType = "",
        const std::string& placeholder = "",
        const std::string& helpText = ""
    );

    SectionBuilder& done();

private:
    SectionBuilder& sectionBuilder;
    Subsection currentSubsection;
};

class FormFiller {
public:
    // Fill a form programmatically
    static AprDocument fillForm(
        const AprDocument& templateDoc,
        const std::map<std::string, std::string>& responses,
        const std::string& filledBy = ""
    );

    // Get completion percentage
    static double getCompletionPercentage(const AprDocument& document);

    // Get empty prompts
    static std::map<std::string, std::string> getEmptyPrompts(const AprDocument& document);
};

} // namespace promptresponse
