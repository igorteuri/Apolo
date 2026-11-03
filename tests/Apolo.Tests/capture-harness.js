// Runs the real capture scripts of Apolo (CaptureScripts.cs) against a fake browser and prints what came out (JSON).
// usage: node capture-harness.js <page.js> <worklet.js>
'use strict';
const fs = require('fs');
const pageCode = fs.readFileSync(process.argv[2], 'utf8');
const workletCode = fs.readFileSync(process.argv[3], 'utf8');
const tick = ms => new Promise(r => setTimeout(r, ms || 0));

// a new fake browser for each scenario (the page script installs itself once per window)
function browser(withWorklet) {
  const msgs = [], listeners = [];
  class AudioNode {
    constructor(ctx) { this.context = ctx; this.outs = []; }
    connect(d) { this.outs.push(d); return d; }
    disconnect(d) { this.outs = d === undefined ? [] : this.outs.filter(x => x !== d); }
  }
  class AudioDestinationNode extends AudioNode {}
  const created = { sp: [], worklets: [], urls: [] };
  let registry = {};
  class FakeContext {
    constructor(rate) {
      this.sampleRate = rate;
      this.destination = new AudioDestinationNode(this);
      if (withWorklet) {
        this.audioWorklet = {
          addModule: url => {
            created.urls.push(url);
            class AudioWorkletProcessor { constructor() { this.port = { postMessage: () => {} }; } }
            new Function('registerProcessor', 'AudioWorkletProcessor', workletCode)((n, c) => { registry[n] = c; }, AudioWorkletProcessor);
            return Promise.resolve();
          }
        };
      }
    }
    createGain() { const g = new AudioNode(this); g.gain = { value: 1 }; return g; }
    createScriptProcessor(n) { const sp = new AudioNode(this); sp.bufferSize = n; created.sp.push(sp); return sp; }
  }
  class AudioWorkletNode extends AudioNode {
    constructor(ctx, name) {
      super(ctx);
      this.port = { onmessage: null };
      this.proc = new registry[name]();
      // the port of the audio thread: messages arrive later, in order
      this.proc.port.postMessage = d => setTimeout(() => { if (this.port.onmessage) this.port.onmessage({ data: d }); }, 0);
      created.worklets.push(this);
    }
  }
  const g = {
    AudioNode, AudioDestinationNode, AudioWorkletNode: withWorklet ? AudioWorkletNode : undefined,
    HTMLMediaElement: class { play() {} },
    document: { querySelectorAll: () => [] },
    location: { origin: 'https://www.youtube.com' },
    chrome: { webview: { postMessage: m => msgs.push(m), addEventListener: (t, f) => listeners.push(f) } },
    setInterval: () => 0, MessageChannel, btoa
  };
  g.window = g;
  const names = Object.keys(g);
  new Function(...names, pageCode)(...names.map(n => g[n]));
  listeners.forEach(f => f({ data: 'apolo-capture:on' }));
  const ctx = new FakeContext(48000);
  new AudioNode(ctx).connect(ctx.destination);   // a sound of the page goes to the speakers
  return { msgs, created, ctx };
}

function sine(n, rate) { return 0.5 * Math.sin(2 * Math.PI * 440 * n / rate); }

function result(msgs) {
  const L = [], R = [];
  for (const m of msgs) {
    if (!m.startsWith('apolo-pcm:')) continue;
    const buf = Buffer.from(m.split(':')[2], 'base64');
    for (let i = 0; i < buf.length; i += 4) { L.push(buf.readInt16LE(i)); R.push(buf.readInt16LE(i + 2)); }
  }
  let maxJump = 0, lrOk = true;
  for (let i = 2; i < L.length; i++) maxJump = Math.max(maxJump, Math.abs(L[i] - 2 * L[i - 1] + L[i - 2]));
  for (let i = 0; i < L.length; i++) if (Math.abs(L[i] + R[i]) > 2) { lrOk = false; break; }
  const mode = msgs.filter(m => m.startsWith('apolo-capmode:')).map(m => m.substring(14));
  const seams = msgs.filter(m => m.startsWith('apolo-seam:')).length;
  return { samples: L.length, maxJump, lrOk, capmode: mode.join('|'), seams };
}

// the browser fires blocks of the script processor into the same memory again and again
async function scriptBlocks(b, blocks, dropBlock) {
  const sp = b.created.sp[0], N = 8192, l = new Float32Array(N), r = new Float32Array(N);
  for (let k = 0; k < blocks; k++) {
    for (let i = 0; i < N; i++) { l[i] = sine(k * N + i, 48000); r[i] = -l[i]; }
    if (k === dropBlock) continue;
    sp.onaudioprocess({ inputBuffer: { numberOfChannels: 2, getChannelData: c => (c ? r : l) } });
    l.fill(9); r.fill(9);   // the next sound is written right after the handler returned
  }
  await tick(20);
}

(async () => {
  const out = {};

  const a = browser(false);
  await tick(5);
  await scriptBlocks(a, 6, -1);
  out.script = result(a.msgs);

  const d = browser(false);
  await tick(5);
  await scriptBlocks(d, 6, 3);
  out.lost = result(d.msgs);

  const w = browser(true);
  await tick(5);   // the worklet module loads
  const node = w.created.worklets[0];
  if (node) {
    const quanta = 6 * 8192 / 128;
    for (let q = 0; q < quanta; q++) {
      const l = new Float32Array(128), r = new Float32Array(128);
      for (let i = 0; i < 128; i++) { l[i] = sine(q * 128 + i, 48000); r[i] = -l[i]; }
      node.proc.process([[l, r]]);
    }
  }
  await tick(20);
  const before = w.msgs.filter(m => m.startsWith('apolo-pcm:')).length;
  await scriptBlocks(w, 1, -1);   // the old recorder must stay quiet once the worklet works
  out.worklet = result(w.msgs);
  out.worklet.url = w.created.urls[0] || '';
  out.worklet.scriptAfter = w.msgs.filter(m => m.startsWith('apolo-pcm:')).length - before;
  console.log(JSON.stringify(out));
  process.exit(0);   // the message channels of the page script would keep node running
})().catch(e => { console.log(JSON.stringify({ error: String(e && e.stack || e) })); process.exit(1); });
