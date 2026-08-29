package org.promptresponse;

/** Thrown when bytes do not have the structural shape of an APR document. */
public final class AprException extends RuntimeException {
    public AprException(String message) { super(message); }
}
