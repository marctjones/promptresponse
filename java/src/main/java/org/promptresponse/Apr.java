package org.promptresponse;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;

/** Entry point for reading, writing, and structurally validating APR documents. */
public final class Apr {
    public static final String PROFILE = "core";
    public static final String VERSION = "1.0-beta.6";
    private Apr() { }
    public static AprDocument parse(String json) {
        Object parsed = Json.parse(json);
        if (!(parsed instanceof Map<?, ?> map)) throw new AprException("An APR document must be a JSON object");
        @SuppressWarnings("unchecked") Map<String,Object> root = (Map<String,Object>) map;
        for (String required : List.of("aprVersion", "metadata", "sections")) if (!root.containsKey(required)) throw new AprException(required + " is required");
        if (!(root.get("aprVersion") instanceof String)) throw new AprException("aprVersion must be a string");
        if (!VERSION.equals(root.get("aprVersion"))) throw new AprException("Unsupported APR version " + root.get("aprVersion") + "; this build accepts only " + VERSION);
        if (!(root.get("metadata") instanceof Map<?, ?>)) throw new AprException("metadata must be an object");
        if (!(root.get("sections") instanceof List<?>)) throw new AprException("sections must be an array");
        if (root.containsKey("signatures")) throw new AprException("RETIRED_EMBEDDED_SIGNATURES");
        rejectBadShape(root);
        dropRetiredMembers(root);
        return new AprDocument(root);
    }
    public static AprDocument read(Path path) throws IOException {
        String json = Files.readString(path, StandardCharsets.UTF_8);
        return parse(json.startsWith("\uFEFF") ? json.substring(1) : json);
    }
    public static void write(AprDocument document, Path path) throws IOException {
        if (!VERSION.equals(document.version())) throw new AprException("Unsupported APR version " + document.version() + "; this build accepts only " + VERSION);
        Files.writeString(path, document.toJson(), StandardCharsets.UTF_8);
    }
    public static ValidationResult validate(AprDocument document) {
        List<ValidationIssue> errors = new ArrayList<>();
        if (blank(document.version())) issue(errors,"REQUIRED_FIELD","aprVersion","aprVersion is required.");
        else if (!VERSION.equals(document.version())) issue(errors,"UNSUPPORTED_VERSION","aprVersion","Unsupported APR version; this build accepts only " + VERSION + ".");
        String title = AprDocument.string(document.metadata().get("title")); if(blank(title)) issue(errors,"REQUIRED_FIELD","metadata.title","metadata.title is required.");
        if(document.sections().isEmpty()) issue(errors,"REQUIRED_FIELD","sections","A document must have at least one section.");
        if("filledForm".equals(document.documentType()) && blank(AprDocument.string(document.metadata().get("templateId")))) issue(errors,"REQUIRED_FIELD","metadata.templateId","A filled form must record templateId.");
        Set<String> sectionIds=new HashSet<>(), promptIds=new HashSet<>(); validateSections(document.sections(), "sections", errors, sectionIds, promptIds);
        return new ValidationResult(List.copyOf(errors));
    }
    private static boolean blank(String value) { return value == null || value.trim().isEmpty(); }
    private static void issue(List<ValidationIssue> list,String code,String path,String message){ list.add(new ValidationIssue(code,path,message)); }
    @SuppressWarnings("unchecked") private static void validateSections(List<Object> items,String path,List<ValidationIssue> errors,Set<String> sectionIds,Set<String> promptIds) {
        for(Object item:items) { Map<String,Object> section=(Map<String,Object>)item; String id=AprDocument.string(section.get("id")); String here=path+"["+(id==null?"?":id)+"]";
            if(blank(id)) issue(errors,"REQUIRED_FIELD",here,"Section id is required."); else if(!sectionIds.add(id)) issue(errors,"DUPLICATE_ID",here,"Duplicate section id: "+id);
            if(blank(AprDocument.string(section.get("title")))) issue(errors,"REQUIRED_FIELD",here+".title","Section title is required.");
            List<Object> prompts=(List<Object>)section.getOrDefault("prompts",List.of()), children=(List<Object>)section.getOrDefault("sections",List.of());
            if(prompts.isEmpty() && children.isEmpty() && !"table".equals(section.get("kind"))) issue(errors,"EMPTY_SECTION",here,"A section must contain prompts or child sections.");
            for(Object promptItem:prompts) { Map<String,Object> prompt=(Map<String,Object>)promptItem; String pid=AprDocument.string(prompt.get("id")); String ppath=here+"."+(pid==null?"?":pid); if(blank(pid)) issue(errors,"REQUIRED_FIELD",ppath,"Prompt id is required."); else if(!promptIds.add(pid)) issue(errors,"DUPLICATE_ID",ppath,"Duplicate prompt id: "+pid); if(blank(AprDocument.string(prompt.get("label")))) issue(errors,"REQUIRED_FIELD",ppath+".label","Prompt label is required."); }
            validateSections(children,here,errors,sectionIds,promptIds);
        }
    }
    @SuppressWarnings("unchecked") private static void rejectBadShape(Map<String,Object> root) {
        Map<String,Object> metadata=(Map<String,Object>)root.get("metadata"); if(metadata.containsKey("submissionUrl")) throw new AprException("metadata.submissionUrl is retired; use submissionUrls array");
        strings(metadata,"submissionUrls","metadata.submissionUrls");
        if(root.containsKey("roles") && !(root.get("roles") instanceof List<?>)) throw new AprException("roles must be an array");
        structuralTypes(root, DOCUMENT, "");
        structuralTypes(metadata, METADATA, "/metadata");
        sections((List<Object>)root.get("sections"));
    }

    /**
     * Workflow state beta.6 retired, dropped rather than carried forward.
     *
     * None carried a claim whose silent loss would be worse than its removal — unlike
     * embedded {@code signatures}, which is refused with a diagnostic because a document
     * holding it was making a cryptographic claim beta.6 cannot honour. Preserving these
     * would write them back into a document the format says has none.
     *
     * The pre-1.0 presentation members are deliberately absent: .NET and the scripting
     * SDKs retire different sets of those, and which list is right is a question for the
     * specification rather than for this reader to guess (issue #376).
     */
    private static final Set<String> RETIRED_MEMBERS = Set.of("responseMetadata", "filledBy", "filledDate");

    @SuppressWarnings("unchecked") private static void dropRetiredMembers(Object node) {
        if (node instanceof Map<?,?> raw) {
            Map<String,Object> map = (Map<String,Object>) raw;
            map.keySet().removeAll(RETIRED_MEMBERS);
            for (Object child : map.values()) dropRetiredMembers(child);
        } else if (node instanceof List<?> list) {
            for (Object child : list) dropRetiredMembers(child);
        }
    }

    /**
     * Members the format types, and the JSON types each may carry.
     *
     * The specification splits two conditions a typed reader runs together. A wrongly
     * typed <em>response</em> is a parse failure, because a response is always a string
     * and a document spelling one as a number is not an APR document (7.3). A wrongly
     * typed <em>structural member</em> is the validation error {@code WRONG_TYPE},
     * because the record parses: it is well formed and says something the format does
     * not allow (7.1). A member the format does not type is preserved and ignored
     * whatever it holds, which is what makes additive change safe.
     */
    private static final Map<String,Class<?>[]> DOCUMENT = Map.of(
        "aprVersion", new Class<?>[]{String.class}, "documentType", new Class<?>[]{String.class},
        "metadata", new Class<?>[]{Map.class}, "sections", new Class<?>[]{List.class});
    // Map.of caps at ten pairs, and metadata has more members than that.
    private static final Map<String,Class<?>[]> METADATA = Map.ofEntries(
        Map.entry("title", new Class<?>[]{String.class}),
        Map.entry("description", new Class<?>[]{String.class}),
        Map.entry("author", new Class<?>[]{String.class}),
        Map.entry("publisher", new Class<?>[]{String.class}),
        Map.entry("templateId", new Class<?>[]{String.class}),
        Map.entry("templateVersion", new Class<?>[]{String.class}),
        Map.entry("created", new Class<?>[]{String.class}),
        Map.entry("modified", new Class<?>[]{String.class}),
        Map.entry("submissionUrls", new Class<?>[]{List.class}),
        Map.entry("regarding", new Class<?>[]{List.class}));
    private static final Map<String,Class<?>[]> SECTION = Map.of(
        "id", new Class<?>[]{String.class}, "title", new Class<?>[]{String.class},
        "description", new Class<?>[]{String.class}, "role", new Class<?>[]{String.class},
        "kind", new Class<?>[]{String.class},
        "canAddRows", new Class<?>[]{Boolean.class}, "maxRows", new Class<?>[]{Number.class},
        "prompts", new Class<?>[]{List.class}, "sections", new Class<?>[]{List.class});
    private static final Map<String,Class<?>[]> PROMPT = Map.of(
        "id", new Class<?>[]{String.class}, "label", new Class<?>[]{String.class},
        "response", new Class<?>[]{String.class}, "role", new Class<?>[]{String.class},
        "hints", new Class<?>[]{Map.class});
    private static final Map<String,Class<?>[]> HINTS = Map.of(
        "expectedDataType", new Class<?>[]{String.class}, "placeholder", new Class<?>[]{String.class},
        "helpText", new Class<?>[]{String.class}, "validationPattern", new Class<?>[]{String.class},
        "suggestedValues", new Class<?>[]{List.class}, "step", new Class<?>[]{Number.class},
        // `min` and `max` are a number on an ordered numeric field and a canonical-form
        // string on a temporal one, so both spellings are the format's own.
        "min", new Class<?>[]{Number.class, String.class}, "max", new Class<?>[]{Number.class, String.class});

    private static void structuralTypes(Map<String,Object> node, Map<String,Class<?>[]> table, String path) {
        for (Map.Entry<String,Object> member : node.entrySet()) {
            Class<?>[] allowed = table.get(member.getKey());
            // An explicit null is absence, not a wrong type: a member table governs a
            // value that is present.
            if (allowed == null || member.getValue() == null) continue;
            boolean ok = false;
            for (Class<?> type : allowed) if (type.isInstance(member.getValue())) ok = true;
            if (!ok) throw new AprException("WRONG_TYPE: " + path + "/" + member.getKey() + " is "
                + spell(member.getValue()) + " where the format declares " + spell(allowed)
                + "; APR values are never coerced.");
        }
    }
    private static String spell(Object value) {
        if (value instanceof Boolean) return "a boolean";
        if (value instanceof Number) return "a number";
        if (value instanceof String) return "a string";
        if (value instanceof List<?>) return "an array";
        return value instanceof Map<?,?> ? "an object" : "null";
    }
    private static String spell(Class<?>[] allowed) {
        List<String> names = new ArrayList<>();
        for (Class<?> type : allowed) names.add(
            type == Boolean.class ? "a boolean" : type == Number.class ? "a number"
            : type == String.class ? "a string" : type == List.class ? "an array" : "an object");
        return String.join(" or ", names);
    }
    @SuppressWarnings("unchecked") private static void sections(List<Object> list) { for(Object item:list) { if(!(item instanceof Map<?,?>)) throw new AprException("section must be an object"); Map<String,Object>s=(Map<String,Object>)item; structuralTypes(s, SECTION, "/sections"); if(s.containsKey("prompts") && !(s.get("prompts") instanceof List<?>)) throw new AprException("section.prompts must be an array"); if(s.containsKey("sections") && !(s.get("sections") instanceof List<?>)) throw new AprException("section.sections must be an array"); for(Object p:(List<Object>)s.getOrDefault("prompts",List.of())) { if(!(p instanceof Map<?,?>)) throw new AprException("prompt must be an object"); Map<String,Object>pm=(Map<String,Object>)p; Object response=pm.get("response"); if(response != null && !(response instanceof String)) throw new AprException("prompt.response must be a string"); structuralTypes(pm, PROMPT, "/prompts"); if(pm.get("hints") instanceof Map<?,?> h) structuralTypes((Map<String,Object>)h, HINTS, "/prompts/hints"); } sections((List<Object>)s.getOrDefault("sections",List.of())); } }
    private static void strings(Map<String,Object> map,String key,String path) { if(map.containsKey(key) && (!(map.get(key) instanceof List<?> values) || values.stream().anyMatch(value -> !(value instanceof String)))) throw new AprException(path+" must be an array of strings"); }
}
