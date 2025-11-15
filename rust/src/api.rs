//! High-level API for building and filling forms

use crate::models::*;
use crate::serialization;
use chrono::Utc;
use std::collections::HashMap;

/// Builder for creating APR templates
pub struct TemplateBuilder {
    metadata: Metadata,
    sections: Vec<Section>,
    section_counter: usize,
    subsection_counter: usize,
    prompt_counter: usize,
}

impl TemplateBuilder {
    pub fn new(title: impl Into<String>, template_id: impl Into<String>) -> Self {
        let mut metadata = Metadata::default();
        metadata.title = Some(title.into());
        metadata.template_id = Some(template_id.into());
        metadata.created = Some(Utc::now());
        metadata.modified = Some(Utc::now());

        Self {
            metadata,
            sections: Vec::new(),
            section_counter: 0,
            subsection_counter: 0,
            prompt_counter: 0,
        }
    }

    pub fn description(mut self, description: impl Into<String>) -> Self {
        self.metadata.description = Some(description.into());
        self
    }

    pub fn author(mut self, author: impl Into<String>) -> Self {
        self.metadata.author = Some(author.into());
        self
    }

    pub fn version(mut self, version: impl Into<String>) -> Self {
        self.metadata.template_version = Some(version.into());
        self
    }

    pub fn section(mut self, title: impl Into<String>) -> SectionBuilder {
        self.section_counter += 1;
        let section_id = format!("section_{:03}", self.section_counter);
        let section = Section::new(section_id, title);
        SectionBuilder::new(self, section)
    }

    pub fn build(mut self) -> AprDocument {
        self.metadata.modified = Some(Utc::now());

        AprDocument {
            version: "1.0".to_string(),
            document_type: DocumentType::Template,
            sections: self.sections,
            metadata: Some(self.metadata),
        }
    }

    fn next_prompt_id(&mut self) -> String {
        self.prompt_counter += 1;
        format!("prompt_{:03}", self.prompt_counter)
    }
}

pub struct SectionBuilder {
    template_builder: TemplateBuilder,
    section: Section,
}

impl SectionBuilder {
    fn new(template_builder: TemplateBuilder, section: Section) -> Self {
        Self {
            template_builder,
            section,
        }
    }

    pub fn prompt(mut self, label: impl Into<String>, expected_type: impl Into<String>) -> Self {
        let prompt_id = self.template_builder.next_prompt_id();
        let mut prompt = Prompt::new(prompt_id, label);

        let type_str = expected_type.into();
        if !type_str.is_empty() {
            let mut hints = PromptHints::default();
            hints.expected_data_type = Some(type_str);
            prompt.hints = Some(hints);
        }

        self.section.prompts.push(prompt);
        self
    }

    pub fn done(mut self) -> TemplateBuilder {
        self.template_builder.sections.push(self.section);
        self.template_builder
    }
}

/// Fill a form programmatically
pub fn fill_form(
    template: &AprDocument,
    responses: HashMap<String, String>,
    filled_by: Option<String>,
) -> Result<AprDocument, serialization::SerializationError> {
    if template.document_type != DocumentType::Template {
        panic!("Document must be a template");
    }

    // Deep copy via serialization
    let json = serialization::serialize(template)?;
    let mut filled_form = serialization::deserialize(&json)?;

    // Convert to filled form
    filled_form.document_type = DocumentType::FilledForm;

    if let Some(metadata) = &mut filled_form.metadata {
        metadata.filled_by = filled_by;
        metadata.filled_date = Some(Utc::now());
        metadata.modified = Some(Utc::now());
    }

    // Apply responses
    for (prompt_id, response) in responses {
        if let Some(prompt) = filled_form.get_prompt_by_id_mut(&prompt_id) {
            prompt.response = response;
            prompt.response_metadata.last_modified = Some(Utc::now());
        }
    }

    Ok(filled_form)
}

/// Get completion percentage
pub fn get_completion_percentage(document: &AprDocument) -> f64 {
    document.get_completion_percentage()
}

/// Get empty prompts
pub fn get_empty_prompts(document: &AprDocument) -> HashMap<String, String> {
    document
        .get_all_prompts()
        .into_iter()
        .filter(|p| p.response.trim().is_empty())
        .map(|p| (p.id.clone(), p.label.clone()))
        .collect()
}
