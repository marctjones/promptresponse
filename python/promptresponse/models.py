"""The APR object model.

Every class keeps an ``extra`` mapping of members it did not recognise, and puts
them back when writing. Specification 4.8 requires that: without it, a document
written by a newer minor version loses its new members the first time an older
reader opens and saves it, which would make every additive change destructive.
"""

from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional

# Members retired from the format. Dropped on write rather than preserved, so a
# document does not carry a contradiction forward (specification 4.8.1).
RETIRED_MEMBERS = frozenset(["tableLayout", "columns", "fixedRows"])


@dataclass
class PromptHints:
    """Advisory only. A hint suggests an affordance; it never restricts a response."""

    expected_data_type: Optional[str] = None
    placeholder: Optional[str] = None
    help_text: Optional[str] = None
    validation_pattern: Optional[str] = None
    suggested_values: List[str] = field(default_factory=list)
    # Bounds (specification 4.7). Strings, like every other value, and an offer
    # rather than a limit: a response outside them is still valid.
    min: Optional[str] = None
    max: Optional[str] = None
    step: Optional[str] = None
    # Expression hints. This reader is core-only: it carries them through
    # untouched and never evaluates them (specification 2.2).
    expr_hidden: Optional[str] = None
    expr_value: Optional[str] = None
    expr_expected: Optional[str] = None
    expr_validation: Optional[str] = None
    expr_read_only: Optional[str] = None
    extra: Dict[str, Any] = field(default_factory=dict)


@dataclass
class ResponseMetadata:
    """Provenance for a response."""

    inferred_data_type: Optional[str] = None
    # "computed" when an exprValue produced the value, absent when a person or
    # an API wrote it. What stops a recomputation overwriting a correction.
    source: Optional[str] = None
    last_modified: Optional[str] = None
    extra: Dict[str, Any] = field(default_factory=dict)


@dataclass
class Prompt:
    """A question and its answer. The answer is always a string."""

    id: str = ""
    label: str = ""
    response: str = ""
    role: Optional[str] = None
    hints: PromptHints = field(default_factory=PromptHints)
    response_metadata: ResponseMetadata = field(default_factory=ResponseMetadata)
    extra: Dict[str, Any] = field(default_factory=dict)


@dataclass
class Section:
    """A group of prompts, which may contain further sections."""

    id: str = ""
    title: str = ""
    description: Optional[str] = None
    # "table" when the child sections are repeating instances. A table adds no
    # new primitive: rows are sections, cells are prompts (specification 4.5).
    kind: Optional[str] = None
    can_add_rows: Optional[str] = None
    max_rows: Optional[str] = None
    role: Optional[str] = None
    prompts: List[Prompt] = field(default_factory=list)
    sections: List["Section"] = field(default_factory=list)
    extra: Dict[str, Any] = field(default_factory=dict)


@dataclass
class RoleDefinition:
    """A party the author expects to fill part of the form (specification 4.10)."""

    id: str = ""
    name: Optional[str] = None
    description: Optional[str] = None
    extra: Dict[str, Any] = field(default_factory=dict)

    @property
    def display_name(self) -> str:
        """What to show a person: the declared name, else the identifier."""
        return self.name if self.name and self.name.strip() else self.id


@dataclass
class Metadata:
    """Document-level facts."""

    title: str = ""
    description: Optional[str] = None
    author: Optional[str] = None
    created: Optional[str] = None
    modified: Optional[str] = None
    template_id: Optional[str] = None
    template_version: Optional[str] = None
    filled_by: Optional[str] = None
    filled_date: Optional[str] = None
    publisher: Optional[str] = None
    submission_urls: Optional[List[str]] = None
    extra: Dict[str, Any] = field(default_factory=dict)


@dataclass
class AprDocument:
    """An APR document."""

    version: str = "1.0-beta"
    document_type: Optional[str] = None
    metadata: Metadata = field(default_factory=Metadata)
    sections: List[Section] = field(default_factory=list)
    roles: Optional[List[RoleDefinition]] = None
    # Carried through untouched and never verified: this reader implements the
    # core profile, so it must preserve signatures and must not report them as
    # verified (specification 2.3).
    signatures: Optional[List[Dict[str, Any]]] = None
    extra: Dict[str, Any] = field(default_factory=dict)

    def all_prompts(self):
        """Every prompt in the document, depth first."""

        def walk(section):
            yield from section.prompts
            for child in section.sections:
                yield from walk(child)

        for section in self.sections:
            yield from walk(section)
