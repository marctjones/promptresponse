"""Result types shared by structural validation and advisory analysis."""

from dataclasses import dataclass, field
from typing import List


@dataclass
class ValidationError:
    code: str
    message: str
    path: str


@dataclass
class ValidationWarning:
    code: str
    message: str
    path: str


@dataclass
class ValidationResult:
    errors: List[ValidationError] = field(default_factory=list)
    warnings: List[ValidationWarning] = field(default_factory=list)

    @property
    def is_valid(self) -> bool:
        """Warnings never make a document invalid."""
        return not self.errors
