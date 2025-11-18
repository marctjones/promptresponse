//! Core data models for APR documents

use serde::{Deserialize, Serialize};
use chrono::{DateTime, Utc};
use std::collections::HashMap;

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
#[serde(rename_all = "camelCase")]
pub enum DocumentType {
    Template,
    FilledForm,
}

#[derive(Debug, Clone, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct PromptHints {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub expected_data_type: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub placeholder: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub help_text: Option<String>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub suggested_values: Vec<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub min_length: Option<i32>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub max_length: Option<i32>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub pattern: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct ResponseMetadata {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub last_modified: Option<DateTime<Utc>>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Prompt {
    pub id: String,
    pub label: String,
    #[serde(default)]
    pub response: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub hints: Option<PromptHints>,
    #[serde(default)]
    pub response_metadata: ResponseMetadata,
}

impl Prompt {
    pub fn new(id: impl Into<String>, label: impl Into<String>) -> Self {
        Self {
            id: id.into(),
            label: label.into(),
            response: String::new(),
            hints: None,
            response_metadata: ResponseMetadata::default(),
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Subsection {
    pub id: String,
    pub title: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub description: Option<String>,
    #[serde(default)]
    pub prompts: Vec<Prompt>,
}

impl Subsection {
    pub fn new(id: impl Into<String>, title: impl Into<String>) -> Self {
        Self {
            id: id.into(),
            title: title.into(),
            description: None,
            prompts: Vec::new(),
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Section {
    pub id: String,
    pub title: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub description: Option<String>,
    #[serde(default)]
    pub prompts: Vec<Prompt>,
    #[serde(default)]
    pub subsections: Vec<Subsection>,
}

impl Section {
    pub fn new(id: impl Into<String>, title: impl Into<String>) -> Self {
        Self {
            id: id.into(),
            title: title.into(),
            description: None,
            prompts: Vec::new(),
            subsections: Vec::new(),
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DigitalSignature {
    pub signer_name: String,
    pub signer_email: String,
    pub signature_algorithm: String,
    pub signature_value: String,
    pub certificate: String,
    pub signed_date: DateTime<Utc>,
    pub template_hash: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SubmissionConfig {
    pub r#type: String,
    pub url: String,
    pub fields: HashMap<String, String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub expires_at: Option<DateTime<Utc>>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub headers: Option<HashMap<String, String>>,
}

impl SubmissionConfig {
    pub fn is_expired(&self) -> bool {
        self.expires_at.map_or(false, |exp| Utc::now() > exp)
    }
}

#[derive(Debug, Clone, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct Metadata {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub title: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub description: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub author: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub created: Option<DateTime<Utc>>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub modified: Option<DateTime<Utc>>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub template_id: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub template_version: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub filled_by: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub filled_date: Option<DateTime<Utc>>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub template_signatures: Vec<DigitalSignature>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub submission_config: Option<SubmissionConfig>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AprDocument {
    pub version: String,
    pub document_type: DocumentType,
    pub sections: Vec<Section>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub metadata: Option<Metadata>,
}

impl AprDocument {
    pub fn new() -> Self {
        Self {
            version: "1.0".to_string(),
            document_type: DocumentType::Template,
            sections: Vec::new(),
            metadata: None,
        }
    }

    /// Get all prompts (flattened)
    pub fn get_all_prompts(&self) -> Vec<&Prompt> {
        let mut prompts = Vec::new();
        for section in &self.sections {
            prompts.extend(&section.prompts);
            for subsection in &section.subsections {
                prompts.extend(&subsection.prompts);
            }
        }
        prompts
    }

    /// Get all prompts (mutable, flattened)
    pub fn get_all_prompts_mut(&mut self) -> Vec<&mut Prompt> {
        let mut prompts = Vec::new();
        for section in &mut self.sections {
            prompts.extend(section.prompts.iter_mut());
            for subsection in &mut section.subsections {
                prompts.extend(subsection.prompts.iter_mut());
            }
        }
        prompts
    }

    /// Find prompt by ID
    pub fn get_prompt_by_id(&self, prompt_id: &str) -> Option<&Prompt> {
        self.get_all_prompts()
            .into_iter()
            .find(|p| p.id == prompt_id)
    }

    /// Find prompt by ID (mutable)
    pub fn get_prompt_by_id_mut(&mut self, prompt_id: &str) -> Option<&mut Prompt> {
        self.get_all_prompts_mut()
            .into_iter()
            .find(|p| p.id == prompt_id)
    }

    /// Calculate completion percentage
    pub fn get_completion_percentage(&self) -> f64 {
        let all_prompts = self.get_all_prompts();
        if all_prompts.is_empty() {
            return 0.0;
        }

        let filled = all_prompts
            .iter()
            .filter(|p| !p.response.trim().is_empty())
            .count();

        (filled as f64 / all_prompts.len() as f64) * 100.0
    }
}

impl Default for AprDocument {
    fn default() -> Self {
        Self::new()
    }
}
