package org.promptresponse;

import java.nio.file.*;

/** Dependency-free corpus runner, usable with just javac/java. */
public final class AprConformanceTest {
    public static void main(String[] args) throws Exception {
        expressionBinding();
        beta6();
        forbiddenCodePoints();
        versionPresence();
        jcsNumbers();
        beta6Corpus();
        specificationExamples();
        expressionActivation();
        validationVocabulary();
        expressionEdgeCases();
        System.out.println("Java APR beta.6 conformance passed");
    }

    /** APR-REP-004: a forbidden code point is a parse failure while reading, never a crash later. */
    private static void forbiddenCodePoints() {
        String prefix = "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"a";
        String suffix = "b\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\"}]}]}";
        for (String escaped : new String[] { "\\u0000", "\\u0007", "\\ud800", "\\udc00" }) {
            expectParseError(prefix + escaped + suffix, escaped);
        }
        expectParseError("{\"aprVersion\":\"1.0-beta.6\",\"x.a\\u0001b\":1,\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\"}]}]}", "a member name");
        AprDocument kept = AprBeta6.readForm(prefix + "\\t\\r\\n \\ud83d\\ude00" + suffix, AprBeta6.Representation.JSONC);
        if (!("a\t\r\n 😀b").equals(kept.metadata().get("title"))) throw new AssertionError("tab, line breaks and a surrogate pair must be read: " + kept.metadata().get("title"));
    }

    /** An absent or blank aprVersion states no version: REQUIRED_FIELD, never UNSUPPORTED_VERSION. */
    private static void versionPresence() {
        String rest = "\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\"}]}]}";
        expectCode("{" + rest, "REQUIRED_FIELD", "an absent aprVersion");
        expectCode("{\"aprVersion\":\"\"," + rest, "REQUIRED_FIELD", "a blank aprVersion");
        expectCode("{\"aprVersion\":\"  \"," + rest, "REQUIRED_FIELD", "a whitespace aprVersion");
        expectCode("{\"aprVersion\":\"2.0\"," + rest, "UNSUPPORTED_VERSION", "a stated version this reader does not accept");
    }

    private static void expectCode(String source, String code, String what) {
        try {
            AprBeta6.readStream(source, AprBeta6.Representation.JSONC);
        } catch (AprException refused) {
            if (!code.equals(refused.code())) throw new AssertionError(what + " must be refused as " + code + ", not " + refused.code());
            return;
        }
        throw new AssertionError(what + " must be refused while reading");
    }

    private static void expectParseError(String source, String what) {
        try {
            AprBeta6.readStream(source, AprBeta6.Representation.JSONC);
        } catch (AprException refused) {
            if (!"PARSE_ERROR".equals(refused.code())) throw new AssertionError(what + " must be refused as PARSE_ERROR, not " + refused.code());
            return;
        }
        throw new AssertionError(what + " must be refused while reading");
    }

    /** The text floor, confusable-script-mix, and table-shape checks added while building AprConformanceDriver. */
    private static void validationVocabulary() {
        String decomposed = "Café"; // "Café" spelled with a combining acute accent
        AprDocument nonNfc = Apr.parse("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"" + decomposed + "\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\"}]}]}");
        if (!decomposed.equals(nonNfc.metadata().get("title"))) throw new AssertionError("a reader must preserve human-facing text exactly, never rewrite it");
        if (!codes(Apr.validate(nonNfc).warnings()).equals(java.util.List.of("NON_NFC_TEXT"))) throw new AssertionError("non-NFC title must be reported, not silently cleaned: " + codes(Apr.validate(nonNfc).warnings()));

        AprDocument hidden = Apr.parse("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"Permit​application\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\"}]}]}");
        if (!codes(Apr.validate(hidden).warnings()).equals(java.util.List.of("FORBIDDEN_CODE_POINT"))) throw new AssertionError("hidden zero-width space must be reported");

        AprDocument confusable = Apr.parse("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"Pаypal Permit\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\"}]}]}"); // Cyrillic а (U+0430) hidden in a Latin title
        if (!codes(Apr.validate(confusable).warnings()).equals(java.util.List.of("CONFUSABLE_SCRIPT_MIX"))) throw new AssertionError("Latin/Cyrillic mix must be reported");

        AprDocument legitimate = Apr.parse("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"Toyota パーツ Order Form\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\"}]}]}"); // Katakana パーツ ("parts")
        if (!Apr.validate(legitimate).warnings().isEmpty()) throw new AssertionError("Latin+Katakana is not a confusable mix: " + codes(Apr.validate(legitimate).warnings()));

        // APR-TEXT-010: an id outside [A-Za-z0-9_.-] warns, on a section, a prompt or a role.
        AprDocument spaced = Apr.parse("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"roles\":[{\"id\":\"the notary\",\"name\":\"N\"}],\"sections\":[{\"id\":\"the applicant\",\"title\":\"S\",\"prompts\":[{\"id\":\"first name\",\"label\":\"P\"}]}]}");
        long idWarnings = Apr.validate(spaced).warnings().stream().filter(w -> "ID_FORBIDDEN_CHARACTER".equals(w.code())).count();
        if (idWarnings != 3) throw new AssertionError("a section, a prompt and a role id with a space must each warn ID_FORBIDDEN_CHARACTER, got " + codes(Apr.validate(spaced).warnings()));
        AprDocument machineKeys = Apr.parse("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"applicant.Details-2\",\"title\":\"S\",\"prompts\":[{\"id\":\"first_name.v2-A\",\"label\":\"P\"}]}]}");
        if (codes(Apr.validate(machineKeys).warnings()).contains("ID_FORBIDDEN_CHARACTER")) throw new AssertionError("ids using only [A-Za-z0-9_.-] must not warn");

        AprDocument unregistered = Apr.parse("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\",\"hints\":{\"expectedDataType\":\"carrier-pigeon\"}}]}]}");
        if (!codes(Apr.validate(unregistered).warnings()).equals(java.util.List.of("UNREGISTERED_DATA_TYPE"))) throw new AssertionError("an unregistered expectedDataType is a warning, not a rejection");

        AprDocument ragged = Apr.parse("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"t\",\"title\":\"T\",\"kind\":\"table\",\"sections\":["
            + "{\"id\":\"r1\",\"title\":\"R1\",\"prompts\":[{\"id\":\"r1.a\",\"label\":\"A\"},{\"id\":\"r1.b\",\"label\":\"B\"}]},"
            + "{\"id\":\"r2\",\"title\":\"R2\",\"prompts\":[{\"id\":\"r2.a\",\"label\":\"A\"}]}]}]}");
        // A ragged row also can't have its labels compared meaningfully against the first row, so both codes fire.
        if (!codes(Apr.validate(ragged).warnings()).equals(java.util.List.of("TABLE_LABEL_MISMATCH", "TABLE_RAGGED"))) throw new AssertionError("mismatched table row length must report TABLE_RAGGED and TABLE_LABEL_MISMATCH: " + codes(Apr.validate(ragged).warnings()));

        AprDocument emptyTable = Apr.parse("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"t\",\"title\":\"T\",\"kind\":\"table\",\"sections\":[]}]}");
        ValidationResult tableResult = Apr.validate(emptyTable);
        if (tableResult.isValid() || !codes(tableResult.errors()).equals(java.util.List.of("EMPTY_TABLE"))) throw new AssertionError("an empty table section is a structural error");

        // docs/BETA6_WIRE_DELTA.md types maxRows as "integer, at least 1"; .NET's
        // model declares it C# int?, so a fractional JSON number fails to
        // deserialize at all. 5.0 and 5 are the same integer and both still
        // parse; only a genuinely fractional value is refused (issue #378).
        try { Apr.parse(table(2.5)); throw new AssertionError("maxRows 2.5 must be refused as WRONG_TYPE"); }
        catch (AprException expected) { if (!"WRONG_TYPE".equals(expected.code())) throw expected; }
        Apr.parse(table(5.0));
        for (double zeroOrNegative : new double[] { 0, -3 }) {
            AprDocument capped = Apr.parse(table(zeroOrNegative));
            if (!codes(Apr.validate(capped).errors()).equals(java.util.List.of("WRONG_TYPE"))) throw new AssertionError("maxRows " + zeroOrNegative + " must be a validation error");
        }

        System.out.println("Java validation vocabulary passed");
    }
    private static java.util.List<String> codes(java.util.List<ValidationIssue> issues) {
        return issues.stream().map(ValidationIssue::code).sorted().toList();
    }
    private static String table(double maxRows) {
        return "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"t\",\"title\":\"T\",\"kind\":\"table\",\"maxRows\":" + maxRows
            + ",\"sections\":[{\"id\":\"r\",\"title\":\"R\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\"}]}]}]}";
    }

    /** CEL bugs found while building AprConformanceDriver, all masked by Context's blanket catch. */
    private static void expressionEdgeCases() {
        // A prompt named ctx does not break the reserved ctx binding (addVar throws on
        // a second registration for one name rather than letting the later, reserved
        // registration win).
        AprDocument ctxDocument = Apr.parse("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":["
            + "{\"id\":\"ctx\",\"label\":\"Ctx\",\"response\":\"a prompt, not the context\",\"hints\":{\"expectedDataType\":\"text\"}},"
            + "{\"id\":\"org\",\"label\":\"Org\",\"hints\":{\"expectedDataType\":\"text\",\"exprValue\":\"ctx.org\"}}]}]}");
        var context = new AprExpressions.Context(ctxDocument, null, java.util.Map.of("org", "Skeptical Engineering"));
        var orgPrompt = ((java.util.List<?>) ((java.util.Map<?,?>) ctxDocument.sections().get(0)).get("prompts")).get(1);
        if (!"Skeptical Engineering".equals(context.evaluate((java.util.Map<String,Object>) orgPrompt, "ctx.org")))
            throw new AssertionError("a field named ctx must not break the reserved ctx binding");

        // The CEL strings extension (trim, split, join, ...) is not part of the standard
        // library the specification requires, and dev.cel provides it unconditionally.
        AprDocument trimDocument = Apr.parse("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":["
            + "{\"id\":\"n\",\"label\":\"N\",\"response\":\"Ada\",\"hints\":{\"expectedDataType\":\"text\"}},"
            + "{\"id\":\"trimmed\",\"label\":\"Trimmed\",\"hints\":{\"expectedDataType\":\"text\",\"exprValue\":\"n.trim()\"}}]}]}");
        var trimContext = new AprExpressions.Context(trimDocument, null, null);
        var trimmedPrompt = (java.util.Map<String,Object>) ((java.util.List<?>) ((java.util.Map<?,?>) trimDocument.sections().get(0)).get("prompts")).get(1);
        if (trimContext.evaluateRaw(trimmedPrompt, "n.trim()") != null) throw new AssertionError("a CEL extension function must be refused, not evaluated");

        // exprValidation is typed string; "2 + 2" evaluating to the number 4 is the
        // same failure as a compile error, not a value to stringify into "4".
        AprDocument validationDocument = Apr.parse("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"h\",\"label\":\"H\",\"hints\":{\"exprValidation\":\"2 + 2\"}}]}]}");
        var validationContext = new AprExpressions.Context(validationDocument, null, null);
        var hPrompt = (java.util.Map<String,Object>) ((java.util.List<?>) ((java.util.Map<?,?>) validationDocument.sections().get(0)).get("prompts")).get(0);
        Object raw = validationContext.evaluateRaw(hPrompt, "2 + 2");
        if (raw instanceof String) throw new AssertionError("a numeric exprValidation result must not be treated as a validation message");

        // size(n) is a CEL int; the stored response must read "3", not "3.0".
        AprDocument lengthDocument = Apr.parse("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":["
            + "{\"id\":\"n\",\"label\":\"N\",\"response\":\"Ada\",\"hints\":{\"expectedDataType\":\"text\"}},"
            + "{\"id\":\"len\",\"label\":\"Len\",\"hints\":{\"expectedDataType\":\"number\",\"exprValue\":\"double(size(n))\"}}]}]}");
        AprExpressions.recomputeComputedValues(lengthDocument);
        var lenPrompt = (java.util.Map<String,Object>) ((java.util.List<?>) ((java.util.Map<?,?>) lengthDocument.sections().get(0)).get("prompts")).get(1);
        if (!"3".equals(lenPrompt.get("response"))) throw new AssertionError("a computed numeric response must be spelled '3', not '3.0': " + lenPrompt.get("response"));

        // A field whose response does not parse as its expectedDataType is declared but
        // unbound; dev.cel resolves that into a CelUnknownSet rather than throwing.
        AprDocument unknownDocument = Apr.parse("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":["
            + "{\"id\":\"qty\",\"label\":\"Qty\",\"response\":\"about twelve\",\"hints\":{\"expectedDataType\":\"number\"}},"
            + "{\"id\":\"cost\",\"label\":\"Cost\",\"hints\":{\"expectedDataType\":\"number\",\"exprValue\":\"qty * 2.0\"}}]}]}");
        AprExpressions.recomputeComputedValues(unknownDocument);
        var costPrompt = (java.util.Map<String,Object>) ((java.util.List<?>) ((java.util.Map<?,?>) unknownDocument.sections().get(0)).get("prompts")).get(1);
        if (costPrompt.get("response") != null && !"".equals(costPrompt.get("response"))) throw new AssertionError("an unparsed field must not leak CelUnknownSet's toString() as a computed value: " + costPrompt.get("response"));

        System.out.println("Java expression edge cases passed");
    }

    /**
     * Runs the executable examples embedded in the specification.
     *
     * The vectors are generated from docs/APR_SPECIFICATION.md, so these are the
     * specification's own claims rather than a separately authored suite. Where an
     * example and this reader disagree, the specification is normative and the
     * reader has the defect.
     */
    private static void specificationExamples() throws Exception {
        Path vectors = Path.of("..", "tests", "Conformance", "beta6", "spec-examples.json");
        String raw = Files.readString(vectors);
        Object parsed = Json.parse(raw);
        if (!(parsed instanceof java.util.Map<?, ?> root)) throw new AssertionError("spec-examples.json is not an object");
        if (!(root.get("examples") instanceof java.util.List<?> examples) || examples.isEmpty())
            throw new AssertionError("spec-examples.json carries no examples");

        java.util.List<String> failures = new java.util.ArrayList<>();
        int checked = 0;
        for (Object entry : examples) {
            java.util.Map<?, ?> example = (java.util.Map<?, ?>) entry;
            String id = String.valueOf(example.get("id"));
            String rule = String.valueOf(example.get("rule"));
            String representation = String.valueOf(example.get("representation"));
            String expect = String.valueOf(example.get("expect"));
            String document = String.valueOf(example.get("document"));
            AprBeta6.Representation form = representation.startsWith("yaml")
                ? AprBeta6.Representation.YAML : AprBeta6.Representation.JSONC;
            // An attestation record belongs to core+attestations, which this SDK does not
            // claim. The conformance runner leaves such a case unanswered, and so does this.
            if (!representation.endsWith("-stream") && (document.contains("\"recordType\"") || document.contains("recordType:")))
                continue;
            checked++;

            boolean accepted;
            String detail = "";
            java.util.List<String> codes = new java.util.ArrayList<>();
            try {
                // Rejection is a refused read or a form that fails validation: a missing
                // label parses and is an error, as the conformance driver reports it.
                java.util.List<AprDocument> forms = new java.util.ArrayList<>();
                if (representation.endsWith("-stream")) {
                    for (AprBeta6.Record record : AprBeta6.readStream(form == AprBeta6.Representation.JSONC ? framed(document) : document, form))
                        if (record instanceof AprBeta6.FormRecord formRecord) forms.add(formRecord.document());
                } else forms.add(AprBeta6.readForm(document, form));
                for (AprDocument read : forms) for (ValidationIssue issue : Apr.validate(read).errors()) codes.add(issue.code());
                // A read that yields no form holds no document, which is refused too (NULL_DOCUMENT).
                if (forms.isEmpty()) codes.add("NULL_DOCUMENT");
                accepted = codes.isEmpty();
                if (!accepted) detail = String.join(", ", codes);
            } catch (RuntimeException rejected) {
                accepted = false;
                detail = String.valueOf(rejected.getMessage());
                if (rejected instanceof AprException refusal && refusal.code() != null) codes.add(refusal.code());
            }

            if ("valid".equals(expect) && !accepted)
                failures.add(id + " (#" + rule + "): specification says valid, it was rejected — " + detail);
            // The code has to be the one the example names, or a reader refusing for an
            // unrelated reason would pass.
            String diagnostic = String.valueOf(example.get("diagnostic"));
            if ("reject".equals(expect) && accepted)
                failures.add(id + " (#" + rule + "): specification requires rejection, reader accepted it");
            else if ("reject".equals(expect) && !codes.contains(diagnostic))
                failures.add(id + " (#" + rule + "): specification names " + diagnostic + ", reader reported "
                    + (codes.isEmpty() ? "no code — " + detail : String.join(", ", codes)));
        }
        if (!failures.isEmpty()) throw new AssertionError("specification examples failed:\n  " + String.join("\n  ", failures));
        System.out.println("Java specification examples passed: " + checked + " of " + examples.size());
    }

    /** The activation the specification defines, and its caller-supplied instants. */
    private static void expressionActivation() {
        String json = "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},"
            + "\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":["
            + "{\"id\":\"echo_id\",\"label\":\"E\",\"response\":\"\"}"
            + "]}]}";
        AprDocument document = Apr.parse(json);
        java.util.Map<String,Object> prompt = new java.util.LinkedHashMap<>();
        prompt.put("id", "echo_id");

        var supplied = new AprExpressions.Context(
            document, "2026-09-01T12:00:00Z", java.util.Map.of("team", "records"));
        if (!"echo_id".equals(supplied.evaluate(prompt, "_id")))
            throw new AssertionError("_id did not bind");
        if (!"2026-09-01".equals(supplied.evaluate(prompt, "_today")))
            throw new AssertionError("_today did not bind as a date string");
        if (!"records".equals(supplied.evaluate(prompt, "ctx['team']")))
            throw new AssertionError("ctx did not bind");

        // With nothing supplied the name is unbound, and the expression degrades
        // rather than silently using the host clock.
        var unsupplied = new AprExpressions.Context(document, null, null);
        if (unsupplied.evaluate(prompt, "_today") != null)
            throw new AssertionError("_today bound without a caller-supplied instant");
        System.out.println("Java expression activation passed");
    }

    private static void expressionBinding() {
        try { Apr.parse("{\"aprVersion\":\"1.0-beta\",\"metadata\":{\"title\":\"T\"},\"sections\":[]}"); throw new AssertionError("legacy APR version was accepted"); } catch (AprException expected) { }
        AprDocument document=Apr.parse("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"qty\",\"label\":\"Quantity\",\"response\":\"3\",\"hints\":{\"expectedDataType\":\"number\"}},{\"id\":\"price\",\"label\":\"Price\",\"response\":\"12.5\",\"hints\":{\"expectedDataType\":\"currency\"}},{\"id\":\"total\",\"label\":\"Total\",\"response\":\"\",\"hints\":{\"expectedDataType\":\"currency\",\"exprValue\":\"qty * price\"}}]}]}");
        if (!AprExpressions.recomputeComputedValues(document) || !document.toJson().contains("\"response\":\"37.5\"")) throw new AssertionError("CEL binding did not compute typed value");
        document.setResponse("total", "40");
        if (AprExpressions.recomputeComputedValues(document) || !document.toJson().contains("\"response\":\"40\"")) throw new AssertionError("CEL binding overwrote human correction");
    }
    private static void beta6() {
        String form = "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\",\"response\":\"Ada\"}]}]}";
        AprDocument jsonc = AprBeta6.readForm("// comment\n" + form.substring(0, form.length() - 1) + ",}", AprBeta6.Representation.JSONC);
        AprDocument yaml = AprBeta6.readForm(AprBeta6.writeForm(jsonc, AprBeta6.Representation.YAML), AprBeta6.Representation.YAML);
        if (!"Ada".equals(((java.util.Map<?,?>)((java.util.List<?>)((java.util.Map<?,?>)yaml.sections().getFirst()).get("prompts")).getFirst()).get("response"))) throw new AssertionError("beta.6 YAML changed a response");
        // An anchor, alias or tag is a node property (specification 4.5): "&", "*" and "!" inside a plain scalar's content are ordinary characters.
        String yamlForm = "aprVersion: \"1.0-beta.6\"\nmetadata:\n  title: T\n  \"<<\": not a merge key\nsections:\n  - id: s\n    title: S\n    prompts:\n      - id: p\n        label: P\n        hints:\n          exprValue: string(fee_count * 8.0)\n        response: a * b & c! d\n";
        var yamlPrompt = (java.util.Map<?,?>)((java.util.List<?>)((java.util.Map<?,?>)((java.util.List<?>)((AprBeta6.FormRecord)AprBeta6.readStream(yamlForm, AprBeta6.Representation.YAML).getFirst()).value().get("sections")).getFirst()).get("prompts")).getFirst();
        if (!"string(fee_count * 8.0)".equals(((java.util.Map<?,?>)yamlPrompt.get("hints")).get("exprValue")) || !"a * b & c! d".equals(yamlPrompt.get("response"))) throw new AssertionError("beta.6 YAML did not read indicator characters inside a plain scalar as content");
        String[][] excluded = {
            { yamlForm.replace("response: a", "response: &r a"), "anchors" }, { yamlForm.replace("response: a * b & c! d", "response: *r"), "aliases" },
            { yamlForm.replace("response: a", "response: !!str a"), "tags" }, { yamlForm.replace("response: a", "response: ! a"), "tags" },
            { yamlForm.replace("    title: S\n", "    title: S\n    <<: {description: merged}\n"), "merge keys" }, { yamlForm.replace("response: a * b & c! d", "response: {<<: {b: 1}}"), "merge keys" },
            { "%YAML 1.2\n---\n" + yamlForm, "directives" }, { "%TAG !e! tag:example.com,2000:\n---\n" + yamlForm, "directives" },
            // A directive belongs to the document that follows it, wherever that is in the stream.
            { yamlForm + "...\n%TAG !e! tag:example.com,2000:\n---\n" + yamlForm, "directives" } };
        for (String[] excludedCase : excluded) {
            try { AprBeta6.readStream(excludedCase[0], AprBeta6.Representation.YAML); throw new AssertionError("beta.6 YAML accepted an excluded construct in:\n" + excludedCase[0]); }
            catch (AprException expected) { if (!expected.getMessage().contains(excludedCase[1])) throw new AssertionError("beta.6 YAML rejected " + excludedCase[1] + " for another reason: " + expected.getMessage()); }
        }
        String attestation = "{\"recordType\":\"attestation\",\"aprVersion\":\"1.0-beta.6\",\"subject\":{\"digest\":\"sha256:0000000000000000000000000000000000000000000000000000000000000000\",\"canonicalization\":\"jcs-sha256\"},\"scope\":{\"kind\":\"document\"},\"manifest\":{\"root\":\"sha256:0000000000000000000000000000000000000000000000000000000000000000\",\"entries\":[]},\"proofs\":[],\"witnesses\":[]}";
        String stream = "\u001e" + attestation + "\n\u001e" + form + "\n\u001e" + form;
        java.util.List<AprBeta6.Record> records = AprBeta6.readStream(stream, AprBeta6.Representation.JSONC);
        if (records.size() != 3 || records.stream().filter(record -> record instanceof AprBeta6.FormRecord).count() != 2) throw new AssertionError("beta.6 stream lost an occurrence");
        try { AprBeta6.readForm(stream, AprBeta6.Representation.JSONC); throw new AssertionError("stream selected a record implicitly"); } catch (AprException expected) { if (!"APR_STREAM_REQUIRES_ITERATION".equals(expected.code())) throw expected; }
        try { AprBeta6.readForm(form.replace("\"metadata\":", "\"metadata\":{},\"metadata\":"), AprBeta6.Representation.JSONC); throw new AssertionError("beta.6 accepted duplicate JSONC members"); } catch (AprException expected) { }
        Object value = Json.parse(form);
        if (!"sha256:b944624a9883f7317f9415090804ddea080806c28ff531664d7d63a66cef50a2".equals(AprBeta6Integrity.digest(value))) throw new AssertionError("beta.6 digest is not representation-neutral");
        var manifest = AprBeta6Integrity.createManifest(value);
        var assertion = new java.util.LinkedHashMap<String,Object>(); assertion.put("recordType","attestation"); assertion.put("aprVersion","1.0-beta.6"); assertion.put("subject", java.util.Map.of("digest",manifest.root(),"canonicalization","jcs-sha256")); assertion.put("scope",java.util.Map.of("kind","document")); assertion.put("manifest",java.util.Map.of("root",manifest.root(),"entries",manifest.entries().stream().map(entry->java.util.Map.of("path",entry.path(),"digest",entry.digest())).toList())); assertion.put("proofs",java.util.List.of()); assertion.put("witnesses",java.util.List.of());
        if (!"unverifiable".equals(AprBeta6Integrity.resolve(java.util.List.of(new AprBeta6.FormRecord(AprBeta6.readForm(form,AprBeta6.Representation.JSONC)),new AprBeta6.AttestationRecord(assertion))).getFirst().state())) throw new AssertionError("unsigned beta.6 attestation must be unverifiable");
        var fieldManifest = new java.util.LinkedHashMap<String,Object>(); fieldManifest.put("root",manifest.root()); fieldManifest.put("entries",manifest.entries().stream().filter(entry -> !entry.path().equals("/sections/0/prompts/0/response")).map(entry->java.util.Map.of("path",entry.path(),"digest",entry.digest())).toList()); assertion.put("scope",java.util.Map.of("kind","fields","fields",java.util.List.of("p"))); assertion.put("manifest",fieldManifest);
        if (!"invalid".equals(AprBeta6Integrity.resolve(java.util.List.of(new AprBeta6.FormRecord(AprBeta6.readForm(form,AprBeta6.Representation.JSONC)),new AprBeta6.AttestationRecord(assertion))).getFirst().state())) throw new AssertionError("fields scope without response must be invalid");
    }
    /**
     * Restores RFC 7464 framing to an APR-JSONC stream the specification prints with {@code ---}.
     *
     * A record separator is invisible on a page, so the specification stands one in with a
     * {@code ---} line. Handing that prose form straight to the reader tests the reader against a
     * document the format never defines. APR-YAML streams are untouched: there {@code ---} is
     * genuinely the separator, not a stand-in for one.
     *
     * Mirrors {@code Framed()} in tests/PromptResponse.Core.Tests/Beta6/SpecExampleTests.cs.
     */
    private static String framed(String document) {
        StringBuilder framed = new StringBuilder();
        for (String part : document.split("(?m)^---$")) {
            if (part.isBlank()) continue;
            framed.append('\u001e').append(part.strip()).append('\n');
        }
        return framed.toString();
    }

    /** RFC 8785 Appendix B number vectors, plus the representation-neutral digest pinned across SDKs. */
    private static void jcsNumbers() {
        String[][] vectors = {
            {"0000000000000000", "0"}, {"8000000000000000", "0"}, {"0000000000000001", "5e-324"}, {"0000000000000002", "1e-323"}, {"8000000000000001", "-5e-324"},
            {"7fefffffffffffff", "1.7976931348623157e+308"}, {"ffefffffffffffff", "-1.7976931348623157e+308"},
            {"4340000000000000", "9007199254740992"}, {"c340000000000000", "-9007199254740992"}, {"4430000000000000", "295147905179352830000"},
            {"44b52d02c7e14af5", "9.999999999999997e+22"}, {"44b52d02c7e14af6", "1e+23"}, {"44b52d02c7e14af7", "1.0000000000000001e+23"},
            {"444b1ae4d6e2ef4e", "999999999999999700000"}, {"444b1ae4d6e2ef4f", "999999999999999900000"}, {"444b1ae4d6e2ef50", "1e+21"},
            {"3eb0c6f7a0b5ed8c", "9.999999999999997e-7"}, {"3eb0c6f7a0b5ed8d", "0.000001"},
            {"41b3de4355555553", "333333333.3333332"}, {"41b3de4355555554", "333333333.33333325"}, {"41b3de4355555555", "333333333.3333333"},
            {"41b3de4355555556", "333333333.3333334"}, {"41b3de4355555557", "333333333.33333343"},
            {"becbf647612f3696", "-0.0000033333333333333333"}, {"43143ff3c1cb0959", "1424953923781206.2"},
        };
        for (String[] vector : vectors) {
            String actual = AprBeta6Integrity.canonicalNumber(Double.longBitsToDouble(Long.parseUnsignedLong(vector[0], 16)));
            if (!vector[1].equals(actual)) throw new AssertionError("JCS number " + vector[0] + ": expected " + vector[1] + " but was " + actual);
        }
        if (!"{\"canAddRows\":true,\"maxRows\":5,\"min\":1996}".equals(AprBeta6Integrity.canonicalize(Json.parse("{\"maxRows\":5.0,\"min\":1.996e3,\"canAddRows\":true}"))))
            throw new AssertionError("JCS must write integral numbers without a fractional part");
        if (!AprBeta6Integrity.digest(Json.parse("{\"maxRows\":5}")).equals(AprBeta6Integrity.digest(Json.parse("{\"maxRows\":5.0}"))))
            throw new AssertionError("JCS digest depends on the spelling of an integral number");
        try { AprBeta6Integrity.canonicalize(Double.POSITIVE_INFINITY); throw new AssertionError("non-finite number was canonicalized"); } catch (AprException expected) { }
        String expected = "sha256:df7259065a4e63df08be70e66bfe7c85412e42a3af6d477ccab2d285a62c9fa8";
        String jsonc = "{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\",\"response\":\"\",\"com.example.canAddRows\":true,\"com.example.maxRows\":5,\"com.example.min\":1996,\"com.example.step\":0.5,\"com.example.scale\":1e21,\"com.example.epsilon\":1e-7}]}]}";
        String yaml = String.join("\n",
            "aprVersion: \"1.0-beta.6\"", "metadata: { title: T }", "sections:", "  - id: s", "    title: S", "    prompts:",
            "      - id: p", "        label: P", "        response: \"\"", "        com.example.canAddRows: true", "        com.example.maxRows: 5",
            "        com.example.min: 1996.0", "        com.example.step: 0.5", "        com.example.scale: 1000000000000000000000", "        com.example.epsilon: 0.0000001", "");
        for (var pair : java.util.Map.of(AprBeta6.Representation.JSONC, jsonc, AprBeta6.Representation.YAML, yaml).entrySet()) {
            var record = (AprBeta6.FormRecord) AprBeta6.readStream(pair.getValue(), pair.getKey()).getFirst();
            String actual = AprBeta6Integrity.digest(record.value());
            if (!expected.equals(actual)) throw new AssertionError(pair.getKey() + " numeric extension digest was " + actual + ": " + AprBeta6Integrity.canonicalize(record.value()));
        }
        var resolved = (AprBeta6.FormRecord) AprBeta6.readStream("aprVersion: \"1.0-beta.6\"\nmetadata: { title: T, com.example.count: 5, com.example.lead: 012, com.example.quoted: '5' }\nsections: []\n", AprBeta6.Representation.YAML).getFirst();
        java.util.Map<?,?> metadata = (java.util.Map<?,?>) resolved.value().get("metadata");
        if (!(metadata.get("com.example.count") instanceof Long) || !"012".equals(metadata.get("com.example.lead")) || !"5".equals(metadata.get("com.example.quoted")))
            throw new AssertionError("APR YAML integer resolution differs from JSON: " + metadata);
    }
    private static void beta6Corpus() throws Exception {
        Path beta6 = Path.of("..", "tests", "Conformance", "beta6");
        AprDocument jsonc = AprBeta6.readForm(Files.readString(beta6.resolve("forms/permit.apr.jsonc")), AprBeta6.Representation.JSONC);
        AprDocument yaml = AprBeta6.readForm(Files.readString(beta6.resolve("forms/permit.apr.yaml")), AprBeta6.Representation.YAML);
        if (!jsonc.toJson().equals(yaml.toJson())) throw new AssertionError("shared beta.6 forms differ");
        var outOfOrder = AprBeta6.readStream(Files.readString(beta6.resolve("streams/out-of-order.apr.jsonc")), AprBeta6.Representation.JSONC);
        if (outOfOrder.size() != 3 || AprBeta6Integrity.resolve(outOfOrder).getFirst().state().equals("unresolved")) throw new AssertionError("out-of-order beta.6 stream did not resolve by digest");
        var yamlOutOfOrder = AprBeta6.readStream(Files.readString(beta6.resolve("streams/out-of-order.apr.yaml")), AprBeta6.Representation.YAML);
        if (yamlOutOfOrder.stream().filter(record -> record instanceof AprBeta6.FormRecord).count() != 2 || AprBeta6Integrity.resolve(yamlOutOfOrder).getFirst().state().equals("unresolved")) throw new AssertionError("YAML out-of-order stream did not preserve its records");
        var witnessed = AprBeta6.readStream(Files.readString(beta6.resolve("streams/witnessed.apr.jsonc")), AprBeta6.Representation.JSONC);
        if (AprBeta6Integrity.resolve(witnessed).get(1).witnessesResolved() != 1) throw new AssertionError("beta.6 witness did not resolve");
        var chain = AprBeta6.readStream(Files.readString(beta6.resolve("streams/witness-chain.apr.jsonc")), AprBeta6.Representation.JSONC);
        if (AprBeta6Integrity.resolve(chain).get(1).witnessesResolved() != 1 || AprBeta6Integrity.resolve(chain).get(2).witnessesResolved() != 1) throw new AssertionError("beta.6 witness chain did not resolve");
        var changed = AprBeta6.readStream(Files.readString(beta6.resolve("streams/changed-form.apr.jsonc")), AprBeta6.Representation.JSONC);
        if (!"unresolved".equals(AprBeta6Integrity.resolve(changed).getFirst().state())) throw new AssertionError("changed form inherited an attestation");
        var cms = AprBeta6.readStream(Files.readString(beta6.resolve("attestations/permit.cms.attestation.jsonc")), AprBeta6.Representation.JSONC);
        if (!"valid".equals(AprBeta6Integrity.resolve(java.util.List.of(new AprBeta6.FormRecord(jsonc), cms.getFirst())).getFirst().state())) throw new AssertionError("CMS proof did not verify");
        var rewritten = AprBeta6.readStream(AprBeta6.writeStream(java.util.List.of(new AprBeta6.FormRecord(jsonc), cms.getFirst()), AprBeta6.Representation.JSONC), AprBeta6.Representation.JSONC);
        if (!"valid".equals(AprBeta6Integrity.resolve(rewritten).getFirst().state())) throw new AssertionError("stream rewrite changed CMS subject semantics");
        var extension = AprBeta6.readStream("{\"aprVersion\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[]}],\"x-vendor\":{\"enabled\":true}}", AprBeta6.Representation.JSONC);
        if (!AprBeta6.writeStream(extension, AprBeta6.Representation.JSONC).contains("\"x-vendor\":{\"enabled\":true}")) throw new AssertionError("stream rewrite lost extension member");
        String tamperedCms = Files.readString(beta6.resolve("attestations/permit.cms.attestation.jsonc")).replace("\"kind\": \"document\"", "\"kind\": \"fields\", \"fields\": [\"name\"]");
        var tampered = AprBeta6.readStream(tamperedCms, AprBeta6.Representation.JSONC);
        if (!"invalid".equals(AprBeta6Integrity.resolve(java.util.List.of(new AprBeta6.FormRecord(jsonc), tampered.getFirst())).getFirst().state())) throw new AssertionError("tampered CMS envelope verified");
        var fields = AprBeta6.readStream(Files.readString(beta6.resolve("attestations/permit.fields.attestation.jsonc")), AprBeta6.Representation.JSONC);
        if (!"unverifiable".equals(AprBeta6Integrity.resolve(java.util.List.of(new AprBeta6.FormRecord(jsonc), fields.getFirst())).getFirst().state())) throw new AssertionError("fields scope corpus vector did not bind context");
        var unsupported = AprBeta6.readStream(Files.readString(beta6.resolve("attestations/permit.unsupported.attestation.jsonc")), AprBeta6.Representation.JSONC);
        if (!"unverifiable".equals(AprBeta6Integrity.resolve(java.util.List.of(new AprBeta6.FormRecord(jsonc), unsupported.getFirst())).getFirst().state())) throw new AssertionError("unsupported proof was not unverifiable");
        try (var malformed = Files.list(beta6.resolve("malformed"))) {
            for (Path path : malformed.filter(Files::isRegularFile).toList()) {
                try { AprBeta6.readStream(Files.readString(path), AprBeta6.Representation.JSONC); throw new AssertionError("malformed beta.6 stream parsed: " + path); } catch (AprException expected) { }
            }
        }
    }
}
