"""Who each part of a form is meant for (specification 4.10).

A role says who a field is *for*. It never says who may type into it: the
format has no identity at fill time, so a reader marks a field and still accepts
input. Accountability comes from signatures, not from a greyed-out box.
"""

from typing import Iterable, List, Optional, Tuple

from .models import AprDocument, Prompt, RoleDefinition, Section


def resolve(document: AprDocument) -> Iterable[Tuple[Prompt, Optional[str]]]:
    """Every prompt paired with the role in force for it.

    A prompt's own role wins over the section containing it, so a single field
    can be handed back to the patient without splitting a nurse's section in two.
    """

    def walk(section: Section, inherited: Optional[str]):
        here = section.role if (section.role or "").strip() else inherited
        for prompt in section.prompts:
            yield prompt, (prompt.role if (prompt.role or "").strip() else here)
        for child in section.sections:
            yield from walk(child, here)

    for section in document.sections:
        yield from walk(section, None)


def used(document: AprDocument) -> List[str]:
    """Every distinct role the document assigns, in document order."""
    seen: List[str] = []
    for _, role in resolve(document):
        if role and role not in seen:
            seen.append(role)
    return seen


def definition(document: AprDocument, role: Optional[str]) -> Optional[RoleDefinition]:
    """The declaration for a role, or None when it was never declared."""
    if not role or not document.roles:
        return None
    return next((r for r in document.roles if r.id == role), None)


def display_name(document: AprDocument, role: Optional[str]) -> Optional[str]:
    """The name to show: the declared one, else the identifier itself.

    Undeclared is not an error - the vocabulary is open, so a reader shows the
    identifier rather than refusing the document.
    """
    if not role:
        return None
    found = definition(document, role)
    return found.display_name if found else role
