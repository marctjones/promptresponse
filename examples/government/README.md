# Official-form APR examples

Each APR file in this directory is a semantic, accessible transcription of the
response-bearing content in an official public IRS form. It is not a
government-issued substitute and must not be represented as one. The adjacent
PDF is retained unchanged as the source for visual comparison and future
extractor evaluation.

| APR example | Official source | SHA-256 | Notes |
| --- | --- | --- | --- |
| `irs-form-w9-2024.aprt` | `https://www.irs.gov/pub/irs-pdf/fw9.pdf` | `2d420cbb4123dcf1fb82595b2359cfbb5d81f00b9df9d359fcc7af361d093f53` | Form W-9, Rev. March 2024; six-page XFA source including instructions. |
| `irs-form-w2-2026.aprt` | `https://www.irs.gov/pub/irs-pdf/fw2.pdf` | `61eca7c81f16d3965819fe1f31be4fe68c1b2887a81f51172f1d2ed2b2b9f087` | Form W-2, 2026; eleven-page XFA source containing the official copy variants. |

The source-form revision and hash are also recorded in each APR metadata
object. These assets are examples and importer-evaluation inputs; they add no
APR semantics and do not amend the frozen format.
