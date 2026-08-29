# apr-sig-v3 Canonicalization Vectors

The canonical payload is the hardest part of APR to port, and the only part that is
**silently wrong until it is catastrophically wrong**: an implementation with flawless
CMS code still produces signatures nobody else can verify if it assembles the payload
differently by one byte. Round-tripping against your own signer proves nothing —
you have to match *these bytes*.

| File | What it is |
|---|---|
| `input.aprt` | Fixed input document. **Do not edit** — the vectors are computed from these exact bytes. |
| `vectors.json` | Expected canonical payloads: the readable text, its byte length, and its SHA-256. |

## How to use them

1. Load `input.aprt`.
2. Build each payload with the parameters in `vectors.json` → `parameters`.
3. Compare against `canonicalText` **first**, not the hash. A hash tells you that you
   are wrong; the text tells you *where* — diff it line by line.
4. Then confirm `sha256` and `byteLength`.

## The three payloads

- **`formDefinition`** — the signed form definition: title, template ids, and the
  ordered section/prompt structure with labels and hints. Excludes responses, response
  metadata, unknown members, the `signatures` array, and `version`.
- **`publisherPayload`** — what a publisher signs. Embeds `formDefDigest`, binding the
  form definition and the submission URL together.
- **`fillerPayload`** — what a filler signs: the covered `(fieldId, response)` pairs.

## Details that are easy to get wrong

- Every line is `<label>=<base64(UTF-8 value)>` terminated by `\n`, **including the
  last line**.
- An empty value is an empty string after `=`. The line is never omitted — dropping
  empty fields changes the payload.
- `fillerFields` are listed unsorted in `parameters` on purpose. The payload **MUST**
  sort them **ordinally by id** (`amount, empty_field, full_name, unicode_note`), so
  the signature does not depend on the order a caller happened to pass them in.
- `formDefDigest` is **UPPERCASE** hex of the SHA-256, and is then base64-encoded like
  any other value. Lowercase hex produces a different, wrong payload.
- Values are UTF-8 before base64. `input.aprt` deliberately contains non-ASCII text
  (`Café — 日本語 — مرحبا`) so that an implementation encoding in Latin-1 or UTF-16
  fails here rather than in production.

## Scope

These vectors cover canonicalization only — not CMS, not certificate trust. They are
schema-valid APR and are checked by `scripts/check-schema.py`, but they are not part
of the `valid/` behavioural corpus.
