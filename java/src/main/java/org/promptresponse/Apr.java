package org.promptresponse;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;

/** Entry point for reading, writing, and structurally validating APR documents. */
public final class Apr {
    public static final String PROFILE = "core";
    private Apr() { }
    public static AprDocument parse(String json) {
        Object parsed = Json.parse(json);
        if (!(parsed instanceof Map<?, ?> map)) throw new AprException("An APR document must be a JSON object");
        @SuppressWarnings("unchecked") Map<String,Object> root = (Map<String,Object>) map;
        for (String required : List.of("version", "metadata", "sections")) if (!root.containsKey(required)) throw new AprException(required + " is required");
        if (!(root.get("version") instanceof String)) throw new AprException("version must be a string");
        if (!(root.get("metadata") instanceof Map<?, ?>)) throw new AprException("metadata must be an object");
        if (!(root.get("sections") instanceof List<?>)) throw new AprException("sections must be an array");
        rejectBadShape(root);
        return new AprDocument(root);
    }
    public static AprDocument read(Path path) throws IOException {
        String json = Files.readString(path, StandardCharsets.UTF_8);
        return parse(json.startsWith("\uFEFF") ? json.substring(1) : json);
    }
    public static void write(AprDocument document, Path path) throws IOException { Files.writeString(path, document.toJson(), StandardCharsets.UTF_8); }
    public static ValidationResult validate(AprDocument document) {
        List<ValidationIssue> errors = new ArrayList<>();
        if (blank(document.version())) issue(errors,"REQUIRED_FIELD","version","version is required.");
        else if (!document.version().split("-",2)[0].startsWith("1.")) issue(errors,"UNSUPPORTED_VERSION","version","Unsupported APR major version.");
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
        sections((List<Object>)root.get("sections"));
    }
    @SuppressWarnings("unchecked") private static void sections(List<Object> list) { for(Object item:list) { if(!(item instanceof Map<?,?>)) throw new AprException("section must be an object"); Map<String,Object>s=(Map<String,Object>)item; if(s.containsKey("prompts") && !(s.get("prompts") instanceof List<?>)) throw new AprException("section.prompts must be an array"); if(s.containsKey("sections") && !(s.get("sections") instanceof List<?>)) throw new AprException("section.sections must be an array"); for(Object p:(List<Object>)s.getOrDefault("prompts",List.of())) { if(!(p instanceof Map<?,?>)) throw new AprException("prompt must be an object"); Object response=((Map<?,?>)p).get("response"); if(response != null && !(response instanceof String)) throw new AprException("prompt.response must be a string"); } sections((List<Object>)s.getOrDefault("sections",List.of())); } }
    private static void strings(Map<String,Object> map,String key,String path) { if(map.containsKey(key) && (!(map.get(key) instanceof List<?> values) || values.stream().anyMatch(value -> !(value instanceof String)))) throw new AprException(path+" must be an array of strings"); }
}
