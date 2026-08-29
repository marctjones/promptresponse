package org.promptresponse;

import java.nio.file.*;

/** Dependency-free corpus runner, usable with just javac/java. */
public final class AprConformanceTest {
    public static void main(String[] args) throws Exception {
        expressionBinding();
        Path corpus=Path.of(args.length == 0 ? "tests/Conformance/v1" : args[0]); int failures=0, checked=0;
        for(String group : new String[]{"valid","invalid","malformed"}) try(var files=Files.list(corpus.resolve(group))) {
            for(Path file : files.filter(Files::isRegularFile).toList()) { checked++; boolean shouldParse=!group.equals("malformed"), shouldValidate=group.equals("valid"); try { AprDocument document=Apr.read(file); boolean valid=Apr.validate(document).isValid(); if(!shouldParse || valid != shouldValidate) throw new IllegalStateException("unexpected parse/validation result"); if(shouldValidate && !Apr.parse(document.toJson()).toJson().equals(document.toJson())) throw new IllegalStateException("round trip changed document"); } catch(Exception ex) { if(shouldParse) { System.err.println(file+": "+ex); failures++; } } }
        }
        if(failures > 0) throw new AssertionError(failures+" / "+checked+" corpus cases failed");
        System.out.println("Java APR core conformance: "+checked+" cases passed");
    }
    private static void expressionBinding() {
        AprDocument document=Apr.parse("{\"version\":\"1.0-beta\",\"metadata\":{\"title\":\"T\"},\"sections\":[{\"id\":\"s\",\"title\":\"S\",\"prompts\":[{\"id\":\"qty\",\"label\":\"Quantity\",\"response\":\"3\",\"hints\":{\"expectedDataType\":\"number\"}},{\"id\":\"price\",\"label\":\"Price\",\"response\":\"12.5\",\"hints\":{\"expectedDataType\":\"currency\"}},{\"id\":\"total\",\"label\":\"Total\",\"response\":\"\",\"hints\":{\"expectedDataType\":\"currency\",\"exprValue\":\"qty * price\"}}]}]}");
        if (!AprExpressions.recomputeComputedValues(document) || !document.toJson().contains("\"response\":\"37.5\"")) throw new AssertionError("CEL binding did not compute typed value");
        document.setResponse("total", "40");
        if (AprExpressions.recomputeComputedValues(document) || !document.toJson().contains("\"response\":\"40\"")) throw new AssertionError("CEL binding overwrote human correction");
    }
}
