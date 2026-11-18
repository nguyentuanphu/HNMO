#!/usr/bin/env node
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { Worker, isMainThread, parentPort, workerData } from 'node:worker_threads';
import { pathToFileURL } from 'node:url';

const OZJ_PREFIX_BYTES = 24;
const OZT_PREFIX_BYTES = 4;
const SKIP_TOP_FOLDERS = new Set(['interface', 'local', 'logo', 'ingameshopscript', 'fonts']);
const TERRAIN_TILE_RE = /^world\d+\/(?:tile|exttile|terrain)/i;
const TEXTURE_EXT_RE = /\.(ozj|ozt|jpe?g|tga)$/i;

function shouldConvert(relPath) {
  const normalized = relPath.replace(/\\/g, '/').replace(/^\/+/, '');
  if (!TEXTURE_EXT_RE.test(normalized)) return false;
  const slash = normalized.indexOf('/');
  if (slash > 0 && SKIP_TOP_FOLDERS.has(normalized.slice(0, slash).toLowerCase())) {
    return false;
  }
  if (TERRAIN_TILE_RE.test(normalized)) {
    return false;
  }
  return true;
}

if (!isMainThread) {
  const { dataDir, jobs, formatsUrl } = workerData;
  const { default: sharp } = await import('sharp');
  const { encodeToKTX2 } = await import('ktx2-encoder');
  const { decodeTGA } = await import(formatsUrl);

  const results = [];
  for (const relPath of jobs) {
    const fullPath = path.join(dataDir, relPath);
    const ext = path.extname(relPath).toLowerCase();
    const isTga = ext === '.ozt' || ext === '.tga';
    try {
      const raw = fs.readFileSync(fullPath);
      let rgba;
      let width;
      let height;

      if (isTga) {
        const payload = ext === '.ozt' ? raw.subarray(OZT_PREFIX_BYTES) : raw;
        const decoded = decodeTGA(new Uint8Array(payload.buffer, payload.byteOffset, payload.byteLength));
        rgba = decoded.pixels;
        width = decoded.width;
        height = decoded.height;
      } else {
        const payload = ext === '.ozj' ? raw.subarray(OZJ_PREFIX_BYTES) : raw;
        const { data, info } = await sharp(payload).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
        rgba = new Uint8Array(data.buffer, data.byteOffset, data.byteLength);
        width = info.width;
        height = info.height;
      }

      if (width < 4 || height < 4) {
        results.push({ relPath, ok: false, reason: 'too-small' });
        continue;
      }

      const imageDecoder = async () => ({ data: rgba, width, height });
      const ktx2Bytes = await encodeToKTX2(rgba, {
        imageDecoder,
        isUASTC: isTga,
        needSupercompression: isTga,
        enableRDO: isTga,
        qualityLevel: isTga ? undefined : 200,
        compressionLevel: isTga ? undefined : 2,
        generateMipmap: true,
        isSetKTX2SRGBTransferFunc: false,
        isYFlip: false,
      });

      const outRel = relPath.slice(0, -ext.length) + '.ktx2';
      const outFull = path.join(dataDir, outRel);
      fs.writeFileSync(outFull, ktx2Bytes);
      results.push({
        relPath,
        outRel,
        ok: true,
        srcBytes: raw.length,
        ktxBytes: ktx2Bytes.length,
      });
    } catch (err) {
      results.push({ relPath, ok: false, reason: err instanceof Error ? err.message : String(err) });
    }
  }
  parentPort.postMessage(results);
} else {
  const args = process.argv.slice(2);
  if (args.length < 2) {
    console.error('Usage: node convert-textures-ktx2.mjs <dataDir> <allowlistFile>');
    process.exit(1);
  }

  const dataDir = path.resolve(args[0]);
  const allowlistFile = path.resolve(args[1]);
  const webDir = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1')), '..');
  const formatsUrl = pathToFileURL(path.join(webDir, 'public', 'js', 'formats.js')).href;

  const lines = fs.readFileSync(allowlistFile, 'utf8')
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0 && !line.startsWith('#'));

  const jobs = [];
  const seenStem = new Set();
  for (const rel of lines) {
    if (!shouldConvert(rel)) continue;
    const stem = rel.slice(0, -path.extname(rel).length).toLowerCase();
    if (seenStem.has(stem)) continue;
    seenStem.add(stem);
    if (fs.existsSync(path.join(dataDir, rel))) {
      jobs.push(rel);
    }
  }

  const workerCount = Math.min(Math.max(1, os.cpus().length), Math.max(1, jobs.length));
  const buckets = Array.from({ length: workerCount }, () => []);
  jobs.forEach((job, idx) => buckets[idx % workerCount].push(job));

  const t0 = performance.now();
  const allResults = (await Promise.all(buckets.map((bucket) => new Promise((resolve, reject) => {
    const worker = new Worker(new URL(import.meta.url), {
      workerData: { dataDir, jobs: bucket, formatsUrl },
    });
    worker.on('message', resolve);
    worker.on('error', reject);
  })))).flat();

  let converted = 0;
  let failed = 0;
  let srcTotal = 0;
  let ktxTotal = 0;
  const added = [];
  for (const res of allResults) {
    if (res.ok) {
      converted++;
      srcTotal += res.srcBytes;
      ktxTotal += res.ktxBytes;
      added.push(res.outRel);
    } else {
      failed++;
    }
  }

  if (added.length > 0) {
    fs.appendFileSync(allowlistFile, added.join('\n') + '\n', 'utf8');
  }

  const secs = ((performance.now() - t0) / 1000).toFixed(1);
  console.log(
    `KTX2: converted ${converted}/${jobs.length} textures (${failed} skipped/kept original) in ${secs}s: `
    + `${(srcTotal / (1024 * 1024)).toFixed(2)} MB -> ${(ktxTotal / (1024 * 1024)).toFixed(2)} MB`,
  );
}
