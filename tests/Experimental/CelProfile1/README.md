# Experimental APR CEL Binding v1 corpus

This directory supports issues #112–#114. It is intentionally outside
`tests/Conformance/v1/`: the format has not yet adopted these decisions.

`vectors.json` states the observable behavior the prototype must compare across
.NET, Python, TypeScript, and Java. A mismatch is evidence for a design decision,
not something an SDK may silently paper over. On ratification, stable cases move
into `tests/Conformance/v1/expressions/` and gain CI gates.
