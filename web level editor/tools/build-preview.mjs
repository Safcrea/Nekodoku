// Bundles the multi-file site into one self-contained HTML file (no modules,
// no external requests) for quick previews/sharing. The real site stays the
// module-based source of truth; regenerate with:
//   node Website/tools/build-preview.mjs [output.html]

import { readFile, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

const css = await readFile(path.join(root, "css/styles.css"), "utf8");
const html = await readFile(path.join(root, "index.html"), "utf8");
const modelJs = await readFile(path.join(root, "js/core/model.js"), "utf8");
const validatorJs = await readFile(path.join(root, "js/core/validator.js"), "utf8");
const generatorJs = await readFile(path.join(root, "js/core/generator.js"), "utf8");
const appJs = await readFile(path.join(root, "js/app.js"), "utf8");

// Everything shares one scope in the bundle, so imports/exports just go away.
function stripModuleSyntax(source) {
    return source
        .replace(/^import[\s\S]*?from\s+"[^"]+";\s*$/gm, "")
        .replace(/^export\s+/gm, "");
}

const body = html
    .replace(/^[\s\S]*<body>/, "")
    .replace(/<script type="module"[\s\S]*$/, "")
    .replace(/<\/body>[\s\S]*$/, "");

const themeBoot = /<script>[\s\S]*?<\/script>/.exec(html)?.[0] ?? "";

const bundle = `<title>Meowdoku Level Editor</title>
${themeBoot}
<style>
${css}
</style>
${body}
<script>
"use strict";
${stripModuleSyntax(modelJs)}
${stripModuleSyntax(validatorJs)}
${stripModuleSyntax(generatorJs)}
${stripModuleSyntax(appJs)}
</script>
`;

const outPath = process.argv[2] ?? path.join(root, "preview.html");
await writeFile(outPath, bundle);
console.log(`Wrote ${outPath} (${bundle.length} bytes).`);
