package io.promptresponse.models;

import java.time.Instant;
import java.util.ArrayList;
import java.util.List;

public class Metadata {
    private String title;
    private String description;
    private String author;
    private Instant created;
    private Instant modified;
    private String templateId;
    private String templateVersion;
    private String filledBy;
    private Instant filledDate;
    private List<DigitalSignature> templateSignatures = new ArrayList<>();
    private SubmissionConfig submissionConfig;

    public String getTitle() { return title; }
    public void setTitle(String title) { this.title = title; }

    public String getDescription() { return description; }
    public void setDescription(String description) { this.description = description; }

    public String getAuthor() { return author; }
    public void setAuthor(String author) { this.author = author; }

    public Instant getCreated() { return created; }
    public void setCreated(Instant created) { this.created = created; }

    public Instant getModified() { return modified; }
    public void setModified(Instant modified) { this.modified = modified; }

    public String getTemplateId() { return templateId; }
    public void setTemplateId(String templateId) { this.templateId = templateId; }

    public String getTemplateVersion() { return templateVersion; }
    public void setTemplateVersion(String templateVersion) { this.templateVersion = templateVersion; }

    public String getFilledBy() { return filledBy; }
    public void setFilledBy(String filledBy) { this.filledBy = filledBy; }

    public Instant getFilledDate() { return filledDate; }
    public void setFilledDate(Instant filledDate) { this.filledDate = filledDate; }

    public List<DigitalSignature> getTemplateSignatures() { return templateSignatures; }
    public void setTemplateSignatures(List<DigitalSignature> templateSignatures) {
        this.templateSignatures = templateSignatures;
    }

    public SubmissionConfig getSubmissionConfig() { return submissionConfig; }
    public void setSubmissionConfig(SubmissionConfig submissionConfig) {
        this.submissionConfig = submissionConfig;
    }
}
