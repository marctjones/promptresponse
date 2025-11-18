"""
High-level API for building APR templates programmatically.
"""
from datetime import datetime
from typing import Optional, List
from ..models import (
    AprDocument,
    DocumentType,
    Section,
    Subsection,
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
        self._subsection_counter = 0
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
            subsections=[]
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

    def __init__(self, template_builder: TemplateBuilder, section: Section):
        """
        Initialize section builder.

        Args:
            template_builder: Parent template builder
            section: Section being built
        """
        self.template_builder = template_builder
        self.section = section

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

    def add_subsection(self, title: str, description: Optional[str] = None) -> 'SubsectionBuilder':
        """
        Add a subsection to this section.

        Args:
            title: Subsection title
            description: Optional subsection description

        Returns:
            SubsectionBuilder for building the subsection
        """
        self.template_builder._subsection_counter += 1
        subsection_id = f"{self.section.id}_subsection_{self.template_builder._subsection_counter}"

        subsection = Subsection(
            id=subsection_id,
            title=title,
            description=description,
            prompts=[]
        )

        return SubsectionBuilder(self, subsection)

    def done(self) -> TemplateBuilder:
        """
        Finish building this section and return to template builder.

        Returns:
            Parent template builder
        """
        self.template_builder.sections.append(self.section)
        return self.template_builder


class SubsectionBuilder:
    """Builder for creating subsections."""

    def __init__(self, section_builder: SectionBuilder, subsection: Subsection):
        """
        Initialize subsection builder.

        Args:
            section_builder: Parent section builder
            subsection: Subsection being built
        """
        self.section_builder = section_builder
        self.subsection = subsection

    def add_prompt(
        self,
        label: str,
        expected_type: Optional[str] = None,
        placeholder: Optional[str] = None,
        help_text: Optional[str] = None
    ) -> 'SubsectionBuilder':
        """
        Add a prompt to this subsection.

        Args:
            label: Prompt label/question
            expected_type: Expected data type (text, email, phone, etc.)
            placeholder: Placeholder text
            help_text: Help text for the prompt

        Returns:
            Self for chaining
        """
        self.section_builder.template_builder._prompt_counter += 1
        prompt_id = f"prompt_{self.section_builder.template_builder._prompt_counter:03d}"

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

        self.subsection.prompts.append(prompt)
        return self

    def done(self) -> SectionBuilder:
        """
        Finish building this subsection and return to section builder.

        Returns:
            Parent section builder
        """
        self.section_builder.section.subsections.append(self.subsection)
        return self.section_builder
