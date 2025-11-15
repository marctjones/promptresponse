package io.promptresponse.api;

import io.promptresponse.models.*;
import java.time.Instant;
import java.util.ArrayList;

public class TemplateBuilder {
    private final Metadata metadata;
    private final ArrayList<Section> sections = new ArrayList<>();
    private int sectionCounter = 0;
    private int promptCounter = 0;

    public TemplateBuilder(String title, String templateId) {
        this.metadata = new Metadata();
        this.metadata.setTitle(title);
        this.metadata.setTemplateId(templateId);
        this.metadata.setCreated(Instant.now());
        this.metadata.setModified(Instant.now());
    }

    public TemplateBuilder setDescription(String description) {
        metadata.setDescription(description);
        return this;
    }

    public TemplateBuilder setAuthor(String author) {
        metadata.setAuthor(author);
        return this;
    }

    public TemplateBuilder setVersion(String version) {
        metadata.setTemplateVersion(version);
        return this;
    }

    public SectionBuilder addSection(String title) {
        return addSection(title, null);
    }

    public SectionBuilder addSection(String title, String description) {
        sectionCounter++;
        String sectionId = String.format("section_%03d", sectionCounter);
        Section section = new Section(sectionId, title);
        section.setDescription(description);
        return new SectionBuilder(this, section);
    }

    public AprDocument build() {
        metadata.setModified(Instant.now());
        AprDocument document = new AprDocument();
        document.setVersion("1.0");
        document.setDocumentType(DocumentType.TEMPLATE);
        document.setSections(sections);
        document.setMetadata(metadata);
        return document;
    }

    void addSection(Section section) {
        sections.add(section);
    }

    String nextPromptId() {
        promptCounter++;
        return String.format("prompt_%03d", promptCounter);
    }

    public static class SectionBuilder {
        private final TemplateBuilder templateBuilder;
        private final Section section;

        SectionBuilder(TemplateBuilder templateBuilder, Section section) {
            this.templateBuilder = templateBuilder;
            this.section = section;
        }

        public SectionBuilder addPrompt(String label) {
            return addPrompt(label, null, null, null);
        }

        public SectionBuilder addPrompt(String label, String expectedType, String placeholder, String helpText) {
            String promptId = templateBuilder.nextPromptId();
            Prompt prompt = new Prompt(promptId, label);

            if (expectedType != null || placeholder != null || helpText != null) {
                PromptHints hints = new PromptHints();
                hints.setExpectedDataType(expectedType);
                hints.setPlaceholder(placeholder);
                hints.setHelpText(helpText);
                prompt.setHints(hints);
            }

            section.getPrompts().add(prompt);
            return this;
        }

        public TemplateBuilder done() {
            templateBuilder.addSection(section);
            return templateBuilder;
        }
    }
}
