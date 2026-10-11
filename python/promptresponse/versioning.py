"""APR wire-version policy independent of model parsing."""

from typing import Optional

CURRENT_VERSION = "1.0-beta.7"
"""The version a new document states."""

ACCEPTED_VERSIONS = ("1.0-beta.7", "1.0-beta.6")
"""Every version a reader accepts; beta.7 adds no member, so beta.6 records are beta.7 records."""


def is_supported_version(version: Optional[str]) -> bool:
    """Whether a document declares a wire version this build reads."""
    return version in ACCEPTED_VERSIONS
