// Unit tests for #20: parallel R2 file upload with progress in hnmo-package workflow.
// Run: node --test client/web/tests/r2-upload.test.mjs
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import cp from 'node:child_process';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '../../..');
const workflowPath = path.join(repoRoot, '.github/workflows/hnmo-package.yml');
const scriptPath = path.join(repoRoot, 'tools/package/upload-r2-files.sh');

function findBash() {
  const candidates = process.platform === 'win32'
    ? ['C:\\Program Files\\Git\\bin\\bash.exe', 'bash']
    : ['bash'];
  for (const cmd of candidates) {
    const res = cp.spawnSync(cmd, ['--version'], { stdio: 'ignore' });
    if (res.status === 0) return cmd;
  }
  return null;
}

test('upload-r2-files.sh uses xargs -P 8 by default', () => {
  assert.ok(fs.existsSync(scriptPath), 'tools/package/upload-r2-files.sh must exist');
  const script = fs.readFileSync(scriptPath, 'utf8');
  assert.match(script, /xargs\b[\s\S]*?-P\s+"?\$\{?R2_UPLOAD_PARALLEL:-8\}?"?/);
});

test('upload-r2-files.sh skips existing hashes, deduplicates, handles spaces, and prints progress', (t) => {
  const bash = findBash();
  if (!bash) {
    t.skip('bash is not available in PATH');
    return;
  }
  assert.ok(fs.existsSync(scriptPath), 'tools/package/upload-r2-files.sh must exist');

  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'hnmo-r2-test-'));
  try {
    const srcDir = path.join(tmp, 'HNMO');
    const binDir = path.join(tmp, 'bin');
    const uploadsLog = path.join(tmp, 'uploads.log');
    fs.mkdirSync(path.join(srcDir, 'sub dir'), { recursive: true });
    fs.mkdirSync(binDir, { recursive: true });
    fs.writeFileSync(path.join(srcDir, 'already.txt'), 'already');
    fs.writeFileSync(path.join(srcDir, 'Start HNMO.bat'), 'bat');
    fs.writeFileSync(path.join(srcDir, 'dup1.txt'), 'same');
    fs.writeFileSync(path.join(srcDir, 'dup2.txt'), 'same');
    fs.writeFileSync(path.join(srcDir, 'sub dir/file (1).dat'), 'dat');

    const manifestPath = path.join(srcDir, 'manifest.json');
    fs.writeFileSync(manifestPath, JSON.stringify({
      version: '1.0.0',
      files: [
        { path: 'already.txt', sha256: 'hash_already' },
        { path: 'Start HNMO.bat', sha256: 'hash_bat' },
        { path: 'dup1.txt', sha256: 'hash_dup' },
        { path: 'dup2.txt', sha256: 'hash_dup' },
        { path: 'sub dir/file (1).dat', sha256: 'hash_sub' },
      ],
    }));

    const havePath = path.join(tmp, 'have.txt');
    fs.writeFileSync(havePath, 'hash_already\n');

    const fakeAws = path.join(binDir, 'aws');
    fs.writeFileSync(fakeAws, `#!/usr/bin/env bash
set -euo pipefail
if [ "\${1:-}" = "s3" ] && [ "\${2:-}" = "cp" ]; then
  printf '%s -> %s\\n' "$3" "$4" >> "${uploadsLog.replace(/\\/g, '/')}"
  exit 0
fi
exit 1
`, { mode: 0o755 });
    if (process.platform === 'win32') {
      fs.writeFileSync(path.join(binDir, 'aws.cmd'), `@echo off\r\n"${bash}" "${fakeAws}" %*\r\n`);
    }

    const toPosix = (p) => (process.platform === 'win32'
      ? p.replace(/^([A-Za-z]):\\/, (_, d) => `/${d.toLowerCase()}/`).replace(/\\/g, '/')
      : p);

    const res = cp.spawnSync(bash, [toPosix(scriptPath), toPosix(srcDir), toPosix(manifestPath), toPosix(havePath), 'test-bucket', 'https://example.r2'], {
      env: {
        ...process.env,
        PATH: `${toPosix(binDir)}:${process.env.PATH ?? ''}`,
        AWS_CMD: toPosix(fakeAws),
        R2_UPLOAD_PARALLEL: '4',
        R2_PROGRESS_EVERY: '1',
      },
      encoding: 'utf8',
    });

    assert.equal(res.status, 0, `Script failed: ${res.stderr}\n${res.stdout}`);
    assert.match(res.stdout, /3 files to upload \(4 parallel\)/);
    assert.match(res.stdout, /\[R2 \d+\/3\]/);

    const uploaded = fs.readFileSync(uploadsLog, 'utf8').trim().split(/\r?\n/).sort();
    assert.equal(uploaded.length, 3, `Expected 3 unique missing files uploaded, got ${uploaded.length}: ${uploaded.join('; ')}`);
    assert.ok(uploaded.some((l) => l.endsWith(' -> s3://test-bucket/files/hash_bat') && l.includes('Start HNMO.bat')));
    assert.ok(uploaded.some((l) => l.endsWith(' -> s3://test-bucket/files/hash_dup') && l.includes('dup1.txt')));
    assert.ok(uploaded.some((l) => l.endsWith(' -> s3://test-bucket/files/hash_sub') && l.includes('sub dir/file (1).dat')));

    // Empty run (all hashes already in have.txt) exits cleanly with 0 files to upload.
    fs.writeFileSync(havePath, 'hash_already\nhash_bat\nhash_dup\nhash_sub\n');
    const emptyRes = cp.spawnSync(bash, [toPosix(scriptPath), toPosix(srcDir), toPosix(manifestPath), toPosix(havePath), 'test-bucket', 'https://example.r2'], {
      env: { ...process.env, PATH: `${toPosix(binDir)}:${process.env.PATH ?? ''}`, AWS_CMD: toPosix(fakeAws) },
      encoding: 'utf8',
    });
    assert.equal(emptyRes.status, 0, emptyRes.stderr);
    assert.match(emptyRes.stdout, /0 files to upload/);

    // Failing aws cp aborts with non-zero exit status so partial manifests are never pushed.
    fs.writeFileSync(havePath, '');
    const failAws = path.join(binDir, 'aws-fail');
    fs.writeFileSync(failAws, '#!/usr/bin/env bash\nexit 7\n', { mode: 0o755 });
    const failRes = cp.spawnSync(bash, [toPosix(scriptPath), toPosix(srcDir), toPosix(manifestPath), toPosix(havePath), 'test-bucket', 'https://example.r2'], {
      env: { ...process.env, PATH: `${toPosix(binDir)}:${process.env.PATH ?? ''}`, AWS_CMD: toPosix(failAws) },
      encoding: 'utf8',
    });
    assert.notEqual(failRes.status, 0);
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
});
