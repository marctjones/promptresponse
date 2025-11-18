"""
PromptResponse - Python library for working with APR (Adaptive Prompt Response) forms.

This library provides a complete toolkit for creating, filling, validating,
and signing APR form documents.
"""

__version__ = "0.1.0"

# Core models
from .models import (
    AprDocument,
    DocumentType,
    Section,
    Subsection,
    Prompt,
    PromptHints,
    ResponseMetadata,
    Metadata,
    DigitalSignature,
    SubmissionConfig
)

# Serialization
from .serialization import AprJsonSerializer

# Validation
from .validation import AprValidator, ValidationResult, ValidationError

# Signatures (optional - requires cryptography)
try:
    from .signatures import TemplateSigner, SignatureVerifier, SignatureError
    SIGNATURES_AVAILABLE = True
except ImportError:
    SIGNATURES_AVAILABLE = False
    TemplateSigner = None
    SignatureVerifier = None
    SignatureError = None

# High-level API
from .api import TemplateBuilder, FormFiller

__all__ = [
    # Core models
    'AprDocument',
    'DocumentType',
    'Section',
    'Subsection',
    'Prompt',
    'PromptHints',
    'ResponseMetadata',
    'Metadata',
    'DigitalSignature',
    'SubmissionConfig',
    # Serialization
    'AprJsonSerializer',
    # Validation
    'AprValidator',
    'ValidationResult',
    'ValidationError',
    # Signatures
    'TemplateSigner',
    'SignatureVerifier',
    'SignatureError',
    # API
    'TemplateBuilder',
    'FormFiller',
]
