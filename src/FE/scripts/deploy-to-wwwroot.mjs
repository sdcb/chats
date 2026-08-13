#!/usr/bin/env node
/**
 * Deploy `next build` output (out/) to BE web wwwroot.
 *
 * - Clears wwwroot completely (removes stale PWA workbox/sw.js etc.)
 * - Re-creates .gitkeep so the directory stays tracked by git
 * - Copies out/ contents into wwwroot
 *
 * Run via: npm run www  (which builds first with API_URL=)
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const feRoot = path.resolve(__dirname, '..');
const outDir = path.join(feRoot, 'out');
const wwwrootDir = path.resolve(feRoot, '..', 'BE', 'web', 'wwwroot');

function fail(message) {
  console.error(`[www] ERROR: ${message}`);
  process.exit(1);
}

if (!fs.existsSync(outDir) || fs.readdirSync(outDir).length === 0) {
  fail(`out/ is empty or missing (${outDir}). Run the build step first.`);
}

if (!fs.existsSync(wwwrootDir)) {
  fail(`wwwroot not found (${wwwrootDir}).`);
}

// 1) Clear wwwroot completely
console.log(`[www] Clearing ${wwwrootDir} ...`);
for (const entry of fs.readdirSync(wwwrootDir)) {
  fs.rmSync(path.join(wwwrootDir, entry), { recursive: true, force: true });
}

// 2) Re-create .gitkeep
const gitkeep = path.join(wwwrootDir, '.gitkeep');
fs.writeFileSync(gitkeep, '');
console.log('[www] .gitkeep restored.');

// 3) Copy out/ -> wwwroot
console.log('[www] Copying out/ -> wwwroot ...');
function copyRecursive(src, dest) {
  const entries = fs.readdirSync(src, { withFileTypes: true });
  for (const entry of entries) {
    const srcPath = path.join(src, entry.name);
    const destPath = path.join(dest, entry.name);
    if (entry.isDirectory()) {
      fs.mkdirSync(destPath, { recursive: true });
      copyRecursive(srcPath, destPath);
    } else {
      fs.copyFileSync(srcPath, destPath);
    }
  }
}
copyRecursive(outDir, wwwrootDir);

console.log('[www] Done.');
