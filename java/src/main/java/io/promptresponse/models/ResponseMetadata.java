package io.promptresponse.models;

import java.time.Instant;

public class ResponseMetadata {
    private Instant lastModified;

    public Instant getLastModified() { return lastModified; }
    public void setLastModified(Instant lastModified) { this.lastModified = lastModified; }
}
