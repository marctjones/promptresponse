package io.promptresponse.models;

import java.util.ArrayList;
import java.util.List;

public class PromptHints {
    private String expectedDataType;
    private String placeholder;
    private String helpText;
    private List<String> suggestedValues = new ArrayList<>();
    private Integer minLength;
    private Integer maxLength;
    private String pattern;

    public String getExpectedDataType() { return expectedDataType; }
    public void setExpectedDataType(String expectedDataType) { this.expectedDataType = expectedDataType; }

    public String getPlaceholder() { return placeholder; }
    public void setPlaceholder(String placeholder) { this.placeholder = placeholder; }

    public String getHelpText() { return helpText; }
    public void setHelpText(String helpText) { this.helpText = helpText; }

    public List<String> getSuggestedValues() { return suggestedValues; }
    public void setSuggestedValues(List<String> suggestedValues) { this.suggestedValues = suggestedValues; }

    public Integer getMinLength() { return minLength; }
    public void setMinLength(Integer minLength) { this.minLength = minLength; }

    public Integer getMaxLength() { return maxLength; }
    public void setMaxLength(Integer maxLength) { this.maxLength = maxLength; }

    public String getPattern() { return pattern; }
    public void setPattern(String pattern) { this.pattern = pattern; }
}
