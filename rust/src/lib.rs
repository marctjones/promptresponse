//! # PromptResponse
//!
//! Rust library for working with APR (Adaptive Prompt Response) forms.
//!
//! ## Features
//!
//! - Create and fill APR templates
//! - Validate document structure
//! - Digital signatures (optional, with `signatures` feature)
//! - JSON serialization with serde
//!
//! ## Example
//!
//! ```rust
//! use promptresponse::TemplateBuilder;
//!
//! let document = TemplateBuilder::new("Contact Form", "contact-v1")
//!     .description("Simple contact form")
//!     .author("Your Org")
//!     .section("Personal Info")
//!         .prompt("Name", "text")
//!         .prompt("Email", "email")
//!         .done()
//!     .build();
//! ```

pub mod models;
pub mod serialization;
pub mod validation;
pub mod api;

#[cfg(feature = "signatures")]
pub mod signatures;

// Re-export main types
pub use models::*;
pub use serialization::*;
pub use validation::*;
pub use api::*;

#[cfg(feature = "signatures")]
pub use signatures::*;
