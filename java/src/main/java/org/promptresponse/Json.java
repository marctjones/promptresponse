package org.promptresponse;

import java.util.*;

/** Small dependency-free JSON reader/writer for the APR core SDK. */
final class Json {
    private final String input;
    private int at;
    private Json(String input) { this.input = input; }

    static Object parse(String input) {
        Json parser = new Json(input == null ? "" : input);
        Object value = parser.value();
        parser.white();
        if (parser.at != parser.input.length()) throw parser.error("unexpected trailing content");
        return value;
    }

    static String write(Object value) {
        StringBuilder out = new StringBuilder();
        write(value, out);
        return out.toString();
    }

    private Object value() {
        white();
        if (at == input.length()) throw error("expected JSON value");
        return switch (input.charAt(at)) {
            case '{' -> object(); case '[' -> array(); case '"' -> string();
            case 't' -> literal("true", Boolean.TRUE); case 'f' -> literal("false", Boolean.FALSE);
            case 'n' -> literal("null", null); default -> number();
        };
    }
    private Map<String, Object> object() {
        take('{'); LinkedHashMap<String, Object> result = new LinkedHashMap<>(); white();
        if (peek('}')) { at++; return result; }
        while (true) {
            white(); if (!peek('"')) throw error("object key must be a string");
            String key = string(); white(); take(':'); result.put(key, value()); white();
            if (peek('}')) { at++; return result; } take(',');
        }
    }
    private List<Object> array() {
        take('['); ArrayList<Object> result = new ArrayList<>(); white();
        if (peek(']')) { at++; return result; }
        while (true) { result.add(value()); white(); if (peek(']')) { at++; return result; } take(','); }
    }
    private String string() {
        take('"'); StringBuilder out = new StringBuilder();
        while (at < input.length()) {
            char c = input.charAt(at++);
            if (c == '"') return out.toString();
            if (c < 0x20) throw error("control character in string");
            if (c != '\\') { out.append(c); continue; }
            if (at == input.length()) throw error("unfinished escape");
            char escaped = input.charAt(at++);
            switch (escaped) {
                case '"', '\\', '/' -> out.append(escaped);
                case 'b' -> out.append('\b'); case 'f' -> out.append('\f'); case 'n' -> out.append('\n');
                case 'r' -> out.append('\r'); case 't' -> out.append('\t');
                case 'u' -> {
                    if (at + 4 > input.length()) throw error("short unicode escape");
                    String hex = input.substring(at, at + 4);
                    try { out.append((char) Integer.parseInt(hex, 16)); } catch (NumberFormatException ex) { throw error("bad unicode escape"); }
                    at += 4;
                }
                default -> throw error("unknown escape");
            }
        }
        throw error("unterminated string");
    }
    private Object literal(String text, Object result) {
        if (!input.startsWith(text, at)) throw error("invalid literal"); at += text.length(); return result;
    }
    private Number number() {
        int start = at;
        if (peek('-')) at++;
        if (at == input.length()) throw error("invalid number");
        if (peek('0')) at++; else { digits(); }
        if (peek('.')) { at++; digits(); }
        if (peek('e') || peek('E')) { at++; if (peek('+') || peek('-')) at++; digits(); }
        String token = input.substring(start, at);
        try { return token.contains(".") || token.contains("e") || token.contains("E") ? Double.valueOf(token) : Long.valueOf(token); }
        catch (NumberFormatException ex) { throw error("invalid number"); }
    }
    private void digits() { int start = at; while (at < input.length() && Character.isDigit(input.charAt(at))) at++; if (at == start) throw error("expected digit"); }
    private void take(char expected) { white(); if (!peek(expected)) throw error("expected '" + expected + "'"); at++; }
    private boolean peek(char c) { return at < input.length() && input.charAt(at) == c; }
    private void white() { while (at < input.length() && Character.isWhitespace(input.charAt(at))) at++; }
    private AprException error(String message) { return new AprException("Invalid JSON at offset " + at + ": " + message); }
    @SuppressWarnings("unchecked")
    private static void write(Object value, StringBuilder out) {
        if (value == null) { out.append("null"); return; }
        if (value instanceof String text) { out.append('"'); for (int i=0; i<text.length(); i++) { char c=text.charAt(i); switch(c) { case '"' -> out.append("\\\""); case '\\' -> out.append("\\\\"); case '\b' -> out.append("\\b"); case '\f' -> out.append("\\f"); case '\n' -> out.append("\\n"); case '\r' -> out.append("\\r"); case '\t' -> out.append("\\t"); default -> { if(c < 0x20) out.append(String.format("\\u%04x", (int)c)); else out.append(c); } } } out.append('"'); return; }
        if (value instanceof Boolean || value instanceof Number) { out.append(value); return; }
        if (value instanceof Map<?, ?> map) { out.append('{'); boolean first=true; for (var entry: map.entrySet()) { if (!first) out.append(','); first=false; write(String.valueOf(entry.getKey()), out); out.append(':'); write(entry.getValue(), out); } out.append('}'); return; }
        if (value instanceof List<?> list) { out.append('['); for(int i=0;i<list.size();i++) { if(i>0) out.append(','); write(list.get(i),out); } out.append(']'); return; }
        throw new IllegalArgumentException("Not JSON: " + value.getClass());
    }
}
