package org.promptresponse;

/** Thrown when bytes do not have the structural shape of an APR document. */
public final class AprException extends RuntimeException {
    private final String code;
    public AprException(String message) { this(message, null); }
    /**
     * @param code The format's own diagnostic, where it names one -- {@code WRONG_TYPE}
     *             for a structural member carrying the wrong JSON type, for example.
     *             A message says what went wrong to a person; a code says it to the
     *             conformance suite.
     */
    public AprException(String message, String code) { super(message); this.code = code; }
    public String code() { return code; }
}
