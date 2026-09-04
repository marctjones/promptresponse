"""APR-YAML: YAML 1.2 syntax carrying the JSON value space (specification 4.5).

This module deliberately depends on nothing but PyYAML and the standard
library. It is the one place APR's YAML resolution and exclusions are
implemented in Python, and it is loaded both by the SDK (promptresponse.beta6)
and, by file path, by scripts/check-schema.py, so the schema gate and the
reference reader cannot disagree about what an APR-YAML document means.
"""

import re
from typing import Any, Iterator, List

import yaml

# APR-YAML resolves scalars to the JSON value space, which is not what a stock
# YAML 1.1 loader does. PyYAML resolves "yes" and "on" as booleans, ".inf" as a
# float, "2026-01-01" as a date, and "012" as octal 10 - the last of which is
# silent data corruption in a response. The specification's resolution table
# (4.5.1) is implemented here instead: quoted scalars are strings, the null,
# boolean and JSON number forms resolve to those types, and any other plain
# scalar is a string. [APR-REP-012]
_JSON_NUMBER = r"-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][-+]?[0-9]+)?"
_NON_FINITE = re.compile(r"^[-+]?\.(?:inf|Inf|INF|nan|NaN|NAN)$")


class AprYamlError(ValueError):
    """The source uses a YAML construct or value APR-YAML excludes."""


class AprYamlLoader(yaml.SafeLoader):
    """A YAML loader whose scalar resolution is the specification's, not YAML 1.1's."""


AprYamlLoader.yaml_implicit_resolvers = {}
AprYamlLoader.add_implicit_resolver(
    "tag:yaml.org,2002:null", re.compile(r"^(?:~|null|Null|NULL|)$"), ["~", "n", "N", ""]
)
AprYamlLoader.add_implicit_resolver(
    "tag:yaml.org,2002:bool",
    re.compile(r"^(?:true|True|TRUE|false|False|FALSE)$"),
    list("tTfF"),
)
AprYamlLoader.add_implicit_resolver(
    "tag:yaml.org,2002:float",
    re.compile(r"^" + _JSON_NUMBER + r"$"),
    list("-0123456789"),
)


def reject_yaml_features(source: str) -> None:
    """Refuse the constructs the specification excludes (4.5, APR-REP-010).

    The check runs on the parser's event stream rather than on the source text.
    An anchor, alias or tag is a node property, so it only exists where the
    parser reports one: ``&``, ``*`` and ``!`` inside a plain scalar's content,
    as in ``string(fee_count * 8.0)``, are ordinary characters of an ordinary
    string. A merge key is a plain ``<<`` in key position; a quoted ``"<<"`` is a
    string key like any other. Directives arrive on the document-start event.
    Running before the composer also means an alias never reaches the part of
    the library that would expand it.
    """
    for event, is_key in _events_with_key_position(source):
        if isinstance(event, yaml.DocumentStartEvent):
            if event.version is not None or event.tags:
                raise AprYamlError("APR YAML forbids directives, including %YAML and %TAG")
        elif isinstance(event, yaml.AliasEvent):
            raise AprYamlError("APR YAML forbids aliases")
        elif isinstance(event, yaml.NodeEvent):
            if event.anchor is not None:
                raise AprYamlError("APR YAML forbids anchors")
            if event.tag is not None:
                raise AprYamlError("APR YAML forbids tags")
            if isinstance(event, yaml.ScalarEvent) and event.style is None:
                if is_key and event.value == "<<":
                    raise AprYamlError("APR YAML forbids merge keys")
                if _NON_FINITE.match(event.value):
                    # A non-finite float has no JSON value, so it is refused
                    # rather than coerced. [APR-REP-011]
                    raise AprYamlError("APR YAML forbids a non-finite number: JSON cannot represent it")


def _events_with_key_position(source: str) -> Iterator[tuple[yaml.Event, bool]]:
    """Yield each parser event with whether it opens a mapping key.

    A mapping alternates key and value nodes, so a frame per open mapping counts
    the nodes seen so far; a sequence frame never reports a key. Nested
    collections count as one node of their parent when they start.
    """
    frames: List[List[Any]] = []  # [is_mapping, node_count]
    for event in yaml.parse(source, Loader=AprYamlLoader):
        is_key = False
        if isinstance(event, (yaml.ScalarEvent, yaml.AliasEvent, yaml.CollectionStartEvent)) and frames:
            frame = frames[-1]
            is_key = frame[0] and frame[1] % 2 == 0
            frame[1] += 1
        yield event, is_key
        if isinstance(event, yaml.CollectionStartEvent):
            frames.append([isinstance(event, yaml.MappingStartEvent), 0])
        elif isinstance(event, yaml.CollectionEndEvent):
            frames.pop()


def load_all(source: str) -> List[Any]:
    """Read every document of an APR-YAML stream into JSON values.

    Raises AprYamlError for an excluded construct or value and yaml.YAMLError
    for a document that is not well-formed YAML.
    """
    reject_yaml_features(source)
    return list(yaml.load_all(source, AprYamlLoader))
