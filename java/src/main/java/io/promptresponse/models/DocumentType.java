package io.promptresponse.models;

import com.fasterxml.jackson.annotation.JsonValue;

public enum DocumentType {
    TEMPLATE("template"),
    FILLED_FORM("filledForm");

    private final String value;

    DocumentType(String value) {
        this.value = value;
    }

    @JsonValue
    public String getValue() {
        return value;
    }

    public static DocumentType fromValue(String value) {
        for (DocumentType type : values()) {
            if (type.value.equals(value)) {
                return type;
            }
        }
        throw new IllegalArgumentException("Unknown document type: " + value);
    }
}
