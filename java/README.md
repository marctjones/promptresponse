# PromptResponse Java SDK

A Java 17+ implementation of APR **core+expressions**. It is
intended for lightweight backend processing: read, structurally validate,
change responses, and write APR JSON while preserving unknown members,
signatures. It evaluates APR's CEL expression hints through the official CEL-Java
runtime, but neither verifies signatures nor emits HTML.

```java
AprDocument form = Apr.read(Path.of("intake.aprt"));
form.setResponse("full_name", "Ada Lovelace");
if (Apr.validate(form).isValid()) Apr.write(form, Path.of("completed.aprf"));
```

## Test the shared corpus

```sh
./java/run-tests.sh
```

`./mvnw` downloads a checksum-pinned Maven distribution and dependencies only into
the ignored `java/.maven/` directory; it never installs Maven or CEL system-wide.
The test runner executes the shared valid/invalid/malformed corpus directly.

## Minimal JDK web-form demo

```sh
./java/run-demo.sh examples/contact-intake.aprt
```

It serves only `127.0.0.1:8082`, uses `com.sun.net.httpserver.HttpServer`,
escapes all document content, and writes a completed `.aprf` locally. This is
a reference host, not a production server: it has no authentication, network
submission, static asset pipeline, or browser renderer profile.
