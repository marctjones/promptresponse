package io.promptresponse.models;

import java.time.Instant;
import java.util.HashMap;
import java.util.Map;

public class SubmissionConfig {
    private String type;
    private String url;
    private Map<String, String> fields = new HashMap<>();
    private Instant expiresAt;
    private Map<String, String> headers;

    public boolean isExpired() {
        return expiresAt != null && Instant.now().isAfter(expiresAt);
    }

    public String getType() { return type; }
    public void setType(String type) { this.type = type; }

    public String getUrl() { return url; }
    public void setUrl(String url) { this.url = url; }

    public Map<String, String> getFields() { return fields; }
    public void setFields(Map<String, String> fields) { this.fields = fields; }

    public Instant getExpiresAt() { return expiresAt; }
    public void setExpiresAt(Instant expiresAt) { this.expiresAt = expiresAt; }

    public Map<String, String> getHeaders() { return headers; }
    public void setHeaders(Map<String, String> headers) { this.headers = headers; }
}
