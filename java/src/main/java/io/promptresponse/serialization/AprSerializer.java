package io.promptresponse.serialization;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;
import io.promptresponse.models.AprDocument;

import java.io.File;
import java.io.IOException;

public class AprSerializer {
    private static final ObjectMapper mapper = createMapper();

    private static ObjectMapper createMapper() {
        ObjectMapper om = new ObjectMapper();
        om.registerModule(new JavaTimeModule());
        return om;
    }

    public static String serialize(AprDocument document) throws IOException {
        return mapper.writerWithDefaultPrettyPrinter().writeValueAsString(document);
    }

    public static AprDocument deserialize(String json) throws IOException {
        return mapper.readValue(json, AprDocument.class);
    }

    public static AprDocument loadFile(String filePath) throws IOException {
        return mapper.readValue(new File(filePath), AprDocument.class);
    }

    public static void saveFile(AprDocument document, String filePath) throws IOException {
        mapper.writerWithDefaultPrettyPrinter().writeValue(new File(filePath), document);
    }
}
