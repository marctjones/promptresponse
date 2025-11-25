package io.promptresponse.models;

import java.util.ArrayList;
import java.util.List;

public class Section {
    private String id;
    private String title;
    private String description;
    private List<Prompt> prompts = new ArrayList<>();
    private List<Section> sections = new ArrayList<>();

    public Section() {}

    public Section(String id, String title) {
        this.id = id;
        this.title = title;
    }

    // Get all prompts in this section and nested sections (flattened)
    public List<Prompt> getAllPrompts() {
        List<Prompt> allPrompts = new ArrayList<>(prompts);
        for (Section section : sections) {
            allPrompts.addAll(section.getAllPrompts());
        }
        return allPrompts;
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

    public List<Section> getSections() { return sections; }
    public void setSections(List<Section> sections) { this.sections = sections; }
}
