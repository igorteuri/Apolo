// Runs the leveler of the web pages (CaptureScripts.cs) in node: the worklet with signals, and the page script with a fake browser.
// usage: node level-harness.js <page.js> <worklet.js>   (prints JSON)
'use strict';
const fs = require('fs');
const pageCode = fs.readFileSync(process.argv[2], 'utf8');
const workletCode = fs.readFileSync(process.argv[3], 'utf8');
const tick = ms => new Promise(r => setTimeout(r, ms || 0));
const RATE = 48000;

function loadLevel() {
  let cls = null;
  class AudioWorkletProcessor { constructor() { this.port = { postMessage: () => {} }; } }
  new Function('registerProcessor', 'AudioWorkletProcessor', 'sampleRate', workletCode)((n, c) => { if (n === 'apolo-level') cls = c; }, AudioWorkletProcessor, RATE);
  const p = new cls();
  return { p, set: d => p.port.onmessage({ data: d }) };
}

// a sine of the given amplitude for some seconds, in blocks of 128; returns gain (dB) over the last second and the highest output peak
function run(amplitude, seconds, config) {
  const l = loadLevel();
  if (config) l.set(config);
  const quanta = Math.floor(seconds * RATE / 128), lastSecond = Math.floor(RATE / 128);
  let inSum = 0, outSum = 0, peak = 0, maxDiff = 0, phase = 0;
  for (let q = 0; q < quanta; q++) {
    const a = new Float32Array(128), b = new Float32Array(128), oa = new Float32Array(128), ob = new Float32Array(128);
    for (let i = 0; i < 128; i++) { a[i] = amplitude * Math.sin(phase); b[i] = a[i]; phase += 2 * Math.PI * 440 / RATE; }
    l.p.process([[a, b]], [[oa, ob]]);
    for (let i = 0; i < 128; i++) {
      peak = Math.max(peak, Math.abs(oa[i]), Math.abs(ob[i]));
      maxDiff = Math.max(maxDiff, Math.abs(oa[i] - a[i]));
      if (q >= quanta - lastSecond) { inSum += a[i] * a[i]; outSum += oa[i] * oa[i]; }
    }
  }
  return { gainDb: 10 * Math.log10((outSum + 1e-12) / (inSum + 1e-12)), peak, maxDiff };
}

// the page script in a fake browser: where does the sound of a node of the page go?
async function page() {
  const msgs = [], listeners = [];
  class AudioNode {
    constructor(ctx) { this.context = ctx; this.outs = []; }
    connect(d) { this.outs.push(d); return d; }
    disconnect(d) { this.outs = d === undefined ? [] : this.outs.filter(x => x !== d); }
  }
  class AudioDestinationNode extends AudioNode {}
  class AudioWorkletNode extends AudioNode {
    constructor(ctx, name) { super(ctx); this.name = name; this.sent = []; this.port = { postMessage: d => this.sent.push(d) }; }
  }
  class FakeContext {
    constructor() {
      this.sampleRate = RATE;
      this.destination = new AudioDestinationNode(this);
      this.audioWorklet = { addModule: () => Promise.resolve() };
    }
    createGain() { const g = new AudioNode(this); g.gain = { value: 1 }; return g; }
    createScriptProcessor() { return new AudioNode(this); }
    createMediaElementSource() { return new AudioNode(this); }
    resume() {}
  }
  const media = [];
  const g = {
    AudioNode, AudioDestinationNode, AudioWorkletNode, AudioContext: FakeContext,
    HTMLMediaElement: class { play() {} },
    document: { querySelectorAll: () => media },
    location: { origin: 'https://www.youtube.com', href: 'https://www.youtube.com/watch' },
    URL, chrome: { webview: { postMessage: m => msgs.push(m), addEventListener: (t, f) => listeners.push(f) } },
    setInterval: () => 0, MessageChannel, btoa
  };
  g.window = g;
  const names = Object.keys(g);
  new Function(...names, pageCode)(...names.map(n => g[n]));
  const out = {};

  // leveler off: the sound goes straight to the speakers
  const ctx0 = new FakeContext();
  const n0 = new AudioNode(ctx0);
  n0.connect(ctx0.destination);
  out.offDirect = n0.outs.includes(ctx0.destination);

  // leveler on: the sound goes to the leveler, never straight to the speakers; the worklet takes it when loaded
  listeners.forEach(f => f({ data: 'apolo-level:1|-20|0.8|9|12' }));
  const ctx = new FakeContext();
  const n = new AudioNode(ctx);
  n.connect(ctx.destination);
  out.onDirect = n.outs.includes(ctx.destination);
  out.toLevel = n.outs.length > 0 && n.outs[0] !== ctx.destination;
  await tick(5);
  const entry = n.outs[0];
  const wn = entry && entry.outs.find(x => x instanceof AudioWorkletNode && x.name === 'apolo-level');
  out.worklet = !!wn;
  out.workletOutGoesOn = !!wn && wn.outs.length === 1;
  out.sentOn = !!wn && wn.sent.length > 0 && wn.sent[wn.sent.length - 1].on === true && wn.sent[wn.sent.length - 1].target === -20 && wn.sent[wn.sent.length - 1].boost === 9;

  // a change of the values reaches the worklet; switched off, it is told to let the sound through
  listeners.forEach(f => f({ data: 'apolo-level:0|-18|1|15|18' }));
  const last = wn ? wn.sent[wn.sent.length - 1] : {};
  out.offTold = last.on === false && last.target === -18 && last.strength === 1 && last.cut === 18;

  // media elements: protected or foreign media is not routed, a blob (YouTube) is
  listeners.forEach(f => f({ data: 'apolo-level:1|-20|0.8|9|12' }));
  const mk = (extra) => Object.assign({ paused: false, muted: false, volume: 1 }, extra);
  const drm = mk({ mediaKeys: {}, currentSrc: 'blob:https://www.youtube.com/1' });
  const foreign = mk({ currentSrc: 'https://cdn.other.com/a.mp3' });
  const noSrc = mk({ currentSrc: '' });
  media.push(drm, foreign, noSrc);
  const protoPlay = g.HTMLMediaElement.prototype.play;
  [drm, foreign, noSrc].forEach(el => protoPlay.call(el));
  out.drmHooked = !!drm.__apoloHooked;
  out.foreignHooked = !!foreign.__apoloHooked;
  out.noSrcHooked = !!noSrc.__apoloHooked;
  return out;
}

(async () => {
  const out = {};
  const cfg = { on: true, target: -20, strength: 0.8, boost: 9, cut: 12 };
  out.quiet = run(0.02, 14, cfg);
  out.loud = run(0.9, 14, cfg);
  out.off = run(0.3, 3, { on: false });
  out.unconfigured = run(0.3, 3, null);
  out.page = await page();
  console.log(JSON.stringify(out));
  process.exit(0);
})().catch(e => { console.log(JSON.stringify({ error: String(e && e.stack || e) })); process.exit(1); });
