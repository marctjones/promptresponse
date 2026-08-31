package org.promptresponse;

import java.util.*;

/**
 * An APR document under the core profile. It deliberately retains the raw JSON
 * tree: unknown members and expression hints round-trip without loss.
 */
public final class AprDocument {
    private final LinkedHashMap<String, Object> root;
    AprDocument(Map<String, Object> root) { this.root = new LinkedHashMap<>(root); }
    public Map<String, Object> raw() { return Collections.unmodifiableMap(root); }
    public String version() { return string(root.get("version")); }
    public String documentType() { return string(root.get("documentType")); }
    @SuppressWarnings("unchecked") public Map<String, Object> metadata() { return (Map<String, Object>) root.get("metadata"); }
    @SuppressWarnings("unchecked") public List<Object> sections() { return (List<Object>) root.get("sections"); }
    public String toJson() { return Json.write(root); }
    public void setResponse(String promptId, String response) {
        if (response == null) throw new IllegalArgumentException("APR responses are strings, not null");
        if (!setResponse(sections(), promptId, response)) throw new IllegalArgumentException("Unknown prompt id: " + promptId);
    }
    @SuppressWarnings("unchecked")
    private static boolean setResponse(List<Object> sections, String id, String response) {
        for (Object item : sections) { Map<String,Object> section=(Map<String,Object>) item;
            List<Object> prompts=(List<Object>) section.getOrDefault("prompts", List.of());
            for (Object promptItem: prompts) { Map<String,Object> prompt=(Map<String,Object>)promptItem; if (id.equals(prompt.get("id"))) {
                prompt.put("response", response);
                Object existing = prompt.get("responseMetadata");
                if (existing instanceof Map<?,?> raw) {
                    @SuppressWarnings("unchecked") Map<String,Object> metadata = (Map<String,Object>) raw;
                    metadata.remove("source");
                }
                return true;
            } }
            List<Object> child=(List<Object>) section.getOrDefault("sections", List.of()); if(setResponse(child,id,response)) return true;
        } return false;
    }
    static String string(Object value) { return value instanceof String s ? s : null; }
}
