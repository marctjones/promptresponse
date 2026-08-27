"""PromptResponse - a reader and writer for the APR form format.

Implements the **core profile** (specification 2). Expression hints and
signatures are parsed, preserved and written back untouched: this reader never
evaluates an expression and never reports a signature as verified, which is
exactly what the core profile requires (2.2, 2.3).
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
from .serialization import (
    CURRENT_VERSION,
    KNOWN_MAJOR,
    KNOWN_MINOR,
    dump,
    dumps,
    load,
    loads,
)
from .validation import ValidationError, ValidationResult, ValidationWarning, validate

#: The profile this implementation provides. Named so a caller can check rather
#: than assume, and so the conformance runner can assert it.
PROFILE = "core"

__all__ = [
    "AprDocument", "Metadata", "Prompt", "PromptHints", "ResponseMetadata",
    "RoleDefinition", "Section",
    "AprParseError", "AprVersionError",
    "load", "loads", "dump", "dumps",
    "validate", "ValidationError", "ValidationResult", "ValidationWarning",
    "CURRENT_VERSION", "KNOWN_MAJOR", "KNOWN_MINOR", "PROFILE",
]
