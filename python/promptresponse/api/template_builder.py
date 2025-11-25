"""
High-level API for building APR templates programmatically.
"""
from datetime import datetime
from typing import Optional, List
from ..models import (
    AprDocument,
    DocumentType,
    Section,
    Prompt,
    PromptHints,
    Metadata
)


class TemplateBuilder:
    """Builder for creating APR templates programmatically."""

    def __init__(self, title: str, template_id: Optional[str] = None):
        """
        Initialize a new template builder.

        Args:
            title: Template title
            template_id: Optional template identifier
        """
        self.metadata = Metadata(
            title=title,
            template_id=template_id,
            created=datetime.utcnow(),
            modified=datetime.utcnow()
        )
        self.sections: List[Section] = []
        self._section_counter = 0
        self._prompt_counter = 0

    def set_description(self, description: str) -> 'TemplateBuilder':
        """Set template description."""
        self.metadata.description = description
        return self

    def set_author(self, author: str) -> 'TemplateBuilder':
        """Set template author."""
        self.metadata.author = author
        return self

    def set_version(self, version: str) -> 'TemplateBuilder':
        """Set template version."""
        self.metadata.template_version = version
        return self

    def add_section(self, title: str, description: Optional[str] = None) -> 'SectionBuilder':
        """
        Add a new section to the template.

        Args:
            title: Section title
            description: Optional section description

        Returns:
            SectionBuilder for building the section
        """
        self._section_counter += 1
        section_id = f"section_{self._section_counter:03d}"

        section = Section(
            id=section_id,
            title=title,
            description=description,
            prompts=[],
            sections=[]
        )

        section_builder = SectionBuilder(self, section)
        return section_builder

    def build(self) -> AprDocument:
        """
        Build the final APR template document.

        Returns:
            Complete APR template document
        """
        self.metadata.modified = datetime.utcnow()

        return AprDocument(
            version="1.0",
            document_type=DocumentType.TEMPLATE,
            sections=self.sections,
            metadata=self.metadata
        )


class SectionBuilder:
    """Builder for creating sections in a template."""

    def __init__(self, template_builder: TemplateBuilder, section: Section, parent_builder: Optional['SectionBuilder'] = None):
        """
        Initialize section builder.

        Args:
            template_builder: Parent template builder
            section: Section being built
            parent_builder: Parent section builder (for nested sections)
        """
        self.template_builder = template_builder
        self.section = section
        self._parent_builder = parent_builder

    def add_prompt(
        self,
        label: str,
        expected_type: Optional[str] = None,
        placeholder: Optional[str] = None,
        help_text: Optional[str] = None
    ) -> 'SectionBuilder':
        """
        Add a prompt to this section.

        Args:
            label: Prompt label/question
            expected_type: Expected data type (text, email, phone, etc.)
            placeholder: Placeholder text
            help_text: Help text for the prompt

        Returns:
            Self for chaining
        """
        self.template_builder._prompt_counter += 1
        prompt_id = f"prompt_{self.template_builder._prompt_counter:03d}"

        hints = None
        if expected_type or placeholder or help_text:
            hints = PromptHints(
                expected_data_type=expected_type,
                placeholder=placeholder,
                help_text=help_text
            )

        prompt = Prompt(
            id=prompt_id,
            label=label,
            hints=hints
        )

        self.section.prompts.append(prompt)
        return self

    def add_section(self, title: str, description: Optional[str] = None) -> 'SectionBuilder':
        """
        Add a nested section to this section.

        Args:
            title: Nested section title
            description: Optional nested section description

        Returns:
            SectionBuilder for building the nested section
        """
        self.template_builder._section_counter += 1
        section_id = f"section_{self.template_builder._section_counter:03d}"

        nested_section = Section(
            id=section_id,
            title=title,
            description=description,
            prompts=[],
            sections=[]
        )

        return SectionBuilder(self.template_builder, nested_section, parent_builder=self)

    def done(self) -> TemplateBuilder:
        """
        Finish building this section and return to template builder.

        Returns:
            Parent template builder or parent section builder
        """
        if self._parent_builder is not None:
            self._parent_builder.section.sections.append(self.section)
            return self._parent_builder
        self.template_builder.sections.append(self.section)
        return self.template_builder
