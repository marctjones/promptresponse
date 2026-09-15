package org.promptresponse;

import java.io.StringReader;
import java.util.*;
import org.yaml.snakeyaml.DumperOptions;
import org.yaml.snakeyaml.events.*;
import org.yaml.snakeyaml.LoaderOptions;
import org.yaml.snakeyaml.Yaml;
import org.yaml.snakeyaml.constructor.SafeConstructor;
import org.yaml.snakeyaml.nodes.Tag;
import org.yaml.snakeyaml.resolver.Resolver;

/** APR beta.6 JSONC/YAML representation and independent-stream API. */
public final class AprBeta6 {
    public static final String VERSION = "1.0-beta.6";
    public enum Representation { JSONC, YAML }
    public sealed interface Record permits FormRecord, AttestationRecord { }
    public record FormRecord(AprDocument document, Map<String,Object> value) implements Record {
        public FormRecord(AprDocument document) {
            this(document, cast(Json.parse(document.toJson())));
        }
    }
    public record AttestationRecord(Map<String,Object> value) implements Record { }
    private AprBeta6() { }

    /** Reads all independent occurrences without deriving a relationship from order. */
    public static List<Record> readStream(String source, Representation representation) {
        if (representation == Representation.YAML) return yamlDocuments(source).stream().map(AprBeta6::parseRecord).toList();
        boolean isStream = source.indexOf('') >= 0;
        return splitJsonc(source).stream().map(part -> parseJsoncRecord(part, isStream)).toList();
    }

    /**
     * A record in a jsonc-stream that will not decode as JSON at all is either
     * malformed or written in the other representation, and those are different
     * faults: reporting PARSE_ERROR for a YAML record would name the symptom and
     * hide the rule actually broken. Only worth telling apart in an actual
     * stream -- a lone malformed document has nothing to be mixed with.
     */
    private static Record parseJsoncRecord(String part, boolean isStream) {
        try {
            return parseRecord(stripJsonc(part));
        } catch (AprException failure) {
            if (!"PARSE_ERROR".equals(failure.code()) || !isStream) throw failure;
            List<String> other;
            try { other = yamlDocuments(part); } catch (RuntimeException ignored) { throw failure; }
            if (!other.isEmpty()) throw new AprException("this stream mixes APR-JSONC and APR-YAML records", "APR_STREAM_MIXED_REPRESENTATIONS");
            throw failure;
        }
    }

    /** Reads one form, explicitly refusing to select a stream record by position. */
    public static AprDocument readForm(String source, Representation representation) {
        List<Record> records = readStream(source, representation);
        if (records.size() != 1 || !(records.getFirst() instanceof FormRecord form)) throw new AprException("a stream must be handled through readStream, not readForm", "APR_STREAM_REQUIRES_ITERATION");
        return form.document();
    }

    /** Writes a beta.6 form in the requested representation. */
    public static String writeForm(AprDocument document, Representation representation) {
        if (!VERSION.equals(document.version())) throw new AprException("APR beta.6 writers require version " + VERSION);
        return writeJson(document.toJson(), representation);
    }

    /** Writes every independent record in supplied occurrence order. */
    public static String writeStream(Iterable<Record> records, Representation representation) {
        List<String> output = new ArrayList<>();
        for (Record record : records) {
            String json = record instanceof FormRecord form
                ? form.value() != null ? Json.write(form.value()) : writeForm(form.document(), Representation.JSONC)
                : Json.write(((AttestationRecord) record).value());
            output.add(writeJson(json, representation));
        }
        return representation == Representation.JSONC ? output.stream().map(value -> "\u001e" + value + "\n").reduce("", String::concat) : String.join("---\n", output);
    }

    @SuppressWarnings("unchecked") private static Record parseRecord(String json) {
        rejectDuplicateObjectMembers(json);
        Object parsed = Json.parse(json);
        if (!(parsed instanceof Map<?,?> raw)) throw new AprException("An APR beta.6 record must be an object", "PARSE_ERROR");
        Map<String,Object> value = (Map<String,Object>) raw;
        if (!VERSION.equals(value.get("aprVersion"))) throw new AprException("APR beta.6 records must declare aprVersion " + VERSION, "UNSUPPORTED_VERSION");
        if (value.containsKey("recordType")) {
            if (!"attestation".equals(value.get("recordType"))) throw new AprException("Unknown APR beta.6 stream record type", "WRONG_TYPE");
            validateAttestation(value);
            return new AttestationRecord(Map.copyOf(value));
        }
        return new FormRecord(Apr.parse(Json.write(value)), value);
    }

    private static List<String> splitJsonc(String source) {
        String[] split = source.indexOf('\u001e') >= 0 ? source.split("\\u001e") : new String[] { source };
        return Arrays.stream(split).filter(value -> !value.isBlank()).toList();
    }

    /**
     * APR defines its own YAML schema. SnakeYAML resolves YAML 1.1, under which
     * "yes" is a boolean, "012" is octal 10 and ".inf" is a float - none of which
     * are APR values, and the "012" case is silent data loss in a response. The
     * library is used for syntax only; resolution follows the specification's
     * table. A YAML 1.2 library would not remove the need for this: under YAML
     * 1.2's core schema "012" still resolves as the number 12.
     */
    private static final class AprResolver extends Resolver {
        @Override protected void addImplicitResolvers() {
            addImplicitResolver(Tag.NULL, java.util.regex.Pattern.compile("^(?:~|null|Null|NULL| )$"), "~nN\0");
            addImplicitResolver(Tag.NULL, java.util.regex.Pattern.compile("^$"), null);
            addImplicitResolver(Tag.BOOL, java.util.regex.Pattern.compile("^(?:true|True|TRUE|false|False|FALSE)$"), "tTfF");
            // An integer-looking scalar resolves to an integral Number, as the JSONC
            // parser yields for "maxRows": 5. Resolvers are tried in insertion order,
            // so the integer form must precede the float one.
            addImplicitResolver(Tag.INT, java.util.regex.Pattern.compile("^-?(?:0|[1-9][0-9]*)$"), "-0123456789");
            addImplicitResolver(Tag.FLOAT,
                java.util.regex.Pattern.compile("^-?(?:0|[1-9][0-9]*)(?:\\.[0-9]+)?(?:[eE][-+]?[0-9]+)?$"),
                "-0123456789");
        }
    }

    private static final Map<String, String> DEFAULT_TAG_HANDLES = Map.of("!", "!", "!!", "tag:yaml.org,2002:");

    /**
     * The constructs APR excludes (specification 4.5) are node properties and
     * directives, so they are detected on the parser's event stream rather than
     * in the source text: "&amp;", "*" and "!" inside a plain scalar's content, as
     * in {@code string(fee_count * 8.0)}, are ordinary characters of an ordinary
     * string. A merge key is a plain {@code <<} in key position; a quoted one is a
     * string key. Running on events, before construction, also means an alias is
     * never expanded.
     */
    private static void rejectYamlFeatures(Yaml yaml, String source) {
        Deque<int[]> frames = new ArrayDeque<>(); // {isMapping, nodesSeen} per open collection
        for (Event event : yaml.parse(new StringReader(source))) {
            boolean isKey = false;
            if (event instanceof NodeEvent && !frames.isEmpty()) {
                int[] frame = frames.peek();
                isKey = frame[0] == 1 && frame[1] % 2 == 0;
                frame[1]++;
            }
            if (event instanceof DocumentStartEvent start) {
                boolean customTag = start.getTags() != null && start.getTags().entrySet().stream()
                    .anyMatch(tag -> !tag.getValue().equals(DEFAULT_TAG_HANDLES.get(tag.getKey())));
                if (start.getVersion() != null || customTag) throw new AprException("APR YAML forbids directives, including %YAML and %TAG", "YAML_DIRECTIVE_FORBIDDEN");
            } else if (event instanceof AliasEvent) {
                throw new AprException("APR YAML forbids aliases", "YAML_ANCHOR_FORBIDDEN");
            } else if (event instanceof NodeEvent node) {
                if (node.getAnchor() != null) throw new AprException("APR YAML forbids anchors", "YAML_ANCHOR_FORBIDDEN");
                if (event instanceof ScalarEvent scalar) {
                    if (scalar.getTag() != null) throw new AprException("APR YAML forbids tags", "YAML_TAG_FORBIDDEN");
                    if (isKey && scalar.getScalarStyle() == DumperOptions.ScalarStyle.PLAIN && "<<".equals(scalar.getValue())) throw new AprException("APR YAML forbids merge keys", "YAML_MERGE_KEY_FORBIDDEN");
                } else if (event instanceof CollectionStartEvent collection) {
                    if (collection.getTag() != null) throw new AprException("APR YAML forbids tags", "YAML_TAG_FORBIDDEN");
                    frames.push(new int[] { event instanceof MappingStartEvent ? 1 : 0, 0 });
                }
            } else if (event instanceof CollectionEndEvent) {
                frames.pop();
            }
        }
    }

    private static List<String> yamlDocuments(String source) {
        if (source.matches("(?s).*(?m):\\s*[-+]?\\.(?:inf|Inf|INF|nan|NaN|NAN)\\s*$.*")) throw new AprException("APR YAML forbids a non-finite number: JSON cannot represent it", "YAML_NON_FINITE_NUMBER");
        Yaml yaml = new Yaml(new SafeConstructor(new LoaderOptions()), new org.yaml.snakeyaml.representer.Representer(new org.yaml.snakeyaml.DumperOptions()), new org.yaml.snakeyaml.DumperOptions(), new AprResolver());
        try {
            rejectYamlFeatures(yaml, source);
            List<String> values = new ArrayList<>();
            for (Object value : yaml.loadAll(source)) values.add(Json.write(normalizeYaml(value)));
            return values;
        } catch (AprException excluded) {
            throw excluded;
        } catch (RuntimeException malformed) {
            // SnakeYAML's own exceptions (ScannerException, ParserException, ...) are not
            // AprException and carry no code; a document that is not well-formed YAML at
            // all is a parse failure regardless of which of those it throws.
            throw new AprException("invalid APR YAML: " + malformed.getMessage(), "PARSE_ERROR");
        }
    }

    @SuppressWarnings("unchecked") private static Object normalizeYaml(Object value) {
        if (value instanceof Map<?,?> map) { Map<String,Object> result = new LinkedHashMap<>(); for (var entry : map.entrySet()) { if (!(entry.getKey() instanceof String key)) throw new AprException("APR YAML requires every mapping key to resolve to a string", "PARSE_ERROR"); result.put(key, normalizeYaml(entry.getValue())); } return result; }
        if (value instanceof List<?> list) return list.stream().map(AprBeta6::normalizeYaml).toList();
        if (value instanceof String || value instanceof Number || value instanceof Boolean || value == null) return value;
        throw new AprException("APR YAML contains an unsupported value");
    }

    private static String writeJson(String json, Representation representation) {
        if (representation == Representation.JSONC) return json;
        Object value = Json.parse(json);
        return new Yaml().dump(value);
    }

    @SuppressWarnings("unchecked") private static void validateAttestation(Map<String,Object> value) {
        if (!(value.get("subject") instanceof Map<?,?> rawSubject)) throw new AprException("beta.6 attestation requires subject");
        Map<String,Object> subject=(Map<String,Object>)rawSubject;
        if (!digest(subject.get("digest")) || !"jcs-sha256".equals(subject.get("canonicalization"))) throw new AprException("beta.6 attestation requires subject.digest and jcs-sha256 canonicalization");
        if (!(value.get("scope") instanceof Map<?,?> rawScope)) throw new AprException("beta.6 attestation requires scope");
        Map<String,Object> scope=(Map<String,Object>)rawScope;
        if (!("document".equals(scope.get("kind")) || "fields".equals(scope.get("kind")))) throw new AprException("beta.6 attestation scope.kind must be document or fields");
        if ("fields".equals(scope.get("kind")) && (!(scope.get("fields") instanceof List<?> fields) || fields.isEmpty() || fields.stream().anyMatch(field -> !(field instanceof String text) || text.isBlank()))) throw new AprException("beta.6 fields attestations require non-blank scope.fields");
        if (!(value.get("manifest") instanceof Map<?,?> rawManifest)) throw new AprException("beta.6 attestation requires manifest");
        Map<String,Object> manifest=(Map<String,Object>)rawManifest;
        if (!digest(manifest.get("root")) || !(manifest.get("entries") instanceof List<?> entries)) throw new AprException("beta.6 attestation requires manifest.root and manifest.entries");
        for (Object rawEntry : entries) {
            if (!(rawEntry instanceof Map<?,?> entry) || !(entry.get("path") instanceof String) || !digest(entry.get("digest"))) throw new AprException("beta.6 manifest entries require path and digest");
        }
        if (!(value.get("proofs") instanceof List<?>) || !(value.get("witnesses") instanceof List<?> witnesses) || witnesses.stream().anyMatch(witness -> !digest(witness))) throw new AprException("beta.6 attestations require proofs and digest witnesses arrays");
    }

    private static boolean digest(Object value) {
        return value instanceof String text && text.matches("sha256:[0-9a-f]{64}");
    }

    private static String stripJsonc(String input) {
        StringBuilder output = new StringBuilder(); boolean quote = false, escaped = false;
        for (int i = 0; i < input.length(); i++) { char c = input.charAt(i);
            if (quote) { output.append(c); if (escaped) escaped=false; else if(c=='\\') escaped=true; else if(c=='"') quote=false; continue; }
            if (c=='"') { quote=true; output.append(c); continue; }
            if (c=='/' && i+1<input.length() && input.charAt(i+1)=='/') { while(i<input.length() && input.charAt(i)!='\n') i++; if(i<input.length()) output.append('\n'); continue; }
            if (c=='/' && i+1<input.length() && input.charAt(i+1)=='*') { i+=2; while(i+1<input.length() && !(input.charAt(i)=='*' && input.charAt(i+1)=='/')) i++; if(i+1>=input.length()) throw new AprException("Unterminated JSONC comment", "PARSE_ERROR"); i++; continue; }
            output.append(c);
        }
        return output.toString().replaceAll(",(\\s*[}\\]])", "$1");
    }

    /** JSON parsers commonly use last-key-wins; APR JSONC rejects duplicates first. */
    private static void rejectDuplicateObjectMembers(String source) {
        Deque<Set<String>> objects = new ArrayDeque<>();
        Deque<Boolean> containers = new ArrayDeque<>();
        for (int i = 0; i < source.length();) {
            char current = source.charAt(i);
            if (Character.isWhitespace(current) || current == ',' || current == ':') { i++; continue; }
            if (current == '{') { containers.push(true); objects.push(new HashSet<>()); i++; continue; }
            if (current == '[') { containers.push(false); i++; continue; }
            if (current == '}' || current == ']') { if (containers.pop()) objects.pop(); i++; continue; }
            if (current == '"') {
                int start = i++; boolean escaped = false;
                while (i < source.length()) { char c = source.charAt(i++); if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') break; }
                if (i > source.length() || source.charAt(i - 1) != '"') throw new AprException("Unterminated JSON string", "PARSE_ERROR");
                int next = i; while (next < source.length() && Character.isWhitespace(source.charAt(next))) next++;
                if (next < source.length() && source.charAt(next) == ':' && !containers.isEmpty() && containers.peek()) {
                    String key = (String) Json.parse(source.substring(start, i));
                    if (!objects.peek().add(key)) throw new AprException("APR JSONC object has duplicate member '" + key + "'.", "DUPLICATE_MEMBER");
                }
                continue;
            }
            i++;
        }
        if (!containers.isEmpty()) throw new AprException("Unclosed JSON container", "PARSE_ERROR");
    }

    @SuppressWarnings("unchecked") private static Map<String,Object> cast(Object value) {
        return (Map<String,Object>) value;
    }
}
