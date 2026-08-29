package org.promptresponse;

/** A structural validation error. Core SDKs do not reject values based on hints. */
public record ValidationIssue(String code, String path, String message) { }
