// The PDF exporter's renderer-conformance driver.
//
// APR-RENDER-009 says an exported artifact must not be written back into the document.
// That is a rule about the thing that exports, and Avalonia does not export — so it is
// answered here, over src/PromptResponse.Rendering.Pdf, and nowhere else.
//
// This driver declares only the `exporter` surface. Every other case in the suite is
// reported unanswered rather than failed, because a surface may decline what it is not.
//
//     python3 scripts/run-renderer-conformance.py \
//         --driver "dotnet run --project tools/PromptResponse.RendererDrivers"

using System.Text.Json;
using System.Text.Json.Nodes;
using PromptResponse.Core.Beta6;
using PromptResponse.Core.Rendering;
using PromptResponse.Rendering.Pdf;

var reader = new AprBeta6Reader();
var suite = JsonNode.Parse(await Console.In.ReadToEndAsync())!.AsObject();
var results = new JsonArray();

foreach (var node in suite["cases"]!.AsArray())
{
    var testCase = node!.AsObject();
    if (testCase["surface"]?.GetValue<string>() != "exporter") continue;

    var source = testCase["document"]!.GetValue<string>();
    var document = reader.ReadForm(source, AprRepresentation.Jsonc);
    var before = reader.WriteForm(document, AprRepresentation.Jsonc);

    // Twice, so a write-back has two chances to appear in the model. An exporter
    // that mutates only on a second pass — caching, or a lazily built render model
    // written back once it exists — would survive a single export.
    Export(document);
    Export(document);

    // Serialized back from the model the exporter was handed. If exporting wrote
    // anything into the document, this is where it appears.
    var after = reader.WriteForm(document, AprRepresentation.Jsonc);
    if (before != after)
        Console.Error.WriteLine(
            $"{testCase["id"]}: exporting changed the document, so the difference the "
            + "scorer reports is a write-back and not a round-trip artefact");

    results.Add(new JsonObject
    {
        ["id"] = testCase["id"]!.GetValue<string>(),
        ["exportedDocument"] = after,
        // No nodes and no saveResult: this driver exports, and reports nothing about
        // an interface it never presented or a save it never made. A hardcoded
        // `written: true` here would be the echo driver check-renderer-teeth.py exists
        // to catch.
        ["nodes"] = new JsonArray(),
        ["requests"] = new JsonArray(),
    });
}

var report = new JsonObject
{
    ["implementation"] = new JsonObject
    {
        ["name"] = "PromptResponse.Rendering.Pdf",
        ["version"] = PromptResponse.Core.AprFormat.CurrentVersion,
        ["surfaces"] = new JsonArray("exporter"),
    },
    ["results"] = results,
};
Console.Out.Write(report.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
return 0;

static void Export(PromptResponse.Core.Models.AprDocument document)
{
    using var output = new MemoryStream();
    new PdfDocumentRenderer().Render(document, RenderOptions.Default, output);
}
