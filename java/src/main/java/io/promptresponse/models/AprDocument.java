package io.promptresponse.models;

import java.util.*;
import java.util.stream.Stream;

public class AprDocument {
    private String version = "1.0";
    private DocumentType documentType;
    private List<Section> sections = new ArrayList<>();
    private Metadata metadata;

    public AprDocument() {
        this.documentType = DocumentType.TEMPLATE;
    }

    public AprDocument(String version, DocumentType documentType, List<Section> sections, Metadata metadata) {
        this.version = version;
        this.documentType = documentType;
        this.sections = sections;
        this.metadata = metadata;
    }

    // Get all prompts (flattened)
    public List<Prompt> getAllPrompts() {
        List<Prompt> prompts = new ArrayList<>();
        for (Section section : sections) {
            prompts.addAll(section.getAllPrompts());
        }
        return prompts;
    }

    // Find prompt by ID
    public Optional<Prompt> getPromptById(String promptId) {
        return getAllPrompts().stream()
                .filter(p -> p.getId().equals(promptId))
                .findFirst();
    }

    // Calculate completion percentage
    public double getCompletionPercentage() {
        List<Prompt> allPrompts = getAllPrompts();
        if (allPrompts.isEmpty()) {
            return 0.0;
        }

        long filled = allPrompts.stream()
                .filter(p -> p.getResponse() != null && !p.getResponse().trim().isEmpty())
                .count();

        return (double) filled / allPrompts.size() * 100.0;
    }

    // Getters and setters
    public String getVersion() { return version; }
    public void setVersion(String version) { this.version = version; }

    public DocumentType getDocumentType() { return documentType; }
    public void setDocumentType(DocumentType documentType) { this.documentType = documentType; }

    public List<Section> getSections() { return sections; }
    public void setSections(List<Section> sections) { this.sections = sections; }

    public Metadata getMetadata() { return metadata; }
    public void setMetadata(Metadata metadata) { this.metadata = metadata; }
}
