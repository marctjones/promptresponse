package io.promptresponse.models;

import java.time.Instant;

public class DigitalSignature {
    private String signerName;
    private String signerEmail;
    private String signatureAlgorithm;
    private String signatureValue;
    private String certificate;
    private Instant signedDate;
    private String templateHash;

    public String getSignerName() { return signerName; }
    public void setSignerName(String signerName) { this.signerName = signerName; }

    public String getSignerEmail() { return signerEmail; }
    public void setSignerEmail(String signerEmail) { this.signerEmail = signerEmail; }

    public String getSignatureAlgorithm() { return signatureAlgorithm; }
    public void setSignatureAlgorithm(String signatureAlgorithm) { this.signatureAlgorithm = signatureAlgorithm; }

    public String getSignatureValue() { return signatureValue; }
    public void setSignatureValue(String signatureValue) { this.signatureValue = signatureValue; }

    public String getCertificate() { return certificate; }
    public void setCertificate(String certificate) { this.certificate = certificate; }

    public Instant getSignedDate() { return signedDate; }
    public void setSignedDate(Instant signedDate) { this.signedDate = signedDate; }

    public String getTemplateHash() { return templateHash; }
    public void setTemplateHash(String templateHash) { this.templateHash = templateHash; }
}
