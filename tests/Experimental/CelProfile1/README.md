# Experimental APR CEL Binding v1 corpus

This directory supports issues #112–#114. It is intentionally outside
`tests/Conformance/beta6/`: the active format has not adopted these experimental decisions.

`vectors.json` states the observable behavior the prototype must compare across
.NET, Python, TypeScript, and Java. A mismatch is evidence for a design decision,
not something an SDK may silently paper over. On ratification, stable cases move
into `tests/Conformance/beta6/` and gain CI gates.
