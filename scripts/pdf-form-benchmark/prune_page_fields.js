// Keep only the form fields that have a widget on this one-page PDF's page.
//
//   mutool run prune_page_fields.js in.pdf out.pdf
//
// Splitting a form into pages copies the document's whole field list into every
// page file, so a page appears to carry fields printed on other pages. A widget
// is on the page when the page's /Annots lists it; a field is kept when any of
// its widgets is, and loses the kids that are not.
var src = scriptArgs[0], dst = scriptArgs[1];
var doc = new PDFDocument(src);
if (doc.countPages() !== 1) throw new Error(src + " has " + doc.countPages() + " pages, expected 1");
var annots = doc.findPage(0).get("Annots");
var onPage = {};
if (annots && annots.isArray())
    for (var i = 0; i < annots.length; i++)
        if (annots.get(i).isIndirect()) onPage[annots.get(i).asIndirect()] = true;

function keep(node) {
    var kids = node.get("Kids");
    if (kids && kids.isArray() && kids.length > 0) {
        var kept = doc.newArray();
        for (var i = 0; i < kids.length; i++) if (keep(kids.get(i))) kept.push(kids.get(i));
        if (kept.length === 0) return false;
        node.put("Kids", kept);
        return true;
    }
    return node.isIndirect() && onPage[node.asIndirect()] === true;
}

var form = doc.getTrailer().get("Root").get("AcroForm");
var before = 0, after = 0;
if (form && form.isDictionary() && form.get("Fields").isArray()) {
    var fields = form.get("Fields"), kept = doc.newArray();
    for (var j = 0; j < fields.length; j++) {
        before++;
        if (keep(fields.get(j))) { kept.push(fields.get(j)); after++; }
    }
    form.put("Fields", kept);
}
doc.save(dst, "garbage=compact");
print(before + " -> " + after + " top-level fields");
