# @promptresponse/core

The browser and Node.js core SDK for the APR form format. It implements the
beta.6 profile: parse, validate, render HTML, fill, write, and inspect independent
attestation records. It does not make trust decisions for callers.

```ts
import { dumps, loads, validate } from "@promptresponse/core";

const form = loads(aprText);
form.sections[0].prompts[0].response = "Any text is valid";
console.log(validate(form).isValid);
const filledAprf = dumps(form);
```

This package performs no network access. `metadata.submissionUrls` remains data
until a host application presents an explicit submission action and applies a
separate, user-visible transport policy.
