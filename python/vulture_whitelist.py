"""
Names vulture reports as unused that are intentional, not dead:

- ``AprYamlLoader.yaml_implicit_resolvers`` is read by PyYAML's own resolver
  machinery, not by this codebase; setting it to ``{}`` disables implicit
  scalar resolution (specification 8.x, no surprise type coercion).
- ``roles.used`` and ``UnicodeFinding.codepoint`` are small pieces of public
  API a consumer of the package would reasonably use alongside their
  neighbors (``roles.resolve``/``roles.definition``; the other
  ``UnicodeFinding`` fields), even though nothing in this repository calls
  them yet.

Regenerate the candidate list with:
    uv run vulture promptresponse tests ../scripts/run-spec-semantic-review.py --min-confidence 60
and add only entries you have actually checked are intentional — see the
2026-09-07 audit for how the rest were confirmed as real usage instead.
"""

from promptresponse.apr_yaml import AprYamlLoader
from promptresponse.roles import used
from promptresponse.unicode_security import UnicodeFinding

AprYamlLoader.yaml_implicit_resolvers
used
UnicodeFinding.codepoint
