package org.promptresponse;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.*;

/**
 * The Java SDK's conformance driver.
 *
 * Answers {@code tests/Conformance/beta6/suite.json} (with the answers withheld)
 * using the shipped {@code org.promptresponse} classes -- the actual library a
 * caller depends on. See {@code docs/SDK_CONFORMANCE.md} for the driver contract
 * this implements, and {@code python/conformance_driver.py} /
 * {@code typescript/conformance-driver.mjs} for the sibling drivers this is
 * modeled on.
 *
 * <pre>
 *   java/run-conformance-driver.sh
 *   python3 scripts/run-conformance.py --driver "java/run-conformance-driver.sh"
 * </pre>
 *
 * Lives under {@code src/test}, not {@code src/main}: a driver over the SDK, the
 * same relationship the other SDKs' drivers have to their own packages, not a
 * class the published library ships.
 */
public final class AprConformanceDriver {
    private static final List<String> PROFILES = List.of("core", "core+streams", "core+expressions");
    private static final char RECORD_SEPARATOR = '';

    public static void main(String[] args) throws IOException {
        String input = new String(System.in.readAllBytes(), StandardCharsets.UTF_8);
        @SuppressWarnings("unchecked") Map<String, Object> suite = (Map<String, Object>) Json.parse(input);
        @SuppressWarnings("unchecked") List<Object> cases = (List<Object>) suite.get("cases");
        // A run that names fewer profiles gets an implementation claiming only those.
        @SuppressWarnings("unchecked") List<Object> run = (List<Object>) suite.getOrDefault("profiles", PROFILES);
        List<String> profiles = PROFILES.stream().filter(run::contains).toList();

        List<Object> results = new ArrayList<>();
        for (Object item : cases) {
            @SuppressWarnings("unchecked") Map<String, Object> testCase = (Map<String, Object>) item;
            if (!profiles.contains(testCase.get("profile"))) continue;
            results.add(answer(testCase, profiles));
        }

        Map<String, Object> implementation = new LinkedHashMap<>();
        implementation.put("name", "PromptResponse (Java)");
        implementation.put("version", suite.get("formatVersion"));
        implementation.put("profiles", profiles);
        Map<String, Object> output = new LinkedHashMap<>();
        output.put("implementation", implementation);
        output.put("results", results);
        System.out.print(Json.write(output));
    }

    private static Map<String, Object> answer(Map<String, Object> testCase, List<String> profiles) {
        String id = (String) testCase.get("id");
        String representationName = (String) testCase.get("representation");
        AprBeta6.Representation representation = representationName.startsWith("yaml")
            ? AprBeta6.Representation.YAML : AprBeta6.Representation.JSONC;
        String document = (String) testCase.get("document");

        if ("jsonc-stream".equals(representationName) && document.indexOf(RECORD_SEPARATOR) < 0) {
            // Two JSON texts with no record separator between them parse as one
            // document, silently losing the second -- refused before parsing gets
            // the chance to merge them.
            return reject(id, "APR_STREAM_MISSING_RECORD_SEPARATOR");
        }

        List<AprBeta6.Record> records;
        try {
            // Without core+streams a caller asks for one form, and the SDK refuses a
            // stream rather than choose a record from it. [APR-CONF-001]
            if (!profiles.contains("core+streams")) AprBeta6.readForm(document, representation);
            records = AprBeta6.readStream(document, representation);
        } catch (RuntimeException failure) {
            return reject(id, diagnostic(failure));
        }
        if (records.isEmpty()) return reject(id, "NULL_DOCUMENT");

        // Attestation records are structurally validated during parsing itself
        // (AprBeta6.validateAttestation); only form records need the separate
        // validate() pass.
        List<ValidationIssue> allErrors = new ArrayList<>();
        for (AprBeta6.Record record : records) {
            if (record instanceof AprBeta6.FormRecord form) allErrors.addAll(Apr.validate(form.document()).errors());
        }
        if (!allErrors.isEmpty()) return reject(id, allErrors.get(0).code());

        AprBeta6.Record first = records.getFirst();
        List<String> warnings = List.of();
        if (first instanceof AprBeta6.FormRecord form) {
            TreeSet<String> codes = new TreeSet<>();
            for (ValidationIssue warning : Apr.validate(form.document()).warnings()) codes.add(warning.code());
            warnings = List.copyOf(codes);
        }

        Map<String, Object> result = new LinkedHashMap<>();
        result.put("id", id);
        result.put("outcome", "valid");
        result.put("digest", digestOf(first));
        result.put("warnings", warnings);

        if ((testCase.get("evaluates") != null || testCase.get("expects") != null) && first instanceof AprBeta6.FormRecord form) {
            try {
                @SuppressWarnings("unchecked") Map<String, Object> inputs = (Map<String, Object>) testCase.get("evaluate");
                result.put("evaluated", evaluate(form.document(), inputs == null ? Map.of() : inputs));
            } catch (RuntimeException failure) {
                result.put("evaluated", Map.of("error", failure.getClass().getSimpleName()));
            }
        }

        if (Boolean.TRUE.equals(testCase.get("roundTrip"))) result.put("written", written(records));

        return result;
    }

    private static Map<String, Object> reject(String id, String diagnostic) {
        Map<String, Object> result = new LinkedHashMap<>();
        result.put("id", id);
        result.put("outcome", "reject");
        result.put("diagnostic", diagnostic);
        return result;
    }

    private static String diagnostic(RuntimeException failure) {
        return failure instanceof AprException apr && apr.code() != null ? apr.code() : failure.getClass().getSimpleName();
    }

    private static String digestOf(AprBeta6.Record record) {
        // record.value() is the document exactly as parsed -- an absent optional
        // member stays absent. AprDocument.toJson() goes through the parsed tree
        // too in this SDK (there is no separate typed model to diverge from it),
        // so both readings agree; value() is used directly since it is already at
        // hand and mirrors the other two drivers' comparable reading.
        Map<String, Object> value = record instanceof AprBeta6.FormRecord form
            ? (form.value() != null ? form.value() : cast(Json.parse(form.document().toJson())))
            : ((AprBeta6.AttestationRecord) record).value();
        return AprBeta6Integrity.digest(value);
    }

    /** Order matters: hidden/validation see only the responses as read, then computed values (exprValue) fill in. */
    private static Map<String, Object> evaluate(AprDocument document, Map<String, Object> inputs) {
        Object todayInput = inputs.containsKey("_today") ? inputs.get("_today") : inputs.get("_now");
        String today = todayInput instanceof String text ? text : null;
        @SuppressWarnings("unchecked") Map<String, String> ctx = (Map<String, String>) inputs.get("ctx");

        AprExpressions.Context context = new AprExpressions.Context(document, today, ctx);
        Map<String, Object> hidden = new LinkedHashMap<>();
        Map<String, Object> expected = new LinkedHashMap<>();
        Map<String, Object> readOnly = new LinkedHashMap<>();
        Map<String, Object> validation = new LinkedHashMap<>();
        for (Map<String, Object> prompt : allPrompts(document.sections())) {
            String id = AprDocument.string(prompt.get("id"));
            Map<String, Object> hints = hintsOf(prompt);
            String exprHidden = AprDocument.string(hints.get("exprHidden"));
            if (!blank(exprHidden)) hidden.put(id, Boolean.TRUE.equals(context.evaluateRaw(prompt, exprHidden)));
            String exprExpected = AprDocument.string(hints.get("exprExpected"));
            if (!blank(exprExpected)) expected.put(id, Boolean.TRUE.equals(context.evaluateRaw(prompt, exprExpected)));
            String exprReadOnly = AprDocument.string(hints.get("exprReadOnly"));
            if (!blank(exprReadOnly)) readOnly.put(id, Boolean.TRUE.equals(context.evaluateRaw(prompt, exprReadOnly)));
            String exprValidation = AprDocument.string(hints.get("exprValidation"));
            if (!blank(exprValidation)) {
                Object raw = context.evaluateRaw(prompt, exprValidation);
                validation.put(id, raw instanceof String message ? message : "");
            }
        }

        AprExpressions.recomputeComputedValues(document, today, ctx);
        Map<String, Object> responses = new LinkedHashMap<>();
        for (Map<String, Object> prompt : allPrompts(document.sections())) {
            String exprValue = AprDocument.string(hintsOf(prompt).get("exprValue"));
            if (!blank(exprValue)) responses.put(AprDocument.string(prompt.get("id")), Optional.ofNullable(AprDocument.string(prompt.get("response"))).orElse(""));
        }

        Map<String, Object> result = new LinkedHashMap<>();
        result.put("responses", responses);
        result.put("hidden", hidden);
        result.put("expected", expected);
        result.put("readOnly", readOnly);
        result.put("validation", validation);
        return result;
    }

    /**
     * Given a form record that still carries the raw value AprBeta6.readStream
     * attached, writeStream echoes that raw text back rather than re-serializing
     * the document -- a legitimate preservation path, but one that would let this
     * driver claim a round trip without exercising the SDK's own writer at all.
     * Clearing it forces the writeForm/writeStream path over AprDocument.toJson(),
     * which is the one part of this contract that tests writing.
     */
    private static String written(List<AprBeta6.Record> records) {
        List<AprBeta6.Record> typed = new ArrayList<>();
        for (AprBeta6.Record record : records) {
            typed.add(record instanceof AprBeta6.FormRecord form ? new AprBeta6.FormRecord(form.document(), null) : record);
        }
        if (typed.size() == 1 && typed.getFirst() instanceof AprBeta6.FormRecord form) return AprBeta6.writeForm(form.document(), AprBeta6.Representation.JSONC);
        return AprBeta6.writeStream(typed, AprBeta6.Representation.JSONC);
    }

    @SuppressWarnings("unchecked")
    private static List<Map<String, Object>> allPrompts(List<Object> sections) {
        List<Map<String, Object>> out = new ArrayList<>();
        for (Object item : sections) {
            Map<String, Object> section = (Map<String, Object>) item;
            for (Object prompt : (List<Object>) section.getOrDefault("prompts", List.of())) out.add((Map<String, Object>) prompt);
            out.addAll(allPrompts((List<Object>) section.getOrDefault("sections", List.of())));
        }
        return out;
    }
    @SuppressWarnings("unchecked") private static Map<String, Object> hintsOf(Map<String, Object> prompt) {
        return prompt.get("hints") instanceof Map<?, ?> hints ? (Map<String, Object>) hints : Map.of();
    }
    @SuppressWarnings("unchecked") private static Map<String, Object> cast(Object value) { return (Map<String, Object>) value; }
    private static boolean blank(String value) { return value == null || value.isBlank(); }

    private AprConformanceDriver() { }
}
