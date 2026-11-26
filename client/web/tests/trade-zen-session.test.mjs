// A trade's zen box belongs to the trade it was asked in (Trade.askGold): an answer that comes after that trade ended,
// or after another trade began, puts no zen in. The box is answered by the test (options.prompt), so its timing is the
// test's.
import test from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

globalThis.window ??= { addEventListener() {}, innerWidth: 800, innerHeight: 600 };
globalThis.document ??= { addEventListener() {}, documentElement: { lang: 'en' } };
globalThis.localStorage ??= { getItem() { return null; }, setItem() {} };

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const { Trade } = await import(pathToFileURL(path.resolve(__dirname, '../public/js/ui/tradeWindow.js')).href);

const deferred = () => { let resolve; const promise = new Promise((res) => { resolve = res; }); return { promise, resolve }; };
const name = (text) => [...new TextEncoder().encode(text.padEnd(10, '\0'))].slice(0, 10);
const accepted = (partner) => new Uint8Array([0xc1, 16, 0x37, 1, ...name(partner), 0, 10]);
const finished = () => new Uint8Array([0xc1, 4, 0x3d, 0]);

function setUp() {
  const sent = [];
  let onPacket = () => undefined;
  const connection = { onPacket(fn) { onPacket = fn; return () => undefined; }, send(group, packet, fields) { sent.push({ packet, fields }); } };
  const view = { partner: new Map(), warnings: new Set(), myGold: 0, partnerGold: 0, myConfirmed: false, partnerConfirmed: false };
  const itemWindows = {
    trade: { view, refresh() {} },
    openTrade: async () => undefined,
    closeTrade() {},
    picked: {},
  };
  const inventory = { money: 1_000_000, trade: new Map(), items: new Map() };
  const answers = [];
  const trade = new Trade({
    connection, inventory, itemWindows,
    message() {}, confirm: async () => true,
    prompt: () => { const d = deferred(); answers.push(d); return d.promise; },
  });
  return { trade, sent, feed: (bytes) => onPacket(bytes), answers, moneySent: () => sent.filter((s) => s.packet === 'SetTradeMoney') };
}

test('an answer in the same trade puts the zen in', async () => {
  const { trade, feed, answers, moneySent } = setUp();
  feed(accepted('Alice'));
  await new Promise((r) => setTimeout(r, 0));
  const asking = trade.askGold();
  answers[0].resolve('5000');
  await asking;
  assert.deepEqual(moneySent().map((s) => s.fields.Amount), [5000]);
});

test('an answer after the trade ended and another one began puts nothing in the new trade', async () => {
  const { trade, feed, answers, moneySent } = setUp();
  feed(accepted('Alice'));
  await new Promise((r) => setTimeout(r, 0));
  const asking = trade.askGold();
  feed(finished());
  feed(accepted('Bob'));
  await new Promise((r) => setTimeout(r, 0));
  assert.equal(trade.isOpen, true, 'the second trade is open');
  answers[0].resolve('5000');
  await asking;
  assert.deepEqual(moneySent(), [], 'the first trade\'s amount went into the second trade');
});

test('an answer after the trade ended puts nothing in', async () => {
  const { trade, feed, answers, moneySent } = setUp();
  feed(accepted('Alice'));
  await new Promise((r) => setTimeout(r, 0));
  const asking = trade.askGold();
  feed(finished());
  answers[0].resolve('5000');
  await asking;
  assert.deepEqual(moneySent(), []);
});
