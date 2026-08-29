package org.promptresponse;

import java.util.List;

public record ValidationResult(List<ValidationIssue> errors) {
    public boolean isValid() { return errors.isEmpty(); }
}
