//! Validation for APR documents

use crate::models::*;
use std::collections::HashSet;

#[derive(Debug, Clone, PartialEq)]
pub enum ValidationSeverity {
    Error,
    Warning,
}

#[derive(Debug, Clone)]
pub struct ValidationError {
    pub field: String,
    pub message: String,
    pub severity: ValidationSeverity,
}

impl ValidationError {
    pub fn error(field: impl Into<String>, message: impl Into<String>) -> Self {
        Self {
            field: field.into(),
            message: message.into(),
            severity: ValidationSeverity::Error,
        }
    }

    pub fn warning(field: impl Into<String>, message: impl Into<String>) -> Self {
        Self {
            field: field.into(),
            message: message.into(),
            severity: ValidationSeverity::Warning,
        }
    }
}

#[derive(Debug, Clone)]
pub struct ValidationResult {
    pub is_valid: bool,
    pub errors: Vec<ValidationError>,
}

impl ValidationResult {
    pub fn new() -> Self {
        Self {
            is_valid: true,
            errors: Vec::new(),
        }
    }

    pub fn add_error(&mut self, error: ValidationError) {
        if error.severity == ValidationSeverity::Error {
            self.is_valid = false;
        }
        self.errors.push(error);
    }
}

impl Default for ValidationResult {
    fn default() -> Self {
        Self::new()
    }
}

/// Validate APR document structure
pub fn validate(document: &AprDocument) -> ValidationResult {
    let mut result = ValidationResult::new();

    // Version validation
    if document.version != "1.0" {
        result.add_error(ValidationError::error(
            "version",
            format!("Unsupported version: {}", document.version),
        ));
    }

    // Sections validation
    if document.sections.is_empty() {
        result.add_error(ValidationError::error(
            "sections",
            "Document must have at least one section",
        ));
    }

    let mut section_ids = HashSet::new();
    for (idx, section) in document.sections.iter().enumerate() {
        validate_section(section, idx, &mut section_ids, &mut result);
    }

    // Metadata validation
    if let Some(metadata) = &document.metadata {
        if document.document_type == DocumentType::Template && metadata.template_id.is_none() {
            result.add_error(ValidationError::warning(
                "metadata.templateId",
                "Template should have a templateId",
            ));
        }
    }

    result
}

fn validate_section(
    section: &Section,
    index: usize,
    section_ids: &mut HashSet<String>,
    result: &mut ValidationResult,
) {
    let prefix = format!("sections[{}]", index);

    // ID validation
    if section.id.is_empty() {
        result.add_error(ValidationError::error(
            format!("{}.id", prefix),
            "Section ID is required",
        ));
    } else if !section_ids.insert(section.id.clone()) {
        result.add_error(ValidationError::error(
            format!("{}.id", prefix),
            format!("Duplicate section ID: {}", section.id),
        ));
    }

    // Title validation
    if section.title.is_empty() {
        result.add_error(ValidationError::error(
            format!("{}.title", prefix),
            "Section title is required",
        ));
    }

    // Validate prompts
    let mut prompt_ids = HashSet::new();
    for (idx, prompt) in section.prompts.iter().enumerate() {
        validate_prompt(prompt, &format!("{}.prompts[{}]", prefix, idx), &mut prompt_ids, result);
    }

    // Validate subsections
    for subsection in &section.subsections {
        for prompt in &subsection.prompts {
            validate_prompt(prompt, &format!("{}.subsections", prefix), &mut prompt_ids, result);
        }
    }
}

fn validate_prompt(
    prompt: &Prompt,
    prefix: &str,
    prompt_ids: &mut HashSet<String>,
    result: &mut ValidationResult,
) {
    // ID validation
    if prompt.id.is_empty() {
        result.add_error(ValidationError::error(
            format!("{}.id", prefix),
            "Prompt ID is required",
        ));
    } else if !prompt_ids.insert(prompt.id.clone()) {
        result.add_error(ValidationError::error(
            format!("{}.id", prefix),
            format!("Duplicate prompt ID: {}", prompt.id),
        ));
    }

    // Label validation
    if prompt.label.is_empty() {
        result.add_error(ValidationError::error(
            format!("{}.label", prefix),
            "Prompt label is required",
        ));
    }
}

/// Validate document for publishing
pub fn validate_for_publishing(document: &AprDocument) -> ValidationResult {
    let mut result = validate(document);

    // Must be a template
    if document.document_type != DocumentType::Template {
        result.add_error(ValidationError::error(
            "documentType",
            "Document must be a template",
        ));
    }

    // Must have metadata
    if document.metadata.is_none() {
        result.add_error(ValidationError::error("metadata", "Metadata is required"));
        return result;
    }

    let metadata = document.metadata.as_ref().unwrap();

    // Must have template ID
    if metadata.template_id.is_none() {
        result.add_error(ValidationError::error(
            "metadata.templateId",
            "Template ID is required",
        ));
    }

    // Must be signed
    if metadata.template_signatures.is_empty() {
        result.add_error(ValidationError::error(
            "metadata.templateSignatures",
            "Template must be digitally signed",
        ));
    }

    result
}
