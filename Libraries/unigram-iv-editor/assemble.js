// assemble.js — inject the bundled editor (+ shared CSS) into an HTML shell,
// producing a single self-contained editor.html (no network dependencies).
//
// Two shells, selected at build time:
//   default        host/editor-shell.html — JUST the editor (production WebView).
//   --shell        host/shell.html        — editor + native-toolbar/inspector
//                                            simulation (app.js) for testing.
//
// Run via `npm run build` (editor only) or `npm run dev` (with the dev shell).
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";

const withShell = process.argv.includes("--shell");
const shellFile = withShell ? "host/shell.html" : "host/editor-shell.html";

const shell = readFileSync(shellFile, "utf8");
const bundle = readFileSync("dist/bundle.min.js", "utf8");
const css = readFileSync("host/editor.css", "utf8");

let html = shell;
html = html.replace("/*__EDITOR_CSS__*/", () => css);
html = html.replace("<script>/*__BUNDLE__*/</script>", () => "<script>" + bundle + "</script>");
if (withShell) {
  const app = readFileSync("host/app.js", "utf8");
  html = html.replace("/*__APP__*/", () => app);
}

mkdirSync("dist", { recursive: true });
writeFileSync("editor.html", html);
writeFileSync("dist/editor.html", html);
console.log(`editor.html written (${html.length} bytes, ${withShell ? "dev shell" : "editor only"})`);
