#!/usr/bin/env node
/**
 * Copy the AMD build of monaco-editor into public/vendor/monaco/vs so that
 * @monaco-editor/react loads Monaco from our own origin instead of cdn.jsdelivr.net.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const feRoot = path.resolve(__dirname, '..');
const sourceDir = path.join(feRoot, 'node_modules', 'monaco-editor', 'min', 'vs');
const targetRoot = path.join(feRoot, 'public', 'vendor', 'monaco');
const targetDir = path.join(targetRoot, 'vs');

if (!fs.existsSync(sourceDir)) {
  console.error(`[copy-monaco] ERROR: ${sourceDir} not found. Run npm install first.`);
  process.exit(1);
}

fs.rmSync(targetRoot, { recursive: true, force: true });
fs.mkdirSync(targetRoot, { recursive: true });
fs.cpSync(sourceDir, targetDir, { recursive: true });

console.log(`[copy-monaco] Copied monaco-editor/min/vs -> ${path.relative(feRoot, targetDir)}`);
