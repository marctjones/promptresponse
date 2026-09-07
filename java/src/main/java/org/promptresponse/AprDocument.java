package org.promptresponse;

import java.util.*;

/**
 * An APR document under the core profile. It deliberately retains the raw JSON
 * tree: unknown members and expression hints round-trip without loss.
 */
public final class AprDocument {
    private final LinkedHashMap<String, Object> root;
    /**
     * The prompts this filling session computed, by identity.
     *
     * Not a member, and never written. beta.6 retired {@code responseMetadata.source},
     * which tried to carry this between parties and rested a prohibition on a marker
     * every reader was free to drop. Every non-empty response in a document as it was
     * read is authored, whatever produced it, so what may be recomputed is a fact about
     * this session. With a raw JSON tree the session is the document object, so the set
     * lives here rather than on a prompt.
     */
    private final Set<Map<String, Object>> computedThisSession =
        Collections.newSetFromMap(new IdentityHashMap<>());
    AprDocument(Map<String, Object> root) { this.root = new LinkedHashMap<>(root); }
    Set<Map<String, Object>> computedThisSession() { return computedThisSession; }
    public Map<String, Object> raw() { return Collections.unmodifiableMap(root); }
    public String version() { return string(root.get("aprVersion")); }
    public String documentType() { return string(root.get("documentType")); }
    @SuppressWarnings("unchecked") public Map<String, Object> metadata() { return (Map<String, Object>) root.get("metadata"); }
    @SuppressWarnings("unchecked") public List<Object> sections() { return (List<Object>) root.get("sections"); }
    public String toJson() { return Json.write(root); }
    public void setResponse(String promptId, String response) {
        if (response == null) throw new IllegalArgumentException("APR responses are strings, not null");
        if (!setResponse(sections(), promptId, response)) throw new IllegalArgumentException("Unknown prompt id: " + promptId);
    }
    @SuppressWarnings("unchecked")
    private boolean setResponse(List<Object> sections, String id, String response) {
        for (Object item : sections) { Map<String,Object> section=(Map<String,Object>) item;
            List<Object> prompts=(List<Object>) section.getOrDefault("prompts", List.of());
            for (Object promptItem: prompts) { Map<String,Object> prompt=(Map<String,Object>)promptItem; if (id.equals(prompt.get("id"))) {
                prompt.put("response", response);
                // Whoever called this is the author of the value now in the prompt, so
                // the session no longer owns it and an expression must not rewrite it.
                computedThisSession.remove(prompt);
                return true;
            } }
            List<Object> child=(List<Object>) section.getOrDefault("sections", List.of()); if(setResponse(child,id,response)) return true;
        } return false;
    }
    static String string(Object value) { return value instanceof String s ? s : null; }
}
