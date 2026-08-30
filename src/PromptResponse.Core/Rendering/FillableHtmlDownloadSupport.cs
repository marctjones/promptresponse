using System.Text;

namespace PromptResponse.Core.Rendering;

/// <summary>
/// Emits the self-contained browser support required to download a filled APR
/// document. Keeping this transport concern separate from semantic form markup
/// makes the renderer's rendering path easier to inspect and change.
/// </summary>
internal static class FillableHtmlDownloadSupport
{
    public static void Append(StringBuilder output, string embeddedJson)
    {
        output.Append("<div class=\"bar\"><button type=\"button\" id=\"apr-download\">Download filled form</button></div>\n");
        output.Append("<script type=\"application/json\" id=\"apr-document\">")
            .Append(EncodeForScript(embeddedJson))
            .Append("</script>\n");
        output.Append("<script>\n").Append(DownloadScript).Append("</script>\n");
    }

    /// <summary>
    /// Escapes JSON for an HTML script container while keeping it valid JSON.
    /// </summary>
    private static string EncodeForScript(string json) =>
        json.Replace("<", "\\u003c").Replace(">", "\\u003e").Replace("&", "\\u0026");

    private const string DownloadScript = """
(function () {
  var raw = document.getElementById('apr-document').textContent;
  function collect() {
    var map = {};
    document.querySelectorAll('[data-prompt-id]').forEach(function (el) {
      var id = el.getAttribute('data-prompt-id');
      map[id] = el.type === 'checkbox' ? (el.checked ? 'true' : 'false') : el.value;
    });
    return map;
  }
  function apply(section, map, stamp) {
    (section.prompts || []).forEach(function (p) {
      if (Object.prototype.hasOwnProperty.call(map, p.id)) {
        p.response = map[p.id];
        p.responseMetadata = p.responseMetadata || {};
        p.responseMetadata.lastModified = stamp;
      }
    });
    (section.sections || []).forEach(function (s) { apply(s, map, stamp); });
  }
  document.getElementById('apr-download').addEventListener('click', function () {
    var doc = JSON.parse(raw);
    var map = collect();
    var stamp = new Date().toISOString();
    (doc.sections || []).forEach(function (s) { apply(s, map, stamp); });
    doc.documentType = 'filledForm';
    var name = (doc.metadata && doc.metadata.title ? doc.metadata.title : 'form').replace(/[\\/:*?"<>|]+/g, '_');
    var blob = new Blob([JSON.stringify(doc, null, 2)], { type: 'application/json' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = name + '.aprf';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(function () { URL.revokeObjectURL(url); }, 0);
  });
})();
""";
}
