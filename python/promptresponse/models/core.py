"""
Core data models for APR (Adaptive Prompt Response) documents.
"""
from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from typing import List, Optional, Dict, Any


class DocumentType(Enum):
    """Type of APR document."""
    TEMPLATE = "template"
    FILLED_FORM = "filledForm"


@dataclass
class PromptHints:
    """
    Hints and guidance for a prompt.
    All hints are suggestions only and never enforced.
    """
    expected_data_type: Optional[str] = None
    placeholder: Optional[str] = None
    help_text: Optional[str] = None
    suggested_values: List[str] = field(default_factory=list)
    min_length: Optional[int] = None
    max_length: Optional[int] = None
    pattern: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        result = {}
        if self.expected_data_type:
            result['expectedDataType'] = self.expected_data_type
        if self.placeholder:
            result['placeholder'] = self.placeholder
        if self.help_text:
            result['helpText'] = self.help_text
        if self.suggested_values:
            result['suggestedValues'] = self.suggested_values
        if self.min_length is not None:
            result['minLength'] = self.min_length
        if self.max_length is not None:
            result['maxLength'] = self.max_length
        if self.pattern:
            result['pattern'] = self.pattern
        return result

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'PromptHints':
        """Create from dictionary."""
        return cls(
            expected_data_type=data.get('expectedDataType'),
            placeholder=data.get('placeholder'),
            help_text=data.get('helpText'),
            suggested_values=data.get('suggestedValues', []),
            min_length=data.get('minLength'),
            max_length=data.get('maxLength'),
            pattern=data.get('pattern')
        )


@dataclass
class ResponseMetadata:
    """Metadata about when and how a response was provided."""
    last_modified: Optional[datetime] = None

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        result = {}
        if self.last_modified:
            result['lastModified'] = self.last_modified.isoformat()
        return result

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'ResponseMetadata':
        """Create from dictionary."""
        last_modified = None
        if 'lastModified' in data:
            last_modified = datetime.fromisoformat(data['lastModified'].replace('Z', '+00:00'))
        return cls(last_modified=last_modified)


@dataclass
class Prompt:
    """
    A single prompt/question in an APR form.
    Responses are ALWAYS stored as strings.
    """
    id: str
    label: str
    response: str = ""
    hints: Optional[PromptHints] = None
    response_metadata: ResponseMetadata = field(default_factory=ResponseMetadata)

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        result = {
            'id': self.id,
            'label': self.label,
            'response': self.response,
            'responseMetadata': self.response_metadata.to_dict()
        }
        if self.hints:
            result['hints'] = self.hints.to_dict()
        return result

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'Prompt':
        """Create from dictionary."""
        hints = None
        if 'hints' in data:
            hints = PromptHints.from_dict(data['hints'])

        response_metadata = ResponseMetadata()
        if 'responseMetadata' in data:
            response_metadata = ResponseMetadata.from_dict(data['responseMetadata'])

        return cls(
            id=data['id'],
            label=data['label'],
            response=data.get('response', ''),
            hints=hints,
            response_metadata=response_metadata
        )


@dataclass
class Subsection:
    """A subsection within a section (3-level hierarchy max)."""
    id: str
    title: str
    description: Optional[str] = None
    prompts: List[Prompt] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        result = {
            'id': self.id,
            'title': self.title,
            'prompts': [p.to_dict() for p in self.prompts]
        }
        if self.description:
            result['description'] = self.description
        return result

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'Subsection':
        """Create from dictionary."""
        prompts = [Prompt.from_dict(p) for p in data.get('prompts', [])]
        return cls(
            id=data['id'],
            title=data['title'],
            description=data.get('description'),
            prompts=prompts
        )


@dataclass
class Section:
    """A top-level section in an APR document."""
    id: str
    title: str
    description: Optional[str] = None
    prompts: List[Prompt] = field(default_factory=list)
    subsections: List[Subsection] = field(default_factory=list)

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        result = {
            'id': self.id,
            'title': self.title,
            'prompts': [p.to_dict() for p in self.prompts],
            'subsections': [s.to_dict() for s in self.subsections]
        }
        if self.description:
            result['description'] = self.description
        return result

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'Section':
        """Create from dictionary."""
        prompts = [Prompt.from_dict(p) for p in data.get('prompts', [])]
        subsections = [Subsection.from_dict(s) for s in data.get('subsections', [])]
        return cls(
            id=data['id'],
            title=data['title'],
            description=data.get('description'),
            prompts=prompts,
            subsections=subsections
        )


@dataclass
class DigitalSignature:
    """Digital signature for a template."""
    signer_name: str
    signer_email: str
    signature_algorithm: str
    signature_value: str
    certificate: str
    signed_date: datetime
    template_hash: str

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        return {
            'signerName': self.signer_name,
            'signerEmail': self.signer_email,
            'signatureAlgorithm': self.signature_algorithm,
            'signatureValue': self.signature_value,
            'certificate': self.certificate,
            'signedDate': self.signed_date.isoformat(),
            'templateHash': self.template_hash
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'DigitalSignature':
        """Create from dictionary."""
        signed_date = datetime.fromisoformat(data['signedDate'].replace('Z', '+00:00'))
        return cls(
            signer_name=data['signerName'],
            signer_email=data['signerEmail'],
            signature_algorithm=data['signatureAlgorithm'],
            signature_value=data['signatureValue'],
            certificate=data['certificate'],
            signed_date=signed_date,
            template_hash=data['templateHash']
        )


@dataclass
class SubmissionConfig:
    """Configuration for S3 form submission."""
    type: str
    url: str
    fields: Dict[str, str]
    expires_at: Optional[datetime] = None
    headers: Optional[Dict[str, str]] = None

    def is_expired(self) -> bool:
        """Check if submission config has expired."""
        if self.expires_at is None:
            return False
        return datetime.utcnow() > self.expires_at

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        result = {
            'type': self.type,
            'url': self.url,
            'fields': self.fields
        }
        if self.expires_at:
            result['expiresAt'] = self.expires_at.isoformat()
        if self.headers:
            result['headers'] = self.headers
        return result

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'SubmissionConfig':
        """Create from dictionary."""
        expires_at = None
        if 'expiresAt' in data:
            expires_at = datetime.fromisoformat(data['expiresAt'].replace('Z', '+00:00'))
        return cls(
            type=data['type'],
            url=data['url'],
            fields=data['fields'],
            expires_at=expires_at,
            headers=data.get('headers')
        )


@dataclass
class Metadata:
    """Metadata for an APR document."""
    title: Optional[str] = None
    description: Optional[str] = None
    author: Optional[str] = None
    created: Optional[datetime] = None
    modified: Optional[datetime] = None
    template_id: Optional[str] = None
    template_version: Optional[str] = None
    filled_by: Optional[str] = None
    filled_date: Optional[datetime] = None
    template_signatures: List[DigitalSignature] = field(default_factory=list)
    submission_config: Optional[SubmissionConfig] = None

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        result = {}
        if self.title:
            result['title'] = self.title
        if self.description:
            result['description'] = self.description
        if self.author:
            result['author'] = self.author
        if self.created:
            result['created'] = self.created.isoformat()
        if self.modified:
            result['modified'] = self.modified.isoformat()
        if self.template_id:
            result['templateId'] = self.template_id
        if self.template_version:
            result['templateVersion'] = self.template_version
        if self.filled_by:
            result['filledBy'] = self.filled_by
        if self.filled_date:
            result['filledDate'] = self.filled_date.isoformat()
        if self.template_signatures:
            result['templateSignatures'] = [sig.to_dict() for sig in self.template_signatures]
        if self.submission_config:
            result['submissionConfig'] = self.submission_config.to_dict()
        return result

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'Metadata':
        """Create from dictionary."""
        created = None
        if 'created' in data:
            created = datetime.fromisoformat(data['created'].replace('Z', '+00:00'))

        modified = None
        if 'modified' in data:
            modified = datetime.fromisoformat(data['modified'].replace('Z', '+00:00'))

        filled_date = None
        if 'filledDate' in data:
            filled_date = datetime.fromisoformat(data['filledDate'].replace('Z', '+00:00'))

        template_signatures = []
        if 'templateSignatures' in data:
            template_signatures = [DigitalSignature.from_dict(sig) for sig in data['templateSignatures']]

        submission_config = None
        if 'submissionConfig' in data:
            submission_config = SubmissionConfig.from_dict(data['submissionConfig'])

        return cls(
            title=data.get('title'),
            description=data.get('description'),
            author=data.get('author'),
            created=created,
            modified=modified,
            template_id=data.get('templateId'),
            template_version=data.get('templateVersion'),
            filled_by=data.get('filledBy'),
            filled_date=filled_date,
            template_signatures=template_signatures,
            submission_config=submission_config
        )


@dataclass
class AprDocument:
    """
    Root APR document.
    Can be either a template or a filled form.
    """
    version: str
    document_type: DocumentType
    sections: List[Section]
    metadata: Optional[Metadata] = None

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        result = {
            'version': self.version,
            'documentType': self.document_type.value,
            'sections': [s.to_dict() for s in self.sections]
        }
        if self.metadata:
            result['metadata'] = self.metadata.to_dict()
        return result

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'AprDocument':
        """Create from dictionary."""
        document_type = DocumentType(data['documentType'])
        sections = [Section.from_dict(s) for s in data.get('sections', [])]

        metadata = None
        if 'metadata' in data:
            metadata = Metadata.from_dict(data['metadata'])

        return cls(
            version=data['version'],
            document_type=document_type,
            sections=sections,
            metadata=metadata
        )

    def get_all_prompts(self) -> List[Prompt]:
        """Get all prompts in the document (flattened)."""
        prompts = []
        for section in self.sections:
            prompts.extend(section.prompts)
            for subsection in section.subsections:
                prompts.extend(subsection.prompts)
        return prompts

    def get_prompt_by_id(self, prompt_id: str) -> Optional[Prompt]:
        """Find a prompt by its ID."""
        for prompt in self.get_all_prompts():
            if prompt.id == prompt_id:
                return prompt
        return None

    def get_completion_percentage(self) -> float:
        """Calculate percentage of prompts that have been filled."""
        all_prompts = self.get_all_prompts()
        if not all_prompts:
            return 0.0

        filled = sum(1 for p in all_prompts if p.response.strip())
        return (filled / len(all_prompts)) * 100.0
