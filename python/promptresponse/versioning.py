"""APR beta.6 wire-version policy independent of model parsing."""

from typing import Optional

CURRENT_VERSION = "1.0-beta.6"


def is_supported_version(version: Optional[str]) -> bool:
    """Whether a document declares the sole supported APR wire version."""
    return version == CURRENT_VERSION
