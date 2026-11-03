/*
 Apolo - a fork of Ares (Copyright (c) Joerg Ruedenauer), distributed under the GPL.

 The scripts which take the sound of a web page from inside it (used when Apolo sends its sound and the PC itself does
 not play it): the page script hooks the audio of the page, and the worklet records it on the audio thread of the page.
 Kept free of window code so that the tests can run them (in node, with a fake browser).
*/
namespace Ares.Player
{
    internal static class CaptureScripts
    {
        public const string WorkletPath = "/__apolo-capture-worklet.js";

        // runs on the audio thread of the page: takes every 128 frames as they come (a busy page cannot make it lose any) and
        // hands them to the page in blocks of 2048 frames; the page sends them to Apolo when it has time
        public const string Worklet = @"class ApoloTap extends AudioWorkletProcessor {
 constructor() { super(); this.size = 2048; this.n = 0; this.l = new Float32Array(this.size); this.r = new Float32Array(this.size); }
 process(inputs) {
  var inp = inputs[0], frames = 128;
  if (inp && inp.length) frames = inp[0].length;
  for (var i = 0; i < frames; i++) {
   var a = inp && inp.length ? inp[0][i] : 0, b = inp && inp.length > 1 ? inp[1][i] : a;
   this.l[this.n] = a; this.r[this.n] = b;
   if (++this.n === this.size) {
    this.port.postMessage({ l: this.l, r: this.r }, [this.l.buffer, this.r.buffer]);
    this.l = new Float32Array(this.size); this.r = new Float32Array(this.size); this.n = 0;
   }
  }
  return true;
 }
}
registerProcessor('apolo-tap', ApoloTap);
";

        public const string Page = @"(function() {
 if (window.__apoloCap) return;
 var cap = window.__apoloCap = { on: false, leak: false };
 var wv = window.chrome && window.chrome.webview;
 if (!wv) return;
 var origConnect = AudioNode.prototype.connect, origDisconnect = AudioNode.prototype.disconnect;
 var queue = [], queued = 0;
 function toB64(i16) {
  var bytes = new Uint8Array(i16.buffer, i16.byteOffset, i16.byteLength), s = '';
  for (var i = 0; i < bytes.length; i += 8192) s += String.fromCharCode.apply(null, bytes.subarray(i, i + 8192));
  return btoa(s);
 }
 // resampling to 48 kHz keeps its position and its last sample from one block to the next (no clicks at the block borders)
 // every audio context of the page (a page can have several, e.g. one per video element) has its own stream and
 // resampler state: the program mixes the streams, so they must not be appended to one another
 var nextId = 0;
 function push(l, r, rate, rs, id) {
  if (!cap.on) return;
  var step = rate / 48000, N = l.length;
  // a block which does not go on where the last one ended (the page was busy and the browser lost a piece of sound):
  // the step is faded out over ~4 ms instead of being heard as a click (when the sound is whole nothing changes)
  if (rs.has && N >= 2) {
   var jl = rs.pl + rs.dl - l[0], jr = rs.pr + rs.dr - r[0];
   var lim = 0.018 + 4 * Math.max(Math.abs(rs.dl), Math.abs(rs.dr));
   if (Math.abs(jl) > lim || Math.abs(jr) > lim) {
    var l2 = new Float32Array(l), r2 = r === l ? l2 : new Float32Array(r), M = Math.min(N, Math.round(rate * 0.004));
    for (var j = 0; j < M; j++) {
     var w = 0.5 + 0.5 * Math.cos(Math.PI * j / M); // soft at both ends
     l2[j] += jl * w;
     if (r2 !== l2) r2[j] += jr * w;
    }
    l = l2; r = r2;
    rs.seams = (rs.seams || 0) + 1;
    try { wv.postMessage('apolo-seam:' + id + ':' + rs.seams); } catch (e) {}
   }
  }
  var out = new Int16Array(Math.ceil((N + 1) / step) * 2 + 4), o = 0;
  var t = rs.t;
  while (t < N - 1) {
   var k = Math.floor(t), f = t - k, a, b;
   if (k < 0) { a = rs.pl + (l[0] - rs.pl) * f; b = rs.pr + (r[0] - rs.pr) * f; }
   else { a = l[k] + (l[k + 1] - l[k]) * f; b = r[k] + (r[k + 1] - r[k]) * f; }
   a = a > 1 ? 1 : (a < -1 ? -1 : a); b = b > 1 ? 1 : (b < -1 ? -1 : b);
   out[o++] = a * 32767; out[o++] = b * 32767;
   t += step;
  }
  rs.t = t - N; rs.pl = l[N - 1]; rs.pr = r[N - 1];
  if (N >= 2) { rs.has = true; rs.dl = l[N - 1] - l[N - 2]; rs.dr = r[N - 1] - r[N - 2]; }
  try { wv.postMessage('apolo-pcm:' + id + ':' + toB64(out.subarray(0, o))); } catch (e) {}
 }
 // the browser cannot record the next sound while the handler of a block runs (it drops pieces of ~3 ms then, heard as
 // clicks just after the block borders): the handler only copies the block, the work is done right after in a new task
 var later = [], laterChannel = null;
 function runLater(fn) {
  if (!laterChannel) {
   laterChannel = new MessageChannel();
   laterChannel.port1.onmessage = function() { var fns = later; later = []; for (var i = 0; i < fns.length; i++) { try { fns[i](); } catch (e) {} } };
  }
  later.push(fn);
  if (later.length === 1) laterChannel.port2.postMessage(0);
 }
 // everything the page plays goes into one entry per audio context; behind it the recorder of the page thread works at
 // once, and the recorder of the audio thread (nothing lost when the page is busy) takes over as soon as it is loaded
 function tapOf(ctx) {
  if (ctx.__apoloTap) return ctx.__apoloTap;
  var entry = ctx.createGain();
  entry.__apoloIsTap = true;
  var sp = ctx.createScriptProcessor(8192, 2, 2); // fewer, larger blocks: the page thread is woken less often
  sp.__apoloIsTap = true;
  var id = ++nextId, rs = { t: 0, pl: 0, pr: 0 }, onWorklet = false;
  sp.onaudioprocess = function(e) {
   if (!cap.on || onWorklet) return;
   var ib = e.inputBuffer, l = ib.getChannelData(0).slice(0), r = ib.numberOfChannels > 1 ? ib.getChannelData(1).slice(0) : l;
   var rate = ctx.sampleRate;
   runLater(function() { push(l, r, rate, rs, id); });
  };
  origConnect.call(entry, sp);
  origConnect.call(sp, ctx.destination);
  ctx.__apoloTap = entry;
  startWorklet(ctx, entry, sp, rs, id, function() { onWorklet = true; });
  return entry;
 }
 function capMode(text) { try { wv.postMessage('apolo-capmode:' + text); } catch (e) {} }
 function startWorklet(ctx, entry, sp, rs, id, done) {
  try {
   if (!ctx.audioWorklet || !window.AudioWorkletNode || !location.origin || location.origin === 'null') { capMode('page thread (no worklet here)'); return; }
   ctx.audioWorklet.addModule(location.origin + '/__apolo-capture-worklet.js').then(function() {
    var wn = new AudioWorkletNode(ctx, 'apolo-tap', { numberOfInputs: 1, numberOfOutputs: 1, outputChannelCount: [2], channelCount: 2, channelCountMode: 'explicit' });
    wn.__apoloIsTap = true;
    wn.port.onmessage = function(e) { if (cap.on) push(e.data.l, e.data.r, ctx.sampleRate, rs, id); };
    origConnect.call(entry, wn);
    origConnect.call(wn, ctx.destination);
    done();
    try { origDisconnect.call(entry, sp); origDisconnect.call(sp); } catch (e) {}
    capMode('audio thread (worklet)');
   }, function(e) { capMode('page thread (worklet refused: ' + (e && e.name) + ')'); });
  } catch (e) { capMode('page thread (' + (e && e.message) + ')'); }
 }
 // while Apolo takes the sound, what would go to the speakers goes to a gain of 0 instead: the tab is not muted, so the
 // browser keeps the clock of the real sound card (a muted tab runs on a software clock which is ~1.4 % slow and uneven:
 // heard as short pauses and a faint crackle in the sent sound)
 function sinkOf(ctx) {
  if (ctx.__apoloSink) return ctx.__apoloSink;
  var g = ctx.createGain();
  g.gain.value = 0;
  g.__apoloIsTap = true;
  origConnect.call(g, ctx.destination);
  ctx.__apoloSink = g;
  return g;
 }
 AudioNode.prototype.connect = function(dest) {
  // something went to the speakers before the capture was on: that sound can not be taken out of the page any more
  if (!cap.on && dest instanceof AudioDestinationNode && !this.__apoloIsTap) cap.leak = true;
  if (cap.on && dest instanceof AudioDestinationNode && !this.__apoloIsTap) {
   try {
    origConnect.call(this, tapOf(dest.context));
    origConnect.call(this, sinkOf(dest.context));
    this.__apoloSunk = true;
    return dest;
   } catch (e) {}
  }
  var r = origConnect.apply(this, arguments);
  try { if (dest instanceof AudioDestinationNode && !this.__apoloIsTap) origConnect.call(this, tapOf(dest.context)); } catch (e) {}
  return r;
 };
 AudioNode.prototype.disconnect = function(dest) {
  var all = arguments.length === 0;
  try {
   if ((all || dest instanceof AudioDestinationNode) && this.context && this.context.__apoloTap && !this.__apoloIsTap)
    origDisconnect.call(this, this.context.__apoloTap);
  } catch (e) {}
  if (this.__apoloSunk && (all || dest instanceof AudioDestinationNode)) {
   // it was connected to the silent gain, not to the speakers
   try { origDisconnect.call(this, this.context.__apoloSink); } catch (e) {}
   this.__apoloSunk = false;
   if (!all) return;
  }
  return origDisconnect.apply(this, arguments);
 };
 function hook(el) {
  if (el.__apoloHooked) return;
  el.__apoloHooked = true;
  try {
   // 'playback': a deeper buffer in the browser, steadier when the PC is busy (nobody listens on this PC anyway)
   var ctx = new (window.AudioContext || window.webkitAudioContext)({ latencyHint: 'playback' });
   var src = ctx.createMediaElementSource(el);
   src.connect(ctx.destination);
   ctx.resume();
   el.__apoloRouted = true;
  } catch (e) {
   // the page made its own source for this element: its sound goes through the graph of the page (watched by cap.leak)
   if (e && e.name === 'InvalidStateError') el.__apoloRouted = true;
  }
 }
 // The tab is unmuted (so that the browser keeps the clock of the real sound card) only while ALL the sound is known to go
 // through Apolo. This tells the program: 1 = routed, 0:<reason> = not (the program keeps the tab muted then).
 var lastState = '';
 function report() {
  if (!cap.on) return;
  var state = '1';
  if (cap.leak) state = '0:graph-before-capture';
  else {
   var els = document.querySelectorAll('audio,video');
   for (var i = 0; i < els.length; i++) {
    var el = els[i];
    if (!el.paused && !el.ended && !el.muted && el.volume > 0 && !el.__apoloRouted) { state = '0:media-not-routed'; break; }
   }
  }
  if (state !== lastState) { lastState = state; try { wv.postMessage('apolo-routed:' + state); } catch (e) {} }
 }
 setInterval(report, 400);
 var origPlay = HTMLMediaElement.prototype.play;
 HTMLMediaElement.prototype.play = function() { if (cap.on) hook(this); return origPlay.apply(this, arguments); };
 function hookAll() { var els = document.querySelectorAll('audio,video'); for (var i = 0; i < els.length; i++) hook(els[i]); }
 wv.addEventListener('message', function(e) {
  if (e.data === 'apolo-capture:on') { cap.on = true; hookAll(); }
  else if (e.data === 'apolo-capture:off') { cap.on = false; }
 });
 wv.postMessage('apolo-hello');
})();";
    }
}
