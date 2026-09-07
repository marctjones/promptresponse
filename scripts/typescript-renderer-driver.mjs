// The rendering half of the TypeScript renderer's conformance driver.
//
// It renders each case with the shipped `renderHtml` and writes the markup out.
// It draws no conclusions: deriving an interaction snapshot from that markup is
// scripts/typescript-renderer-driver.py, and the split is the point. A driver that
// walked the document to describe the interface would be reporting the document
// back, and would pass every rule about rendering without rendering anything.
//
// Network access is watched rather than asserted. Everything is patched before the
// renderer is imported, so a fetch made while rendering is recorded and reported,
// and `APR-SEC-010` is decided by what happened.
import http from "node:http";
import https from "node:https";
import net from "node:net";

const requests = [];
const watch = (object, name, describe) => {
  const original = object[name].bind(object);
  object[name] = (...args) => { requests.push(describe(args)); return original(...args); };
};

globalThis.fetch = (...args) => {
  requests.push(String(args[0]?.url ?? args[0]));
  throw new Error("rendering must not fetch");
};
for (const module of [http, https]) {
  for (const name of ["request", "get"]) {
    watch(module, name, args => typeof args[0] === "string" ? args[0]
      : `${args[0]?.protocol ?? ""}//${args[0]?.host ?? args[0]?.hostname ?? "?"}${args[0]?.path ?? ""}`);
  }
}
watch(net, "connect", args => `tcp://${args[0]?.host ?? args[0]}:${args[0]?.port ?? ""}`);

const { readBeta6Form, renderHtml, validate, writeBeta6Form } =
  await import("../typescript/dist/index.js");

const suite = JSON.parse(await new Promise((resolve, reject) => {
  let text = "";
  process.stdin.setEncoding("utf8");
  process.stdin.on("data", chunk => { text += chunk; });
  process.stdin.on("end", () => resolve(text));
  process.stdin.on("error", reject);
}));

// What this library does when asked to save. Advisories are warnings and do not
// stop a write; only an error does, and whether the mismatch in the suite is one of
// those is the question `APR-RENDER-006` asks.
function save(document) {
  const report = validate(document);
  if (report.errors.length)
    return { written: false, blockedBy: `${report.errors[0].code} at ${report.errors[0].path}` };
  try {
    writeBeta6Form(document, "jsonc");
    return { written: true, blockedBy: null };
  } catch (error) {
    return { written: false, blockedBy: String(error?.message ?? error) };
  }
}

const rendered = [];
for (const testCase of suite.cases) {
  if (testCase.surface !== "renderer") continue;
  const before = requests.length;
  let html = "", failure = null, saveResult = null;
  try {
    const document = readBeta6Form(testCase.document, "jsonc");
    html = renderHtml(document);
    saveResult = save(document);
  } catch (error) {
    // Reported rather than thrown: a renderer that refuses one document has failed
    // that case, and the remaining cases are still worth scoring.
    failure = String(error?.message ?? error);
  }
  rendered.push({ id: testCase.id, html, failure, saveResult,
                  requests: requests.slice(before) });
}

process.stdout.write(JSON.stringify({
  name: "@promptresponse/core renderHtml",
  version: suite.formatVersion ?? "",
  cases: rendered,
}));
