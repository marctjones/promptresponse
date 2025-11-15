package io.promptresponse.api;

import io.promptresponse.models.*;
import io.promptresponse.serialization.AprSerializer;
import java.time.Instant;
import java.util.HashMap;
import java.util.Map;

public class FormFiller {

    public static AprDocument fillForm(AprDocument template, Map<String, String> responses, String filledBy) {
        if (template.getDocumentType() != DocumentType.TEMPLATE) {
            throw new IllegalArgumentException("Document must be a template");
        }

        // Deep copy via serialization
        String json = AprSerializer.serialize(template);
        AprDocument filledForm = AprSerializer.deserialize(json);

        // Convert to filled form
        filledForm.setDocumentType(DocumentType.FILLED_FORM);

        if (filledForm.getMetadata() != null) {
            filledForm.getMetadata().setFilledBy(filledBy);
            filledForm.getMetadata().setFilledDate(Instant.now());
            filledForm.getMetadata().setModified(Instant.now());
        }

        // Apply responses
        for (Map.Entry<String, String> entry : responses.entrySet()) {
            filledForm.getPromptById(entry.getKey()).ifPresent(prompt -> {
                prompt.setResponse(entry.getValue());
                prompt.getResponseMetadata().setLastModified(Instant.now());
            });
        }

        return filledForm;
    }

    public static double getCompletionPercentage(AprDocument document) {
        return document.getCompletionPercentage();
    }

    public static Map<String, String> getEmptyPrompts(AprDocument document) {
        Map<String, String> empty = new HashMap<>();
        for (Prompt prompt : document.getAllPrompts()) {
            if (prompt.getResponse() == null || prompt.getResponse().trim().isEmpty()) {
                empty.put(prompt.getId(), prompt.getLabel());
            }
        }
        return empty;
    }
}
