package org.promptresponse;

import java.nio.file.*;

/** Dependency-free corpus runner, usable with just javac/java. */
public final class AprConformanceTest {
    public static void main(String[] args) throws Exception {
        expressionBinding();
        beta6();
        beta6Corpus();
        specificationExamples();
        expressionActivation();
        System.out.println("Java APR beta.6 conformance passed");
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
        for (Object entry : examples) {
            java.util.Map<?, ?> example = (java.util.Map<?, ?>) entry;
            String id = String.valueOf(example.get("id"));
            String rule = String.valueOf(example.get("rule"));
            String representation = String.valueOf(example.get("representation"));
            String expect = String.valueOf(example.get("expect"));
            String document = String.valueOf(example.get("document"));
            AprBeta6.Representation form = representation.startsWith("yaml")
                ? AprBeta6.Representation.YAML : AprBeta6.Representation.JSONC;

            boolean accepted;
            String detail = "";
            try {
                if (representation.endsWith("-stream")) AprBeta6.readStream(document, form);
                else AprBeta6.readForm(document, form);
                accepted = true;
            } catch (RuntimeException rejected) {
                accepted = false;
                detail = String.valueOf(rejected.getMessage());
            }

            if ("valid".equals(expect) && !accepted)
                failures.add(id + " (#" + rule + "): specification says valid, reader rejected it — " + detail);
            if ("reject".equals(expect) && accepted)
                failures.add(id + " (#" + rule + "): specification requires rejection, reader accepted it");
        }
        if (!failures.isEmpty()) throw new AssertionError("specification examples failed:\n  " + String.join("\n  ", failures));
        System.out.println("Java specification examples passed: " + examples.size());
    }

    /** The activation the specification defines, and its caller-supplied instants. */
    private static void expressionActivation() {
        String json = "{\"version\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},"
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
        try { Apr.parse("{\"version\":\"1.0-beta\",\"metadata\":{\"title\":\"T\"},\"sections\":[]}"); throw new AssertionError("legacy APR version was accepted"); } catch (AprException expected) { }
        AprDocument document=Apr.parse("{\"version\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"qty\",\"label\":\"Quantity\",\"response\":\"3\",\"hints\":{\"expectedDataType\":\"number\"}},{\"id\":\"price\",\"label\":\"Price\",\"response\":\"12.5\",\"hints\":{\"expectedDataType\":\"currency\"}},{\"id\":\"total\",\"label\":\"Total\",\"response\":\"\",\"hints\":{\"expectedDataType\":\"currency\",\"exprValue\":\"qty * price\"}}]}]}");
        if (!AprExpressions.recomputeComputedValues(document) || !document.toJson().contains("\"response\":\"37.5\"")) throw new AssertionError("CEL binding did not compute typed value");
        document.setResponse("total", "40");
        if (AprExpressions.recomputeComputedValues(document) || !document.toJson().contains("\"response\":\"40\"")) throw new AssertionError("CEL binding overwrote human correction");
    }
    private static void beta6() {
        String form = "{\"version\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"p\",\"label\":\"P\",\"response\":\"Ada\"}]}]}";
        AprDocument jsonc = AprBeta6.readForm("// comment\n" + form.substring(0, form.length() - 1) + ",}", AprBeta6.Representation.JSONC);
        AprDocument yaml = AprBeta6.readForm(AprBeta6.writeForm(jsonc, AprBeta6.Representation.YAML), AprBeta6.Representation.YAML);
        if (!"Ada".equals(((java.util.Map<?,?>)((java.util.List<?>)((java.util.Map<?,?>)yaml.sections().getFirst()).get("prompts")).getFirst()).get("response"))) throw new AssertionError("beta.6 YAML changed a response");
        String attestation = "{\"recordType\":\"attestation\",\"version\":\"1.0-beta.6\",\"subject\":{\"digest\":\"sha256:0000000000000000000000000000000000000000000000000000000000000000\",\"canonicalization\":\"jcs-sha256\"},\"scope\":{\"kind\":\"document\"},\"manifest\":{\"root\":\"sha256:0000000000000000000000000000000000000000000000000000000000000000\",\"entries\":[]},\"proofs\":[],\"witnesses\":[]}";
        String stream = "\u001e" + attestation + "\n\u001e" + form + "\n\u001e" + form;
        java.util.List<AprBeta6.Record> records = AprBeta6.readStream(stream, AprBeta6.Representation.JSONC);
        if (records.size() != 3 || records.stream().filter(record -> record instanceof AprBeta6.FormRecord).count() != 2) throw new AssertionError("beta.6 stream lost an occurrence");
        try { AprBeta6.readForm(stream, AprBeta6.Representation.JSONC); throw new AssertionError("stream selected a record implicitly"); } catch (AprException expected) { if (!expected.getMessage().contains("APR_STREAM_REQUIRES_ITERATION")) throw expected; }
        try { AprBeta6.readForm(form.substring(0, form.length() - 1) + ",\"signatures\":[]}", AprBeta6.Representation.JSONC); throw new AssertionError("beta.6 accepted embedded signatures"); } catch (AprException expected) { if (!expected.getMessage().contains("RETIRED_EMBEDDED_SIGNATURES")) throw expected; }
        try { AprBeta6.readForm(form.replace("\"metadata\":", "\"metadata\":{},\"metadata\":"), AprBeta6.Representation.JSONC); throw new AssertionError("beta.6 accepted duplicate JSONC members"); } catch (AprException expected) { }
        Object value = Json.parse(form);
        if (!"sha256:9d7899e7f997eeb08d72e55fe9ee0ed9278748eaa415061cac9b11c142cac01d".equals(AprBeta6Integrity.digest(value))) throw new AssertionError("beta.6 digest is not representation-neutral");
        var manifest = AprBeta6Integrity.createManifest(value);
        var assertion = new java.util.LinkedHashMap<String,Object>(); assertion.put("recordType","attestation"); assertion.put("version","1.0-beta.6"); assertion.put("subject", java.util.Map.of("digest",manifest.root(),"canonicalization","jcs-sha256")); assertion.put("scope",java.util.Map.of("kind","document")); assertion.put("manifest",java.util.Map.of("root",manifest.root(),"entries",manifest.entries().stream().map(entry->java.util.Map.of("path",entry.path(),"digest",entry.digest())).toList())); assertion.put("proofs",java.util.List.of()); assertion.put("witnesses",java.util.List.of());
        if (!"unverifiable".equals(AprBeta6Integrity.resolve(java.util.List.of(new AprBeta6.FormRecord(AprBeta6.readForm(form,AprBeta6.Representation.JSONC)),new AprBeta6.AttestationRecord(assertion))).getFirst().state())) throw new AssertionError("unsigned beta.6 attestation must be unverifiable");
        var fieldManifest = new java.util.LinkedHashMap<String,Object>(); fieldManifest.put("root",manifest.root()); fieldManifest.put("entries",manifest.entries().stream().filter(entry -> !entry.path().equals("/sections/0/prompts/0/response")).map(entry->java.util.Map.of("path",entry.path(),"digest",entry.digest())).toList()); assertion.put("scope",java.util.Map.of("kind","fields","fields",java.util.List.of("p"))); assertion.put("manifest",fieldManifest);
        if (!"invalid".equals(AprBeta6Integrity.resolve(java.util.List.of(new AprBeta6.FormRecord(AprBeta6.readForm(form,AprBeta6.Representation.JSONC)),new AprBeta6.AttestationRecord(assertion))).getFirst().state())) throw new AssertionError("fields scope without response must be invalid");
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
        var extension = AprBeta6.readStream("{\"version\":\"1.0-beta.6\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[]}],\"x-vendor\":{\"enabled\":true}}", AprBeta6.Representation.JSONC);
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
