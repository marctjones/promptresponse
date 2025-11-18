package io.promptresponse.models;

import java.util.ArrayList;
import java.util.List;

public class Section {
    private String id;
    private String title;
    private String description;
    private List<Prompt> prompts = new ArrayList<>();
    private List<Subsection> subsections = new ArrayList<>();

    public Section() {}

    public Section(String id, String title) {
        this.id = id;
        this.title = title;
    }

    // Getters and setters
    public String getId() { return id; }
    public void setId(String id) { this.id = id; }

    public String getTitle() { return title; }
    public void setTitle(String title) { this.title = title; }

    public String getDescription() { return description; }
    public void setDescription(String description) { this.description = description; }

    public List<Prompt> getPrompts() { return prompts; }
    public void setPrompts(List<Prompt> prompts) { this.prompts = prompts; }

    public List<Subsection> getSubsections() { return subsections; }
    public void setSubsections(List<Subsection> subsections) { this.subsections = subsections; }
}
