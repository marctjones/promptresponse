"""
Digital signature creation and verification for APR templates.
"""
import hashlib
import base64
from datetime import datetime
from typing import Optional, Tuple
from pathlib import Path

try:
    from cryptography import x509
    from cryptography.hazmat.primitives import hashes, serialization
    from cryptography.hazmat.primitives.asymmetric import rsa, padding
    from cryptography.x509.oid import NameOID
    CRYPTOGRAPHY_AVAILABLE = True
except ImportError:
    CRYPTOGRAPHY_AVAILABLE = False

from ..models import AprDocument, DigitalSignature, DocumentType, Metadata
from ..serialization import AprJsonSerializer


class SignatureError(Exception):
    """Exception raised for signature-related errors."""
    pass


class TemplateSigner:
    """Service for signing APR templates."""

    def __init__(self):
        """Initialize the signer."""
        if not CRYPTOGRAPHY_AVAILABLE:
            raise ImportError(
                "cryptography library is required for signature support. "
                "Install with: pip install cryptography"
            )

    @staticmethod
    def _compute_template_hash(document: AprDocument) -> str:
        """
        Compute SHA-256 hash of template content (excluding signatures).

        Args:
            document: The APR template document

        Returns:
            Hex-encoded SHA-256 hash
        """
        # Clone document and clear signatures for hashing
        doc_dict = document.to_dict()
        if 'metadata' in doc_dict and 'templateSignatures' in doc_dict['metadata']:
            doc_dict['metadata']['templateSignatures'] = []

        # Serialize to canonical JSON
        canonical_json = AprJsonSerializer.serialize(AprDocument.from_dict(doc_dict), indent=None)

        # Compute hash
        return hashlib.sha256(canonical_json.encode('utf-8')).hexdigest()

    @staticmethod
    def generate_certificate(
        name: str,
        email: str,
        organization: Optional[str] = None,
        validity_days: int = 365
    ) -> Tuple[bytes, bytes]:
        """
        Generate a self-signed certificate and private key.

        Args:
            name: Signer's full name
            email: Signer's email address
            organization: Organization name (optional)
            validity_days: Number of days the certificate is valid (default: 365)

        Returns:
            Tuple of (private_key_pem, certificate_pem) as bytes
        """
        if not CRYPTOGRAPHY_AVAILABLE:
            raise ImportError("cryptography library is required")

        # Generate private key
        private_key = rsa.generate_private_key(
            public_exponent=65537,
            key_size=2048
        )

        # Build subject
        subject_attrs = [
            x509.NameAttribute(NameOID.COMMON_NAME, name),
            x509.NameAttribute(NameOID.EMAIL_ADDRESS, email),
        ]
        if organization:
            subject_attrs.append(x509.NameAttribute(NameOID.ORGANIZATION_NAME, organization))

        subject = x509.Name(subject_attrs)

        # Create self-signed certificate
        cert = (
            x509.CertificateBuilder()
            .subject_name(subject)
            .issuer_name(subject)
            .public_key(private_key.public_key())
            .serial_number(x509.random_serial_number())
            .not_valid_before(datetime.utcnow())
            .not_valid_after(datetime.utcnow().replace(year=datetime.utcnow().year + validity_days // 365))
            .sign(private_key, hashes.SHA256())
        )

        # Serialize to PEM
        private_key_pem = private_key.private_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PrivateFormat.PKCS8,
            encryption_algorithm=serialization.NoEncryption()
        )

        cert_pem = cert.public_bytes(serialization.Encoding.PEM)

        return private_key_pem, cert_pem

    def sign_template(
        self,
        document: AprDocument,
        private_key_pem: bytes,
        certificate_pem: bytes,
        signer_name: str,
        signer_email: str
    ) -> AprDocument:
        """
        Sign an APR template with a digital signature.

        Args:
            document: The template document to sign
            private_key_pem: Private key in PEM format
            certificate_pem: Certificate in PEM format
            signer_name: Name of the signer
            signer_email: Email of the signer

        Returns:
            New document with signature added

        Raises:
            SignatureError: If signing fails
        """
        if document.document_type != DocumentType.TEMPLATE:
            raise SignatureError("Only templates can be signed")

        # Compute template hash
        template_hash = self._compute_template_hash(document)

        # Load private key
        try:
            private_key = serialization.load_pem_private_key(
                private_key_pem,
                password=None
            )
        except Exception as e:
            raise SignatureError(f"Failed to load private key: {e}") from e

        # Sign the hash
        try:
            signature_bytes = private_key.sign(
                template_hash.encode('utf-8'),
                padding.PSS(
                    mgf=padding.MGF1(hashes.SHA256()),
                    salt_length=padding.PSS.MAX_LENGTH
                ),
                hashes.SHA256()
            )
            signature_value = base64.b64encode(signature_bytes).decode('utf-8')
        except Exception as e:
            raise SignatureError(f"Failed to sign template: {e}") from e

        # Create signature object
        signature = DigitalSignature(
            signer_name=signer_name,
            signer_email=signer_email,
            signature_algorithm="RSA-PSS-SHA256",
            signature_value=signature_value,
            certificate=certificate_pem.decode('utf-8'),
            signed_date=datetime.utcnow(),
            template_hash=template_hash
        )

        # Add signature to document
        if document.metadata is None:
            document.metadata = Metadata()

        # Create new signatures list with existing + new
        new_signatures = list(document.metadata.template_signatures)
        new_signatures.append(signature)
        document.metadata.template_signatures = new_signatures

        return document

    def sign_template_file(
        self,
        template_path: Path,
        output_path: Path,
        private_key_pem: bytes,
        certificate_pem: bytes,
        signer_name: str,
        signer_email: str
    ) -> None:
        """
        Sign a template file and save the signed version.

        Args:
            template_path: Path to input template file
            output_path: Path to save signed template
            private_key_pem: Private key in PEM format
            certificate_pem: Certificate in PEM format
            signer_name: Name of the signer
            signer_email: Email of the signer
        """
        # Load template
        document = AprJsonSerializer.load_file(template_path)

        # Sign it
        signed_document = self.sign_template(
            document,
            private_key_pem,
            certificate_pem,
            signer_name,
            signer_email
        )

        # Save signed template
        AprJsonSerializer.save_file(signed_document, output_path)


class SignatureVerifier:
    """Service for verifying APR template signatures."""

    def __init__(self):
        """Initialize the verifier."""
        if not CRYPTOGRAPHY_AVAILABLE:
            raise ImportError(
                "cryptography library is required for signature support. "
                "Install with: pip install cryptography"
            )

    @staticmethod
    def verify_signature(document: AprDocument, signature: DigitalSignature) -> Tuple[bool, Optional[str]]:
        """
        Verify a single signature on a template.

        Args:
            document: The template document
            signature: The signature to verify

        Returns:
            Tuple of (is_valid, error_message). error_message is None if valid.
        """
        if document.document_type != DocumentType.TEMPLATE:
            return False, "Document is not a template"

        # Compute current template hash
        current_hash = TemplateSigner._compute_template_hash(document)

        # Check if hash matches
        if current_hash != signature.template_hash:
            return False, "Template has been modified since signing"

        # Load certificate
        try:
            cert = x509.load_pem_x509_certificate(signature.certificate.encode('utf-8'))
        except Exception as e:
            return False, f"Invalid certificate: {e}"

        # Decode signature
        try:
            signature_bytes = base64.b64decode(signature.signature_value)
        except Exception as e:
            return False, f"Invalid signature encoding: {e}"

        # Verify signature
        try:
            public_key = cert.public_key()
            public_key.verify(
                signature_bytes,
                signature.template_hash.encode('utf-8'),
                padding.PSS(
                    mgf=padding.MGF1(hashes.SHA256()),
                    salt_length=padding.PSS.MAX_LENGTH
                ),
                hashes.SHA256()
            )
            return True, None
        except Exception as e:
            return False, f"Signature verification failed: {e}"

    def verify_template(self, document: AprDocument) -> Tuple[bool, str]:
        """
        Verify all signatures on a template.

        Args:
            document: The template document to verify

        Returns:
            Tuple of (is_valid, message)
        """
        if not document.metadata or not document.metadata.template_signatures:
            return False, "Template has no signatures"

        results = []
        for idx, signature in enumerate(document.metadata.template_signatures):
            is_valid, error = self.verify_signature(document, signature)
            if is_valid:
                results.append(f"✓ Signature {idx + 1} by {signature.signer_name} is valid")
            else:
                results.append(f"✗ Signature {idx + 1} by {signature.signer_name} is invalid: {error}")

        all_valid = all(
            self.verify_signature(document, sig)[0]
            for sig in document.metadata.template_signatures
        )

        message = "\n".join(results)
        return all_valid, message

    def verify_template_file(self, template_path: Path) -> Tuple[bool, str]:
        """
        Verify signatures in a template file.

        Args:
            template_path: Path to template file

        Returns:
            Tuple of (is_valid, message)
        """
        document = AprJsonSerializer.load_file(template_path)
        return self.verify_template(document)
