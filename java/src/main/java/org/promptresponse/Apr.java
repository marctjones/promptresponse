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
        if (!(parsed instanceof Map<?, ?> map)) throw new AprException("An APR document must be a JSON object", "PARSE_ERROR");
        @SuppressWarnings("unchecked") Map<String,Object> root = (Map<String,Object>) map;
        for (String required : List.of("aprVersion", "metadata", "sections")) if (!root.containsKey(required)) throw new AprException(required + " is required. A document missing it is a structurally wrong shape, which is a parse failure rather than a validation error (specification 6.3).", "REQUIRED_FIELD");
        if (!(root.get("aprVersion") instanceof String)) throw new AprException("aprVersion must be a string", "WRONG_TYPE");
        if (!VERSION.equals(root.get("aprVersion"))) throw new AprException("Unsupported APR version " + root.get("aprVersion") + "; this build accepts only " + VERSION, "UNSUPPORTED_VERSION");
        if (!(root.get("metadata") instanceof Map<?, ?>)) throw new AprException("metadata must be an object", "WRONG_TYPE");
        if (!(root.get("sections") instanceof List<?>)) throw new AprException("sections must be an array", "WRONG_TYPE");
        // Present-but-null is a value, and this member has no null spelling: a document
        // either declares a kind or leaves the member out entirely.
        if (root.containsKey("documentType") && root.get("documentType") == null) throw new AprException("documentType, if present, must be a string", "PARSE_ERROR");
        rejectBadShape(root);
        return new AprDocument(root);
    }
    public static AprDocument read(Path path) throws IOException {
        String json = Files.readString(path, StandardCharsets.UTF_8);
        return parse(json.startsWith("\uFEFF") ? json.substring(1) : json);
    }
    public static void write(AprDocument document, Path path) throws IOException {
        if (!VERSION.equals(document.version())) throw new AprException("Unsupported APR version " + document.version() + "; this build accepts only " + VERSION, "UNSUPPORTED_VERSION");
        Files.writeString(path, document.toJson(), StandardCharsets.UTF_8);
    }
    public static ValidationResult validate(AprDocument document) {
        List<ValidationIssue> errors = new ArrayList<>();
        List<ValidationIssue> warnings = new ArrayList<>();
        if (blank(document.version())) issue(errors,"REQUIRED_FIELD","aprVersion","aprVersion is required.");
        else if (!VERSION.equals(document.version())) issue(errors,"UNSUPPORTED_VERSION","aprVersion","Unsupported APR version; this build accepts only " + VERSION + ".");
        String title = AprDocument.string(document.metadata().get("title")); if(blank(title)) issue(errors,"REQUIRED_FIELD","metadata.title","metadata.title is required.");
        if(document.sections().isEmpty()) issue(errors,"REQUIRED_FIELD","sections","A document must have at least one section.");
        if("filledForm".equals(document.documentType()) && blank(AprDocument.string(document.metadata().get("templateId")))) issue(errors,"REQUIRED_FIELD","metadata.templateId","A filled form must record templateId.");
        if (document.raw().get("roles") instanceof List<?> roles) for (int i = 0; i < roles.size(); i++)
            if (roles.get(i) instanceof Map<?,?> role && blank(AprDocument.string(role.get("id")))) issue(errors,"REQUIRED_FIELD","roles[" + i + "].id","A role entry names its id.");
        validateShape(document, errors);
        validateTextFloor(document, warnings);
        checkConfusableScriptMix(document, warnings);
        validateHintBounds(document, errors);
        Set<String> sectionIds=new HashSet<>(), promptIds=new HashSet<>(); validateSections(document.sections(), "sections", errors, sectionIds, promptIds);
        validateTables(document, errors, warnings);
        documentAdvisories(document, warnings);
        return new ValidationResult(List.copyOf(errors), List.copyOf(warnings));
    }

    /** Every child section of `sections`, depth first, with its index-based path. */
    @SuppressWarnings("unchecked") private static void walkSections(List<Object> sections, String path, java.util.function.BiConsumer<Map<String,Object>,String> visitor) {
        for (int i = 0; i < sections.size(); i++) {
            Map<String,Object> section = (Map<String,Object>) sections.get(i);
            String here = path + "[" + i + "]";
            visitor.accept(section, here);
            walkSections((List<Object>) section.getOrDefault("sections", List.of()), here + ".sections", visitor);
        }
    }

    private static boolean isUri(String value) {
        try { return new java.net.URI(value).getScheme() != null; } catch (java.net.URISyntaxException malformed) { return false; }
    }
    private static boolean isDigest(Object value) { return value instanceof String text && text.matches("sha256:[0-9a-f]{64}"); }

    /** Members whose value has a shape the format states, not just a type (specification 7.1). */
    @SuppressWarnings("unchecked") private static void validateShape(AprDocument document, List<ValidationIssue> errors) {
        String template = AprDocument.string(document.metadata().get("templateId"));
        if (template != null && !isUri(template)) issue(errors, "WRONG_TYPE", "metadata.templateId", "templateId '" + template + "' is not a URI.");
        if (document.metadata().get("regarding") instanceof List<?> entries) {
            for (int i = 0; i < entries.size(); i++) {
                if (!isDigest(entries.get(i))) issue(errors, "WRONG_TYPE", "metadata.regarding[" + i + "]", "regarding entry " + i + " is not a digest.");
            }
        }
        walkSections(document.sections(), "sections", (section, path) -> {
            if (section.get("maxRows") instanceof Number maxRows && maxRows.doubleValue() < 1) issue(errors, "WRONG_TYPE", path + ".maxRows",
                "maxRows is " + maxRows + ". A table always holds at least one instance, so a cap below one describes a table that cannot exist.");
        });
    }

    // Below the human-facing text floor (specification 7.2): unassigned, a surrogate,
    // private-use, a control other than tab and newline, or a code point UTS #39
    // classifies as default-ignorable, deprecated or not-a-character.
    private static final int[][] BELOW_FLOOR_RANGES = {
        {0x00AD, 0x00AD}, {0x061C, 0x061C}, {0x180B, 0x180F}, {0x200B, 0x200F},
        {0x202A, 0x202E}, {0x2060, 0x206F}, {0xFEFF, 0xFEFF}, {0xFFF0, 0xFFF8},
        {0xFFFE, 0xFFFF}, {0x1D173, 0x1D17A}, {0xE0000, 0xE0FFF},
    };
    private static boolean belowFloor(int codePoint) {
        if (codePoint == 0x09 || codePoint == 0x0A) return false;
        int type = Character.getType(codePoint);
        if (type == Character.CONTROL || type == Character.FORMAT || type == Character.SURROGATE
            || type == Character.PRIVATE_USE || type == Character.UNASSIGNED) return true;
        for (int[] range : BELOW_FLOOR_RANGES) if (codePoint >= range[0] && codePoint <= range[1]) return true;
        return (codePoint & 0xFFFE) == 0xFFFE;
    }

    // Specification 8.2.3 places NON_NFC_TEXT and FORBIDDEN_CODE_POINT in the
    // warnings table (7.2), not the errors table (7.1, stated exhaustive by
    // APR-VAL-007): a validator MUST report them, but a warning MUST NOT affect
    // validity or block saving (APR-VAL-006, APR-VAL-002). It also requires a
    // reader to preserve this text exactly (APR-TEXT-006) -- Apr.parse() does not normalize
    // or strip any string, so a non-NFC spelling or an excluded code point in
    // the source survives to be reported here instead of being silently
    // cleaned away before anyone sees it.
    private static void holdToTheFloor(String value, String path, List<ValidationIssue> warnings) {
        if (value == null || value.isEmpty()) return;
        if (!java.text.Normalizer.isNormalized(value, java.text.Normalizer.Form.NFC)) issue(warnings, "NON_NFC_TEXT", path,
            "Human-facing text must be in Normalization Form C; two spellings of one word are two different strings to everything that compares them.");
        for (int offset = 0; offset < value.length();) {
            int codePoint = value.codePointAt(offset);
            if (belowFloor(codePoint)) {
                // Not uniformly "renders as nothing": this category also holds ZWJ and
                // ZWNJ, load-bearing for correct glyph shaping in Persian, Hindi and
                // other scripts. Say what the rule is, not a rendering claim that's
                // false for part of the set it covers.
                issue(warnings, "FORBIDDEN_CODE_POINT", path, String.format("Human-facing text carries U+%04X, which the human-facing text floor excludes.", codePoint));
                return; // One report names the member; listing every offender adds noise.
            }
            offset += Character.charCount(codePoint);
        }
    }

    private static void reportExcluded(String value, String path, String code, boolean allowCarriageReturn, List<ValidationIssue> warnings) {
        if (value == null) return;
        for (int offset = 0; offset < value.length();) {
            int codePoint = value.codePointAt(offset);
            offset += Character.charCount(codePoint);
            if (allowCarriageReturn && codePoint == '\r') continue;
            if (!belowFloor(codePoint)) continue;
            issue(warnings, code, path, String.format("U+%04X is a code point the human-facing text floor excludes.", codePoint));
            return; // One report names the member; listing every offender adds noise.
        }
    }

    /**
     * Titles and labels are human-facing text. A response and a submission URL are held to
     * the same floor under their own codes, a response less a carriage return.
     */
    @SuppressWarnings("unchecked") private static void validateTextFloor(AprDocument document, List<ValidationIssue> warnings) {
        if (document.metadata().get("submissionUrls") instanceof List<?> urls) for (int i = 0; i < urls.size(); i++) {
            if (urls.get(i) instanceof String url) reportExcluded(url, "metadata.submissionUrls[" + i + "]", "SUBMISSION_URL_FORBIDDEN_CODE_POINT", false, warnings);
        }
        holdToTheFloor(AprDocument.string(document.metadata().get("title")), "metadata.title", warnings);
        holdToTheFloor(AprDocument.string(document.metadata().get("description")), "metadata.description", warnings);
        holdToTheFloor(AprDocument.string(document.metadata().get("author")), "metadata.author", warnings);
        holdToTheFloor(AprDocument.string(document.metadata().get("publisher")), "metadata.publisher", warnings);
        walkSections(document.sections(), "sections", (section, path) -> {
            holdToTheFloor(AprDocument.string(section.get("title")), path + ".title", warnings);
            holdToTheFloor(AprDocument.string(section.get("description")), path + ".description", warnings);
            List<Object> prompts = (List<Object>) section.getOrDefault("prompts", List.of());
            for (int i = 0; i < prompts.size(); i++) {
                Map<String,Object> prompt = (Map<String,Object>) prompts.get(i);
                holdToTheFloor(AprDocument.string(prompt.get("label")), path + ".prompts[" + i + "].label", warnings);
                // A response keeps the line breaks a person typed (APR-REP-004), so a
                // carriage return is not held against it (APR-TEXT-004).
                reportExcluded(AprDocument.string(prompt.get("response")), path + ".prompts[" + i + "].response", "RESPONSE_FORBIDDEN_CODE_POINT", true, warnings);
            }
        });
    }

    // Specification 8.2.3/APR-TEXT-012: "SHOULD apply the confusable and
    // mixed-script detection of UTS #39... report what it finds." Full UTS #39
    // restriction-level analysis needs a declared document language to avoid
    // flagging ordinary multi-script text (Japanese Han+Hiragana+Katakana,
    // Korean Hangul+Han, Latin loanwords in Indic/Arabic/Hebrew text) -- APR
    // has no metadata.language member yet, so that full analysis isn't
    // attempted here.
    //
    // What doesn't need a declared language: Latin, Cyrillic and Greek have
    // extensive letter-shape homoglyphs between them (Cyrillic а/Latin a,
    // Greek Α/Latin A) and essentially no legitimate reason to co-occur
    // within one title or label -- unlike CJK/Hangul/Indic scripts, which
    // routinely mix with Latin for brand names, loanwords and numerals.
    // Flagging only these three scripts mixing with each other is a narrow,
    // script-agnostic slice of UTS #39 that produces zero known false
    // positives on real multi-script text.
    //
    // Not in specification 7.2's warnings table -- CONFUSABLE_SCRIPT_MIX is
    // this implementation's own spelling of an APR-VAL-002 "MAY surface any
    // warning, including conditions this table does not name" extension, not
    // a code every implementation must use.
    private static String confusableScriptOf(int codePoint) {
        if (!Character.isLetter(codePoint)) return null; // Not a letter: digits, punctuation and spaces are script-neutral.
        Character.UnicodeScript script = Character.UnicodeScript.of(codePoint);
        if (script == Character.UnicodeScript.LATIN) return "Latin";
        if (script == Character.UnicodeScript.CYRILLIC) return "Cyrillic";
        if (script == Character.UnicodeScript.GREEK) return "Greek";
        return null;
    }
    private static void checkFieldForConfusableMix(String value, String path, List<ValidationIssue> warnings) {
        if (value == null || value.isEmpty()) return;
        Set<String> scripts = new TreeSet<>();
        for (int offset = 0; offset < value.length();) {
            int codePoint = value.codePointAt(offset);
            String script = confusableScriptOf(codePoint);
            if (script != null) scripts.add(script);
            offset += Character.charCount(codePoint);
        }
        if (scripts.size() > 1) issue(warnings, "CONFUSABLE_SCRIPT_MIX", path,
            "Mixes " + String.join(", ", scripts) + " letters in one field; Latin, Cyrillic and Greek share look-alike letters, and a mix within one title or label is rarely intentional.");
    }
    /** A response is not human-facing text in this sense; only titles and labels are. */
    @SuppressWarnings("unchecked") private static void checkConfusableScriptMix(AprDocument document, List<ValidationIssue> warnings) {
        checkFieldForConfusableMix(AprDocument.string(document.metadata().get("title")), "metadata.title", warnings);
        checkFieldForConfusableMix(AprDocument.string(document.metadata().get("description")), "metadata.description", warnings);
        checkFieldForConfusableMix(AprDocument.string(document.metadata().get("author")), "metadata.author", warnings);
        checkFieldForConfusableMix(AprDocument.string(document.metadata().get("publisher")), "metadata.publisher", warnings);
        walkSections(document.sections(), "sections", (section, path) -> {
            checkFieldForConfusableMix(AprDocument.string(section.get("title")), path + ".title", warnings);
            checkFieldForConfusableMix(AprDocument.string(section.get("description")), path + ".description", warnings);
            List<Object> prompts = (List<Object>) section.getOrDefault("prompts", List.of());
            for (int i = 0; i < prompts.size(); i++) checkFieldForConfusableMix(AprDocument.string(((Map<String,Object>) prompts.get(i)).get("label")), path + ".prompts[" + i + "].label", warnings);
        });
    }

    private static final Set<String> NUMERIC_TYPES = Set.of("number", "currency", "range");
    private static final Set<String> TEMPORAL_TYPES = Set.of("date", "time", "datetime");

    /** A bound must be comparable in the space its field lives in (specification 4.7). */
    @SuppressWarnings("unchecked") private static void validateHintBounds(AprDocument document, List<ValidationIssue> errors) {
        walkSections(document.sections(), "sections", (section, path) -> {
            List<Object> prompts = (List<Object>) section.getOrDefault("prompts", List.of());
            for (int i = 0; i < prompts.size(); i++) {
                Map<String,Object> hints = hintsOf((Map<String,Object>) prompts.get(i));
                String declared = AprDocument.string(hints.get("expectedDataType"));
                boolean numeric = declared != null && NUMERIC_TYPES.contains(declared), temporal = declared != null && TEMPORAL_TYPES.contains(declared);
                if (!numeric && !temporal) continue;
                for (String bound : List.of("min", "max")) {
                    Object value = hints.get(bound);
                    if (value == null || (numeric ? value instanceof Number : value instanceof String)) continue;
                    issue(errors, "WRONG_TYPE", path + ".prompts[" + i + "].hints." + bound, "hints." + bound + " is not "
                        + (numeric ? "a number" : "a string") + " on a '" + declared + "' field, where the format declares a value comparable in that field's space.");
                }
            }
        });
    }

    @SuppressWarnings("unchecked") private static void validateTables(AprDocument document, List<ValidationIssue> errors, List<ValidationIssue> warnings) {
        walkSections(document.sections(), "sections", (section, path) -> {
            if (!"table".equals(section.get("kind"))) return;
            List<Object> rows = (List<Object>) section.getOrDefault("sections", List.of());
            if (rows.isEmpty()) { issue(errors, "EMPTY_TABLE", path, "A table section has no instances. A table always has at least one row; an empty one cannot describe its own fields."); return; }
            List<Object> first = (List<Object>) ((Map<String,Object>) rows.get(0)).getOrDefault("prompts", List.of());
            for (int r = 1; r < rows.size(); r++) {
                Map<String,Object> row = (Map<String,Object>) rows.get(r);
                List<Object> prompts = (List<Object>) row.getOrDefault("prompts", List.of());
                boolean mismatch = prompts.size() != first.size();
                if (mismatch) issue(warnings, "TABLE_RAGGED", path + ".sections", "Table instance '" + row.get("id") + "' has " + prompts.size() + " prompts but the first has " + first.size() + "; corresponding fields cannot be aligned by position.");
                for (int i = 0; i < prompts.size() && !mismatch; i++) mismatch = !Objects.equals(((Map<String,Object>) prompts.get(i)).get("label"), ((Map<String,Object>) first.get(i)).get("label"));
                // A ragged row also can't have its labels compared meaningfully against
                // the first row, so it is reported as a label mismatch too.
                if (mismatch) issue(warnings, "TABLE_LABEL_MISMATCH", path + ".sections", "Table instance '" + row.get("id") + "' does not name its fields as the first instance does; corresponding fields should share a label.");
            }
            if (section.get("maxRows") instanceof Number maxRows && maxRows.doubleValue() > 0 && rows.size() > maxRows.doubleValue())
                issue(warnings, "TABLE_OVER_CAPACITY", path, "Table has " + rows.size() + " instances, above the advisory maximum of " + maxRows + ".");
        });
    }

    // Mirrors schemas/apr-types-1.0.json. A copy, not a read of that file at run time --
    // python/promptresponse/advisories.py and
    // src/PromptResponse.Core/Validation/AdvisoryVocabulary.cs keep the same copy for
    // the same reason.
    private static final Set<String> REGISTERED_TYPES = Set.of(
        "boolean", "color", "currency", "date", "datetime", "email", "multichoice",
        "multiline", "number", "password", "phone", "range", "select", "text", "time", "url");
    private static final Set<String> SUBMISSION_SCHEMES = Set.of("https", "mailto");

    @SuppressWarnings("unchecked") private static Map<String,Object> hintsOf(Map<String,Object> prompt) {
        return prompt.get("hints") instanceof Map<?,?> hints ? (Map<String,Object>) hints : Map.of();
    }
    private static Set<String> declaredRoles(AprDocument document) {
        Set<String> ids = new HashSet<>();
        if (document.raw().get("roles") instanceof List<?> roles) for (Object role : roles) if (role instanceof Map<?,?> r && r.get("id") instanceof String id) ids.add(id);
        return ids;
    }
    private static void inspectExtensions(Map<String,Object> node, Set<String> known, String path, List<ValidationIssue> warnings) {
        for (String name : node.keySet()) {
            if (known.contains(name) || name.contains(".")) continue;
            issue(warnings, "UNPREFIXED_MEMBER", path + "." + name, "unknown member '" + name + "' carries no reverse-DNS prefix; unprefixed names are reserved to the specification.");
        }
    }
    private static boolean compiles(String pattern) {
        try { java.util.regex.Pattern.compile(pattern); return true; } catch (java.util.regex.PatternSyntaxException invalid) { return false; }
    }
    private static boolean isNumber(String value) {
        try { Double.parseDouble(value.trim()); return !value.trim().isEmpty(); } catch (NumberFormatException notNumeric) { return false; }
    }
    private static boolean looksLike(String value, String expected) {
        return switch (expected) {
            case "email" -> value.contains("@") && value.substring(value.lastIndexOf('@') + 1).contains(".");
            case "number", "range" -> isNumber(value);
            case "currency" -> { String digits = value.replaceAll("[^0-9.eE+-]", ""); yield isNumber(digits.isEmpty() ? "x" : digits); }
            case "date" -> value.matches("^\\d{4}-\\d{2}-\\d{2}.*");
            case "url" -> value.startsWith("http://") || value.startsWith("https://");
            case "boolean" -> Set.of("true", "false", "yes", "no", "1", "0").contains(value.trim().toLowerCase(Locale.ROOT));
            default -> true;
        };
    }
    private static void outOfBounds(Map<String,Object> hints, String response, String id, List<ValidationIssue> warnings) {
        Object min = hints.get("min"), max = hints.get("max");
        if (min == null && max == null) return;
        double value;
        try { value = Double.parseDouble(response.trim()); } catch (NumberFormatException notNumeric) { return; }
        // Map.entry refuses a null value, so a hint carrying only max threw here
        // before either bound was compared.
        for (String name : List.of("minimum", "maximum")) {
            Object limitRaw = "minimum".equals(name) ? min : max;
            if (limitRaw == null) continue;
            double limit;
            try { limit = limitRaw instanceof Number n ? n.doubleValue() : Double.parseDouble(String.valueOf(limitRaw).trim()); } catch (NumberFormatException notNumeric) { continue; }
            boolean worse = "minimum".equals(name) ? value < limit : value > limit;
            if (worse) issue(warnings, "RESPONSE_OUTSIDE_BOUNDS", id, "Outside the suggested " + name + " of " + limitRaw + ". Bounds describe the control offered, not a limit on the answer.");
        }
    }

    /** Ids are machine keys, so a character outside [A-Za-z0-9_.-] is worth saying (APR-TEXT-010). */
    private static void idAdvisory(String id, String path, List<ValidationIssue> warnings) {
        if (!blank(id) && !id.matches("[A-Za-z0-9_.-]*")) issue(warnings, "ID_FORBIDDEN_CHARACTER", path, "id '" + id + "' carries a character outside [A-Za-z0-9_.-].");
    }

    private static void advisoriesFor(Map<String,Object> prompt, Set<String> roles, List<ValidationIssue> warnings) {
        String id = AprDocument.string(prompt.get("id"));
        idAdvisory(id, id, warnings);
        String role = AprDocument.string(prompt.get("role"));
        if (role != null && !roles.contains(role)) issue(warnings, "UNDECLARED_ROLE", id, "role '" + role + "' is not declared in roles.");
        inspectExtensions(prompt, PROMPT.keySet(), id, warnings);

        Map<String,Object> hints = hintsOf(prompt);
        String declared = AprDocument.string(hints.get("expectedDataType"));
        if (declared != null && !REGISTERED_TYPES.contains(declared)) issue(warnings, "UNREGISTERED_DATA_TYPE", id,
            "expectedDataType '" + declared + "' is not in the type registry; an unrecognised type degrades to text and never rejects a response.");
        String pattern = AprDocument.string(hints.get("validationPattern"));
        if (pattern != null && !compiles(pattern)) issue(warnings, "HINT_UNUSABLE", id, "validationPattern is not a valid regular expression, so nothing can apply it.");

        String response = Optional.ofNullable(AprDocument.string(prompt.get("response"))).orElse("");
        if (response.isEmpty()) return;
        if (pattern != null) {
            try {
                if (!java.util.regex.Pattern.compile(pattern).matcher(response).find()) issue(warnings, "RESPONSE_PATTERN_MISMATCH", id, "'" + response + "' does not match the suggested pattern.");
            } catch (java.util.regex.PatternSyntaxException invalid) {
                issue(warnings, "RESPONSE_PATTERN_MISMATCH", id, "The suggested pattern is not a valid regex.");
            }
        }
        if (declared != null && !looksLike(response, declared)) issue(warnings, "RESPONSE_CONTRADICTS_TYPE", id, "'" + response + "' does not look like '" + declared + "' (advisory).");
        if (hints.get("suggestedValues") instanceof List<?> suggested && !suggested.isEmpty() && !suggested.contains(response))
            issue(warnings, "RESPONSE_OUTSIDE_SUGGESTED_VALUES", id, "Not one of the suggested options, which the format allows.");
        outOfBounds(hints, response, id, warnings);
    }

    @SuppressWarnings("unchecked") private static void documentAdvisories(AprDocument document, List<ValidationIssue> warnings) {
        Set<String> roles = declaredRoles(document);
        for (String role : roles) idAdvisory(role, "roles", warnings);
        inspectExtensions(document.metadata(), METADATA.keySet(), "metadata", warnings);
        if (document.metadata().get("submissionUrls") instanceof List<?> urls) for (int i = 0; i < urls.size(); i++) {
            String url = String.valueOf(urls.get(i));
            String scheme = url.contains(":") ? url.substring(0, url.indexOf(':')) : "";
            if (!SUBMISSION_SCHEMES.contains(scheme.toLowerCase(Locale.ROOT))) issue(warnings, "SUBMISSION_URL_UNSUPPORTED", "metadata.submissionUrls[" + i + "]",
                "submission entry " + i + " names the scheme '" + scheme + "', which this document does not define; a reader offers the entries it understands.");
        }
        walkSections(document.sections(), "sections", (section, path) -> {
            idAdvisory(AprDocument.string(section.get("id")), path + ".id", warnings);
            if (!"table".equals(section.get("kind")) && (section.get("maxRows") != null || section.get("canAddRows") != null)) issue(warnings, "TABLE_MEMBERS_ON_A_PLAIN_SECTION", path,
                "maxRows or canAddRows on a section that is not a table; a table is a table only by carrying kind: \"table\".");
            String role = AprDocument.string(section.get("role"));
            if (role != null && !roles.contains(role)) issue(warnings, "UNDECLARED_ROLE", path, "role '" + role + "' is not declared in roles.");
            inspectExtensions(section, SECTION.keySet(), path, warnings);
            for (Object prompt : (List<Object>) section.getOrDefault("prompts", List.of())) advisoriesFor((Map<String,Object>) prompt, roles, warnings);
        });
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
        Map<String,Object> metadata=(Map<String,Object>)root.get("metadata");
        strings(metadata,"submissionUrls","metadata.submissionUrls");
        if(root.containsKey("roles") && !(root.get("roles") instanceof List<?>)) throw new AprException("roles must be an array", "WRONG_TYPE");
        structuralTypes(root, DOCUMENT, "");
        structuralTypes(metadata, METADATA, "/metadata");
        sections((List<Object>)root.get("sections"));
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
        Map.entry("language", new Class<?>[]{String.class}),
        Map.entry("created", new Class<?>[]{String.class}),
        Map.entry("modified", new Class<?>[]{String.class}),
        Map.entry("submissionUrls", new Class<?>[]{List.class}),
        Map.entry("regarding", new Class<?>[]{List.class}));
    private static final Map<String,Class<?>[]> SECTION = Map.of(
        "id", new Class<?>[]{String.class}, "title", new Class<?>[]{String.class},
        "description", new Class<?>[]{String.class}, "role", new Class<?>[]{String.class},
        "kind", new Class<?>[]{String.class}, "language", new Class<?>[]{String.class},
        "canAddRows", new Class<?>[]{Boolean.class}, "maxRows", new Class<?>[]{Number.class},
        "prompts", new Class<?>[]{List.class}, "sections", new Class<?>[]{List.class});
    private static final Map<String,Class<?>[]> PROMPT = Map.of(
        "id", new Class<?>[]{String.class}, "label", new Class<?>[]{String.class},
        "response", new Class<?>[]{String.class}, "role", new Class<?>[]{String.class},
        "language", new Class<?>[]{String.class},
        "hints", new Class<?>[]{Map.class});
    // Map.of caps at ten pairs, and hints has more members than that.
    private static final Map<String,Class<?>[]> HINTS = Map.ofEntries(
        Map.entry("expectedDataType", new Class<?>[]{String.class}),
        Map.entry("placeholder", new Class<?>[]{String.class}),
        Map.entry("helpText", new Class<?>[]{String.class}),
        Map.entry("validationPattern", new Class<?>[]{String.class}),
        Map.entry("suggestedValues", new Class<?>[]{List.class}),
        Map.entry("step", new Class<?>[]{Number.class}),
        // `min` and `max` are a number on an ordered numeric field and a canonical-form
        // string on a temporal one, so both spellings are the format's own.
        Map.entry("min", new Class<?>[]{Number.class, String.class}),
        Map.entry("max", new Class<?>[]{Number.class, String.class}),
        Map.entry("exprHidden", new Class<?>[]{String.class}),
        Map.entry("exprValue", new Class<?>[]{String.class}),
        Map.entry("exprExpected", new Class<?>[]{String.class}),
        Map.entry("exprValidation", new Class<?>[]{String.class}),
        Map.entry("exprReadOnly", new Class<?>[]{String.class}));

    private static void structuralTypes(Map<String,Object> node, Map<String,Class<?>[]> table, String path) {
        for (Map.Entry<String,Object> member : node.entrySet()) {
            Class<?>[] allowed = table.get(member.getKey());
            // An explicit null is absence, not a wrong type: a member table governs a
            // value that is present.
            if (allowed == null || member.getValue() == null) continue;
            boolean ok = false;
            for (Class<?> type : allowed) if (type.isInstance(member.getValue())) ok = true;
            if (!ok) throw new AprException(path + "/" + member.getKey() + " is "
                + spell(member.getValue()) + " where the format declares " + spell(allowed)
                + "; APR values are never coerced.", "WRONG_TYPE");
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
    @SuppressWarnings("unchecked") private static void sections(List<Object> list) { for(Object item:list) { if(!(item instanceof Map<?,?>)) throw new AprException("section must be an object"); Map<String,Object>s=(Map<String,Object>)item; structuralTypes(s, SECTION, "/sections"); if(s.get("maxRows") instanceof Double d && d != Math.floor(d)) throw new AprException("/sections/maxRows is " + d + " where the format declares an integer; APR values are never coerced.", "WRONG_TYPE"); if(s.containsKey("prompts") && !(s.get("prompts") instanceof List<?>)) throw new AprException("section.prompts must be an array", "WRONG_TYPE"); if(s.containsKey("sections") && !(s.get("sections") instanceof List<?>)) throw new AprException("section.sections must be an array", "WRONG_TYPE"); for(Object p:(List<Object>)s.getOrDefault("prompts",List.of())) { if(!(p instanceof Map<?,?>)) throw new AprException("prompt must be an object"); Map<String,Object>pm=(Map<String,Object>)p; Object response=pm.get("response"); if(response != null && !(response instanceof String)) throw new AprException("prompt.response must be a string", "WRONG_TYPE"); structuralTypes(pm, PROMPT, "/prompts"); if(pm.get("hints") instanceof Map<?,?> h) { Map<String,Object> hints=(Map<String,Object>)h; structuralTypes(hints, HINTS, "/prompts/hints"); strings(hints,"suggestedValues","/prompts/hints/suggestedValues"); } } sections((List<Object>)s.getOrDefault("sections",List.of())); } }
    private static void strings(Map<String,Object> map,String key,String path) { if(map.containsKey(key) && (!(map.get(key) instanceof List<?> values) || values.stream().anyMatch(value -> !(value instanceof String)))) throw new AprException(path+" must be an array of strings", "WRONG_TYPE"); }
}
