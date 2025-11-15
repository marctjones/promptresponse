#!/usr/bin/env python3
"""
Example: Signing and verifying APR templates.

Requires: pip install cryptography
"""
from promptresponse import (
    AprJsonSerializer,
    TemplateSigner,
    SignatureVerifier,
    AprValidator
)

# Load a template
template = AprJsonSerializer.load_file("contact-form.aprt")
print(f"Template: {template.metadata.title}")

# Generate a certificate and private key
print("\nGenerating certificate...")
signer = TemplateSigner()
private_key, certificate = signer.generate_certificate(
    name="John Doe",
    email="john.doe@example.com",
    organization="Example Corp"
)
print("✓ Certificate generated")

# Sign the template
print("\nSigning template...")
signed_template = signer.sign_template(
    template,
    private_key,
    certificate,
    signer_name="John Doe",
    signer_email="john.doe@example.com"
)
print(f"✓ Template signed")
print(f"  Signatures: {len(signed_template.metadata.template_signatures)}")

# Save signed template
output_path = "contact-form-signed.aprt"
AprJsonSerializer.save_file(signed_template, output_path)
print(f"✓ Signed template saved to: {output_path}")

# Verify the signature
print("\nVerifying signature...")
verifier = SignatureVerifier()
is_valid, message = verifier.verify_template(signed_template)

print(message)

if is_valid:
    print("\n✓ All signatures are valid!")
else:
    print("\n✗ Signature verification failed!")

# Validate for publishing
print("\nValidating for publishing...")
pub_result = AprValidator.validate_for_publishing(signed_template)
print(pub_result)
