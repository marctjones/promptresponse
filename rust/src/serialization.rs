//! JSON serialization for APR documents

use crate::models::AprDocument;
use std::fs;
use std::path::Path;
use thiserror::Error;

#[derive(Error, Debug)]
pub enum SerializationError {
    #[error("JSON error: {0}")]
    Json(#[from] serde_json::Error),
    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),
}

pub type Result<T> = std::result::Result<T, SerializationError>;

/// Serialize APR document to JSON string
pub fn serialize(document: &AprDocument) -> Result<String> {
    Ok(serde_json::to_string_pretty(document)?)
}

/// Deserialize APR document from JSON string
pub fn deserialize(json: &str) -> Result<AprDocument> {
    Ok(serde_json::from_str(json)?)
}

/// Load APR document from file
pub fn load_file<P: AsRef<Path>>(path: P) -> Result<AprDocument> {
    let json = fs::read_to_string(path)?;
    deserialize(&json)
}

/// Save APR document to file
pub fn save_file<P: AsRef<Path>>(document: &AprDocument, path: P) -> Result<()> {
    let json = serialize(document)?;
    fs::write(path, json)?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::models::*;

    #[test]
    fn test_serialize_deserialize() {
        let doc = AprDocument::new();
        let json = serialize(&doc).unwrap();
        let deserialized = deserialize(&json).unwrap();
        assert_eq!(doc.version, deserialized.version);
    }
}
