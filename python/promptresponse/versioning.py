"""APR version compatibility independent of model parsing."""

from typing import Optional

KNOWN_MAJOR = 1
KNOWN_MINOR = 0
CURRENT_VERSION = "1.0-beta"


def is_supported_version(version: Optional[str]) -> bool:
    """Whether a document uses this reader's compatible major version."""
    if not version:
        return False
    core = version.split("-", 1)[0]
    parts = core.split(".")
    return len(parts) == 2 and all(part.isdigit() for part in parts) and int(parts[0]) == KNOWN_MAJOR
