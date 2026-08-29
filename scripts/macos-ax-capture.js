ObjC.import('Foundation');

function safe(read, fallback) { try { return read(); } catch (_) { return fallback; } }
function snapshot(element, depth, budget) {
  if (budget.count++ >= 500 || depth > 12) return { name: '', role: 'truncated', children: [] };
  const children = safe(() => element.uiElements(), []);
  return {
    name: String(safe(() => element.name(), '')),
    role: String(safe(() => element.role(), '')),
    description: String(safe(() => element.description(), '')),
    focused: Boolean(safe(() => element.focused(), false)),
    children: children.map(child => snapshot(child, depth + 1, budget))
  };
}
function names(node, all) {
  if (node.name) all.push(node.name);
  node.children.forEach(child => names(child, all));
}
function run(argv) {
  const processName = argv[0];
  const outputPath = argv[1];
  const processes = Application('System Events').processes();
  const process = processes.find(p => String(safe(() => p.name(), '')) === processName);
  if (!process) throw new Error(`No running process named ${processName}`);
  const tree = snapshot(process, 0, { count: 0 });
  const visibleNames = [];
  names(tree, visibleNames);
  const required = ['File menu', 'Open file', 'Save'];
  const missing = required.filter(name => !visibleNames.includes(name));
  const evidence = { capturedAt: (new Date()).toISOString(), processName, required, missing, tree };
  const json = JSON.stringify(evidence, null, 2);
  $(json).writeToFileAtomicallyEncodingError($(outputPath), true, $.NSUTF8StringEncoding, null);
  if (missing.length) throw new Error(`Missing required AX names: ${missing.join(', ')}`);
  return 'Captured live AX tree';
}
