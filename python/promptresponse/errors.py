"""The two ways reading a document can fail.

Specification 6.3 keeps these apart on purpose: a parse failure means the bytes
are not an APR document, while a validation error means the document is one and
has something wrong with it. Conflating them is what stops a reader from opening
a flawed form and showing somebody what is wrong with it.
"""


class AprParseError(Exception):
    """The bytes are not a well-formed APR document (specification 6.3)."""


class AprVersionError(AprParseError):
    """The document declares a major version this reader does not implement."""
