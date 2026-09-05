# Rule vectors

One pair of documents per rule: `<rule>-satisfies` is a document the rule permits,
and `<rule>-violates` is one it forbids. Each is declared in `../corpus.map.json`
with the rule it exercises, and `scripts/check-rule-evidence.py` requires the
violating document to be refused traceably for that rule.

These are narrow on purpose. An example in the specification illustrates prose to a
reader; a vector here exists only to make a rule fail if an implementation ignores it.
