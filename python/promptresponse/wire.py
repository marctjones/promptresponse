"""Small JSON-wire helpers shared by APR parsing and writing.

These helpers deliberately know nothing about APR models. They centralize the
wire-type and unknown-member invariants so model mapping remains readable.
"""

from typing import Any, Dict, Iterable

from .errors import AprParseError
from .models import RETIRED_MEMBERS


def require_object(value: Any, what: str) -> Dict[str, Any]:
    if not isinstance(value, dict):
        raise AprParseError(f"{what} must be a JSON object, got {type(value).__name__}")
    return value


def string_member(node: Dict[str, Any], key: str, what: str):
    """Return an optional string; never coerce non-string JSON values."""
    if key not in node or node[key] is None:
        return None
    value = node[key]
    if not isinstance(value, str):
        raise AprParseError(
            f"{what}.{key} must be a string; got {type(value).__name__}. "
            "Every value in an APR document is a string (specification 3.2), and a "
            "reader must refuse rather than coerce."
        )
    return value


def string_list_member(node: Dict[str, Any], key: str, what: str):
    if key not in node or node[key] is None:
        return None
    value = node[key]
    if not isinstance(value, list) or any(not isinstance(item, str) for item in value):
        raise AprParseError(f"{what}.{key} must be an array of strings")
    return value


def unknown_members(node: Dict[str, Any], known: Iterable[str]) -> Dict[str, Any]:
    """Preserve compatible extension members but omit explicitly retired ones."""
    taken = set(known)
    return {key: value for key, value in node.items() if key not in taken and key not in RETIRED_MEMBERS}


def compact_members(node: Dict[str, Any]) -> Dict[str, Any]:
    """Omit absent or empty optional members from a writer's JSON object."""
    return {key: value for key, value in node.items() if value is not None and value != [] and value != {}}
