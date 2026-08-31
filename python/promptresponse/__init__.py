"""PromptResponse - a reader and writer for the APR form format.

Implements the APR core and optional CEL expression profiles. Signatures are
preserved but not verified.
"""

from .errors import AprParseError, AprVersionError
from .models import (
    AprDocument,
    Metadata,
    Prompt,
    PromptHints,
    ResponseMetadata,
    RoleDefinition,
    Section,
)
from .serialization import dump, dumps, load, loads
from .versioning import CURRENT_VERSION, KNOWN_MAJOR, KNOWN_MINOR
from .validation import ValidationError, ValidationResult, ValidationWarning, validate
from .unicode_security import UnicodeFinding, inspect_text
from .expressions import (
    COMPUTED_SOURCE, ExpressionContext, build_expression_context, compute_value,
    condition, validation_message, recompute_computed_values,
)

#: The profile this implementation provides. Named so a caller can check rather
#: than assume, and so the conformance runner can assert it.
PROFILE = "core+expressions"

__all__ = [
    "AprDocument", "Metadata", "Prompt", "PromptHints", "ResponseMetadata",
    "RoleDefinition", "Section",
    "AprParseError", "AprVersionError",
    "load", "loads", "dump", "dumps",
    "validate", "ValidationError", "ValidationResult", "ValidationWarning",
    "inspect_text", "UnicodeFinding",
    "ExpressionContext", "COMPUTED_SOURCE", "build_expression_context",
    "compute_value", "condition", "validation_message", "recompute_computed_values",
    "CURRENT_VERSION", "KNOWN_MAJOR", "KNOWN_MINOR", "PROFILE",
]
