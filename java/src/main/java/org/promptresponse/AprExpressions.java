package org.promptresponse;

import dev.cel.bundle.Cel;
import dev.cel.bundle.CelFactory;
import dev.cel.common.CelValidationResult;
import dev.cel.common.types.CelType;
import dev.cel.common.types.ListType;
import dev.cel.common.types.MapType;
import dev.cel.common.types.SimpleType;

import java.time.Instant;
import java.time.LocalDate;
import java.time.OffsetDateTime;
import java.util.*;

/** APR's optional CEL expression binding. Evaluation is pure and advisory. */
public final class AprExpressions {
    public static final String PROFILE = "core+expressions";
    public static final String COMPUTED_SOURCE = "computed";
    private AprExpressions() { }

    public static boolean recomputeComputedValues(AprDocument document) {
        boolean changed = false;
        for (int pass = 0; pass < 5; pass++) {
            Context context = new Context(document); boolean changedThisPass = false;
            for (Map<String,Object> prompt : prompts(document.sections())) {
                Map<String,Object> hints = map(prompt.get("hints"));
                String expression = AprDocument.string(hints.get("exprValue"));
                if (blank(expression)) continue;
                String response = Optional.ofNullable(AprDocument.string(prompt.get("response"))).orElse("");
                Map<String,Object> responseMetadata = map(prompt.get("responseMetadata"));
                if (!response.isEmpty() && !COMPUTED_SOURCE.equals(AprDocument.string(responseMetadata.get("source")))) continue;
                String result = context.evaluate(prompt, expression);
                if (result != null && !result.equals(response)) {
                    prompt.put("response", result);
                    responseMetadata.put("source", COMPUTED_SOURCE);
                    prompt.put("responseMetadata", responseMetadata);
                    changedThisPass = changed = true;
                }
            }
            if (!changedThisPass) break;
        }
        return changed;
    }

    public static final class Context {
        private final Map<String,Map<String,Object>> prompts = new LinkedHashMap<>();
        private final Map<String,Object> bindings = new LinkedHashMap<>();
        private final String today;
        Context(AprDocument document) { this(document, null, null); }
        Context(AprDocument document, String instant, Map<String,String> context) {
            this.today = instant;
            for (Map<String,Object> prompt : prompts(document.sections())) {
                String id = AprDocument.string(prompt.get("id")); if (blank(id) || prompts.containsKey(id)) continue;
                prompts.put(id, prompt); Object value = bind(prompt);
                if (value != null) bindings.put(id, value);
            }
            // _now and _today are caller-supplied and never read from the host
            // clock, so the same form with the same inputs evaluates the same way
            // twice. Instant.now() made every expression using them
            // non-deterministic and unlike the other implementations.
            if (instant != null && !instant.isBlank()) {
                try { bindings.put("_now", Instant.parse(instant)); } catch (RuntimeException ignored) { }
                bindings.put("_today", instant.length() >= 10 ? instant.substring(0, 10) : instant);
            }
            bindings.put("ctx", context == null ? Map.of() : Map.copyOf(context));
        }
        public String evaluate(Map<String,Object> prompt, String expression) {
            try {
                CelFactory.standardCelBuilder().build();
                var builder = CelFactory.standardCelBuilder();
                for (Map.Entry<String,Map<String,Object>> entry : prompts.entrySet()) builder.addVar(entry.getKey(), typeOf(entry.getValue()));
                // Declared only when bound. A declared but unbound name does not
                // reliably error: it can evaluate to a default, so an expression
                // referencing an unsupplied _today would produce a value rather
                // than degrading. Leaving it undeclared makes the reference a
                // compile error, which is what the specification requires.
                if (bindings.containsKey("_today")) builder.addVar("_today", SimpleType.STRING);
                if (bindings.containsKey("_now")) builder.addVar("_now", SimpleType.TIMESTAMP);
                builder.addVar("_id", SimpleType.STRING)
                       .addVar("ctx", MapType.create(SimpleType.STRING, SimpleType.STRING))
                       .addVar("_this", typeOf(prompt));
                Cel cel = builder.build(); CelValidationResult checked = cel.compile(expression);
                if (checked.hasError()) return null;
                Map<String,Object> values = new LinkedHashMap<>(bindings); Object current = bind(prompt); if (current != null) values.put("_this", current);
                values.put("_id", AprDocument.string(prompt.get("id")));
                return stored(cel.createProgram(checked.getAst()).eval(values));
            } catch (Exception ignored) { return null; }
        }
    }

    private static CelType typeOf(Map<String,Object> prompt) {
        String type = AprDocument.string(map(prompt.get("hints")).get("expectedDataType"));
        return switch (type == null ? "" : type.toLowerCase(Locale.ROOT)) {
            case "number", "currency", "range" -> SimpleType.DOUBLE;
            case "boolean" -> SimpleType.BOOL;
            case "date", "time", "datetime" -> SimpleType.TIMESTAMP;
            case "multichoice" -> ListType.create(SimpleType.STRING);
            default -> SimpleType.STRING;
        };
    }
    private static Object bind(Map<String,Object> prompt) {
        String value = Optional.ofNullable(AprDocument.string(prompt.get("response"))).orElse("");
        String type = AprDocument.string(map(prompt.get("hints")).get("expectedDataType"));
        try {
            return switch (type == null ? "" : type.toLowerCase(Locale.ROOT)) {
                case "number", "currency", "range" -> value.trim().isEmpty() ? null : Double.valueOf(value.trim());
                case "boolean" -> bool(value);
                case "date" -> value.trim().isEmpty() ? null : LocalDate.parse(value.trim()).atStartOfDay().toInstant(java.time.ZoneOffset.UTC);
                case "datetime" -> value.trim().isEmpty() ? null : OffsetDateTime.parse(value.replace("Z", "+00:00")).toInstant();
                case "multichoice" -> Arrays.stream(value.contains("\n") ? value.split("\n") : value.split(",")).map(String::trim).filter(s -> !s.isEmpty()).toList();
                default -> value;
            };
        } catch (Exception ignored) { return null; }
    }
    private static Boolean bool(String value) { return switch (value.trim().toLowerCase(Locale.ROOT)) { case "true", "yes", "y", "1", "on", "x", "checked" -> true; case "false", "no", "n", "0", "off", "unchecked" -> false; default -> null; }; }
    private static String stored(Object value) {
        if (value == null) return "";
        if (value instanceof Boolean b) return b ? "true" : "false";
        if (value instanceof Number n) return Double.toString(n.doubleValue());
        if (value instanceof Instant instant) return instant.toString();
        if (value instanceof Iterable<?> values) { List<String> parts = new ArrayList<>(); for (Object item : values) parts.add(String.valueOf(item)); return String.join("\n", parts); }
        return String.valueOf(value);
    }
    @SuppressWarnings("unchecked") private static Map<String,Object> map(Object value) { return value instanceof Map<?,?> raw ? (Map<String,Object>) raw : new LinkedHashMap<>(); }
    @SuppressWarnings("unchecked") private static List<Map<String,Object>> prompts(List<Object> sections) { List<Map<String,Object>> out = new ArrayList<>(); for (Object item : sections) { Map<String,Object> section = (Map<String,Object>) item; for (Object prompt : (List<Object>) section.getOrDefault("prompts", List.of())) out.add((Map<String,Object>) prompt); out.addAll(prompts((List<Object>) section.getOrDefault("sections", List.of()))); } return out; }
    private static boolean blank(String value) { return value == null || value.trim().isEmpty(); }
}
