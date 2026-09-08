package org.promptresponse;

import java.util.List;

public record ValidationResult(List<ValidationIssue> errors, List<ValidationIssue> warnings) {
    public boolean isValid() { return errors.isEmpty(); }
}
