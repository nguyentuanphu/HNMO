// Verification tests for unified Windows launcher, macOS .command, Linux scripts, and package-hnmo.sh.
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const HNMO_DIR = path.resolve(__dirname, '../tools/hnmo');
const PACKAGE_SH = path.resolve(__dirname, '../tools/package-hnmo.sh');

test('single Windows Start HNMO.bat exists without duplicate window bat', () => {
  assert.equal(fs.existsSync(path.join(HNMO_DIR, 'Start HNMO.bat')), true);
  assert.equal(fs.existsSync(path.join(HNMO_DIR, 'Start HNMO (window).bat')), false);

  const bat = fs.readFileSync(path.join(HNMO_DIR, 'Start HNMO.bat'), 'utf8');
  assert.match(bat, /\/window/i);
  assert.match(bat, /--window/i);
  assert.match(bat, /HNMO_WINDOW/);
  assert.match(bat, /ConsoleWindow/);
  assert.match(bat, /start-hidden\.vbs/);
});

test('macOS Start HNMO.command and Linux start/stop scripts exist and have valid bash syntax', () => {
  const macCmd = path.join(HNMO_DIR, 'Start HNMO.command');
  const startSh = path.join(HNMO_DIR, 'start-hnmo.sh');
  const stopSh = path.join(HNMO_DIR, 'stop-hnmo.sh');
  assert.equal(fs.existsSync(macCmd), true);
  assert.equal(fs.existsSync(startSh), true);
  assert.equal(fs.existsSync(stopSh), true);

  const macText = fs.readFileSync(macCmd, 'utf8');
  assert.match(macText, /start-hnmo\.sh/);
  assert.ok(!macText.includes('\r\n'), 'macOS .command file must use LF line endings');

  const stopText = fs.readFileSync(stopSh, 'utf8');
  assert.match(stopText, /\/api\/control\/shutdown/);
  assert.match(stopText, /pg_ctl stop/);

  const bash = process.platform === 'win32' ? 'C:\\Program Files\\Git\\bin\\bash.exe' : 'bash';
  if (fs.existsSync(bash) || process.platform !== 'win32') {
    for (const script of [macCmd, startSh, stopSh]) {
      execFileSync(bash, ['-n', script], { stdio: 'pipe' });
    }
  }
});

test('package-hnmo.sh ships Start HNMO.bat and Start HNMO.command cleanly', () => {
  const pkg = fs.readFileSync(PACKAGE_SH, 'utf8');
  assert.match(pkg, /Start HNMO\.bat/);
  assert.match(pkg, /Start HNMO\.command/);
  assert.doesNotMatch(pkg, /Start HNMO \(window\)\.bat/);
  assert.match(pkg, /chmod \+x .*Start HNMO\.command/);
});

test('hnmo-tray.ps1 supports full process-tree and child cleanup on Stop HNMO', () => {
  const tray = fs.readFileSync(path.join(HNMO_DIR, 'launcher/hnmo-tray.ps1'), 'utf8');
  assert.match(tray, /\/api\/control\/shutdown/);
  assert.match(tray, /taskkill\.exe/);
  assert.match(tray, /pg_ctl\.exe/);
  assert.match(tray, /server\.pid/);
});
