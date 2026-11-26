// Bundle the scripts referenced by the packaged HTML, including inline imports.
import { mkdtemp, readdir, readFile, rename, rm, writeFile } from 'node:fs/promises';
import { join, resolve } from 'node:path';
import { build } from 'esbuild';

const [rootArgument] = process.argv.slice(2);
if (!rootArgument) throw new Error('Pass the packaged web root.');
const root = resolve(rootArgument);
const source = join(root, 'js');
const output = await mkdtemp(join(root, '.hnmo-bundles-'));
const scriptPath = /(["'])\/js\/([\w./-]+\.js)\1/g;
const pages = new Map();
const modules = new Set();

for (const file of await readdir(root)) {
  if (!file.endsWith('.html')) continue;
  const page = join(root, file);
  const html = await readFile(page, 'utf8');
  pages.set(page, html);
  for (const [, , script] of html.matchAll(scriptPath)) modules.add(script);
}

const bundle = {
  outdir: output,
  outbase: source,
  entryNames: '[dir]/[name].min',
  bundle: true,
  format: 'esm',
  external: ['three'],
  logLevel: 'warning',
};
try {
  const preserveGameNames = process.env.HNMO_KEEP_JS_NAMES === '1' && modules.delete('main.js');
  if (preserveGameNames) {
    await build({
      ...bundle,
      entryPoints: [join(source, 'main.js')],
      minifyWhitespace: true,
      minifySyntax: true,
    });
  }
  await build({
    ...bundle,
    entryPoints: [...modules].map(script => join(source, script)),
    splitting: true,
    minify: true,
  });
  for (const [page, html] of pages) {
    const bundled = html.replace(scriptPath, (_, quote, script) =>
      `${quote}/js/${script.replace(/\.js$/, '.min.js')}${quote}`);
    await writeFile(page, bundled);
  }
  await rm(source, { recursive: true });
  await rename(output, source);
} finally {
  await rm(output, { recursive: true, force: true });
}
