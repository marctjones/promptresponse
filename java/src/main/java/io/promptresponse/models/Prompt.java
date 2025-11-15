package io.promptresponse.models;

public class Prompt {
    private String id;
    private String label;
    private String response = "";
    private PromptHints hints;
    private ResponseMetadata responseMetadata = new ResponseMetadata();

    public Prompt() {}

    public Prompt(String id, String label) {
        this.id = id;
        this.label = label;
    }

    // Getters and setters
    public String getId() { return id; }
    public void setId(String id) { this.id = id; }

    public String getLabel() { return label; }
    public void setLabel(String label) { this.label = label; }

    public String getResponse() { return response; }
    public void setResponse(String response) { this.response = response; }

    public PromptHints getHints() { return hints; }
    public void setHints(PromptHints hints) { this.hints = hints; }

    public ResponseMetadata getResponseMetadata() { return responseMetadata; }
    public void setResponseMetadata(ResponseMetadata responseMetadata) { this.responseMetadata = responseMetadata; }
}
