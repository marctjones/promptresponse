package org.promptresponse;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.*;
import java.util.Base64;
import org.bouncycastle.cms.CMSProcessableByteArray;
import org.bouncycastle.cms.CMSSignedData;
import org.bouncycastle.cms.SignerInformation;
import org.bouncycastle.cms.jcajce.JcaSimpleSignerInfoVerifierBuilder;
import org.bouncycastle.cert.X509CertificateHolder;
import org.bouncycastle.util.Store;

/** Semantic digest and non-gating attestation resolver for the beta.6 profile. */
public final class AprBeta6Integrity {
    public static final String CANONICALIZATION = "jcs-sha256";
    public record ManifestEntry(String path, String digest) { }
    public record Manifest(String root, List<ManifestEntry> entries) { }
    public record Resolution(AprBeta6.AttestationRecord attestation, String state, List<String> differingPaths, int witnessesResolved) { }
    private AprBeta6Integrity() { }

    public static String canonicalize(Object value) { return canonical(value); }
    public static String digest(Object value) {
        try { return "sha256:" + HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(canonical(value).getBytes(StandardCharsets.UTF_8))); }
        catch (NoSuchAlgorithmException ex) { throw new IllegalStateException(ex); }
    }
    public static Manifest createManifest(Object value) {
        List<ManifestEntry> entries = new ArrayList<>(); visit(value, "", entries); return new Manifest(digest(value), List.copyOf(entries));
    }
    public static String attestationEnvelopeDigest(Map<String,Object> value) {
        Map<String,Object> envelope = new LinkedHashMap<>(value); envelope.remove("proofs"); return digest(envelope);
    }
    public static List<Resolution> resolve(List<AprBeta6.Record> records) {
        Map<String,Object> forms = new HashMap<>(); Set<String> envelopes = new HashSet<>();
        for (var record : records) if (record instanceof AprBeta6.FormRecord form) { Object value=form.value(); forms.put(digest(value), value); }
        for (var record : records) if (record instanceof AprBeta6.AttestationRecord attestation) envelopes.add(attestationEnvelopeDigest(attestation.value()));
        List<Resolution> result = new ArrayList<>();
        for (var record : records) if (record instanceof AprBeta6.AttestationRecord attestation) {
            @SuppressWarnings("unchecked") Map<String,Object> subject = attestation.value().get("subject") instanceof Map<?,?> raw ? (Map<String,Object>)raw : null;
            if (subject == null || !(subject.get("digest") instanceof String target)) throw new AprException("beta.6 attestation subject.digest is required");
            int witnesses = attestation.value().get("witnesses") instanceof List<?> listed ? (int)listed.stream().filter(item -> item instanceof String text && envelopes.contains(text)).count() : 0;
            Object form = forms.get(target); if (form == null) { result.add(new Resolution(attestation,"unresolved",List.of(),witnesses)); continue; }
            Manifest actual = createManifest(form); @SuppressWarnings("unchecked") Map<String,Object> asserted = attestation.value().get("manifest") instanceof Map<?,?> raw ? (Map<String,Object>)raw : Map.of();
            List<String> differing = new ArrayList<>(); if (!Objects.equals(asserted.get("root"), actual.root())) differing.add("");
            Map<String,String> actualByPath = new HashMap<>(); for (var entry:actual.entries()) actualByPath.put(entry.path(),entry.digest());
            if (asserted.get("entries") instanceof List<?> entries) for(Object item:entries) { if (!(item instanceof Map<?,?> entry) || !(entry.get("path") instanceof String path) || !Objects.equals(actualByPath.get(path),entry.get("digest"))) differing.add(item instanceof Map<?,?> entry && entry.get("path") instanceof String path ? path : "?"); }
            Set<String> assertedPaths = new HashSet<>(); if (asserted.get("entries") instanceof List<?> entries) for (Object item : entries) if (item instanceof Map<?,?> entry && entry.get("path") instanceof String path) assertedPaths.add(path);
            validateFieldsScope(form, attestation.value(), assertedPaths, differing);
            String state = "invalid";
            if (differing.isEmpty()) state = verifyProofs(attestation.value()) ? "valid" : hasRecognizedCmsProof(attestation.value()) ? "invalid" : "unverifiable";
            result.add(new Resolution(attestation,state,List.copyOf(new LinkedHashSet<>(differing)),witnesses));
        }
        return List.copyOf(result);
    }
    @SuppressWarnings("unchecked") private static void visit(Object value,String path,List<ManifestEntry> entries) {
        entries.add(new ManifestEntry(path,digest(value)));
        if (value instanceof Map<?,?> map) for(String key:new TreeSet<>(map.keySet().stream().map(String::valueOf).toList())) visit(((Map<String,Object>)map).get(key),path+"/"+escape(key),entries);
        else if (value instanceof List<?> list) for(int i=0;i<list.size();i++) visit(list.get(i),path+"/"+i,entries);
    }
    @SuppressWarnings("unchecked") private static String canonical(Object value) {
        if (value==null || value instanceof String || value instanceof Boolean || value instanceof Number) { if(value instanceof Double d && !Double.isFinite(d)) throw new AprException("APR semantic digests require finite JSON numbers"); return Json.write(value); }
        if (value instanceof List<?> list) return "["+list.stream().map(AprBeta6Integrity::canonical).reduce((a,b)->a+","+b).orElse("")+"]";
        if (value instanceof Map<?,?> raw) { Map<String,Object> map=(Map<String,Object>)raw; return "{"+map.keySet().stream().sorted().map(key->Json.write(key)+":"+canonical(map.get(key))).reduce((a,b)->a+","+b).orElse("")+"}"; }
        throw new AprException("APR semantic digests require JSON values");
    }
    private static String escape(String value) { return value.replace("~","~0").replace("/","~1"); }

    @SuppressWarnings("unchecked") private static boolean hasRecognizedCmsProof(Map<String,Object> attestation) {
        return attestation.get("proofs") instanceof List<?> proofs && proofs.stream().anyMatch(item -> item instanceof Map<?,?> proof && "cms/ecdsa-p256-sha256".equals(proof.get("type")));
    }

    @SuppressWarnings("unchecked") private static boolean verifyProofs(Map<String,Object> attestation) {
        if (!(attestation.get("proofs") instanceof List<?> proofs)) return false;
        Map<String,Object> envelope = new LinkedHashMap<>(attestation); envelope.remove("proofs");
        byte[] payload = canonical(envelope).getBytes(StandardCharsets.UTF_8);
        for (Object item : proofs) try {
            if (!(item instanceof Map<?,?> proof) || !"cms/ecdsa-p256-sha256".equals(proof.get("type")) || !(proof.get("value") instanceof String encoded)) continue;
            CMSSignedData cms = new CMSSignedData(new CMSProcessableByteArray(payload), Base64.getDecoder().decode(encoded));
            Store<X509CertificateHolder> certificates = cms.getCertificates();
            for (SignerInformation signer : cms.getSignerInfos().getSigners()) {
                var matches = certificates.getMatches(signer.getSID());
                if (!matches.isEmpty() && signer.verify(new JcaSimpleSignerInfoVerifierBuilder().build((X509CertificateHolder) matches.iterator().next()))) return true;
            }
        } catch (Exception ignored) { }
        return false;
    }

    @SuppressWarnings("unchecked") private static void validateFieldsScope(Object form, Map<String,Object> attestation, Set<String> paths, List<String> differing) {
        if (!(attestation.get("scope") instanceof Map<?,?> rawScope) || !"fields".equals(rawScope.get("kind")) || !(rawScope.get("fields") instanceof List<?> fields) || fields.isEmpty()) { if (attestation.get("scope") instanceof Map<?,?> scope && "fields".equals(scope.get("kind"))) differing.add("/scope/fields"); return; }
        Map<String,Object> root=(Map<String,Object>)form;
        for(Object item:fields) { if (!(item instanceof String id)) { differing.add("/scope/fields"); continue; } List<String> sections=new ArrayList<>(); String prompt=findPrompt((List<Object>)root.get("sections"),id,"/sections",sections); if(prompt==null) { differing.add("/scope/fields"); continue; } require(paths,prompt,differing); require(paths,prompt+"/response",differing); if(pointer(root,prompt+"/hints")!=null) require(paths,prompt+"/hints",differing); for(String section:sections) for(String member:List.of("id","title","description","kind","role")) if(pointer(root,section+"/"+member)!=null) require(paths,section+"/"+member,differing); }
    }
    @SuppressWarnings("unchecked") private static String findPrompt(List<Object> sections,String id,String base,List<String> ancestors) { if(sections==null) return null; for(int i=0;i<sections.size();i++) { if(!(sections.get(i) instanceof Map<?,?> raw)) continue; Map<String,Object> section=(Map<String,Object>)raw; String path=base+"/"+i; ancestors.add(path); List<Object> prompts=(List<Object>)section.getOrDefault("prompts",List.of()); for(int j=0;j<prompts.size();j++) if(prompts.get(j) instanceof Map<?,?> prompt && id.equals(prompt.get("id"))) return path+"/prompts/"+j; String found=findPrompt((List<Object>)section.get("sections"),id,path+"/sections",ancestors); if(found!=null)return found; ancestors.removeLast(); } return null; }
    private static void require(Set<String> paths,String path,List<String> differing) { if(!paths.contains(path)) differing.add(path); }
    @SuppressWarnings("unchecked") private static Object pointer(Object value,String pointer) { Object current=value; for(String token:pointer.split("/")) { if(token.isEmpty())continue; token=token.replace("~1","/").replace("~0","~"); if(current instanceof Map<?,?> map) current=((Map<String,Object>)map).get(token); else if(current instanceof List<?> list && token.matches("\\d+") && Integer.parseInt(token)<list.size()) current=list.get(Integer.parseInt(token)); else return null; } return current; }
}
