// Test of the plugin against a fake Stream Deck (WebSocket) and a fake Apolo (HTTP).  node tools/test_plugin.js
const net = require('net'), http = require('http'), crypto = require('crypto'), path = require('path'), { spawn } = require('child_process');
const assert = require('assert');

const KEY = 'secretkey1234567890';
const cmds = [];
const state = { playing: true, paused: false, title: 'Song A', artist: '', list: 'L', position: 1, length: 100, repeat: false,
    vol: { overall: 60, music: 50, sound: 40 }, modes: ['Taverna', 'Batalha'], mode: 0,
    elements: [{ id: 11, title: 'Trovao', active: false }] };
let hostOk = true;
const apolo = http.createServer((req, res) => {
    if (req.headers.host !== 'localhost:' + apolo.address().port) hostOk = false;
    let body = '';
    req.on('data', d => body += d);
    req.on('end', () => {
        if (req.url === '/api/login') {
            if (body === 'password=' + KEY) { res.end('TOKEN1'); } else { res.statusCode = 401; res.end('wrong'); }
            return;
        }
        if (req.headers['x-token'] !== 'TOKEN1') { res.statusCode = 401; res.end('login'); return; }
        if (req.url === '/api/state') { res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify(state)); return; }
        if (req.url === '/api/project') { res.end(JSON.stringify([{ title: 'Taverna', elements: [{ id: 11, title: 'Trovao' }] }, { title: 'Batalha', elements: [{ id: 21, title: 'Espada' }] }])); return; }
        if (req.url === '/api/cmd') {
            const p = new URLSearchParams(body);
            cmds.push(p.get('cmd') + ':' + p.get('arg'));
            if (p.get('cmd') === 'mode') state.mode = parseInt(p.get('arg'), 10);
            res.end('ok'); return;
        }
        res.statusCode = 404; res.end('nf');
    });
});

// fake Stream Deck
const received = [];
let clientSock = null, buf = Buffer.alloc(0), upgraded = false;
function frame(text) {
    const p = Buffer.from(text); let head;
    if (p.length < 126) head = Buffer.from([0x81, p.length]);
    else { head = Buffer.alloc(4); head[0] = 0x81; head[1] = 126; head.writeUInt16BE(p.length, 2); }
    return Buffer.concat([head, p]);
}
const sdServer = net.createServer(sock => {
    clientSock = sock;
    sock.on('data', d => {
        buf = Buffer.concat([buf, d]);
        if (!upgraded) {
            const end = buf.indexOf('\r\n\r\n'); if (end < 0) return;
            const key = /Sec-WebSocket-Key: (.*)\r\n/.exec(buf.slice(0, end).toString())[1];
            const acc = crypto.createHash('sha1').update(key + '258EAFA5-E914-47DA-95CA-C5AB0DC85B11').digest('base64');
            sock.write('HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: ' + acc + '\r\n\r\n');
            buf = buf.slice(end + 4); upgraded = true;
        }
        for (;;) {
            if (buf.length < 2) return;
            let len = buf[1] & 0x7f, off = 2;
            if (len === 126) { if (buf.length < 4) return; len = buf.readUInt16BE(2); off = 4; }
            if (buf.length < off + 4 + len) return;
            const mask = buf.slice(off, off + 4); const pl = Buffer.from(buf.slice(off + 4, off + 4 + len));
            for (let i = 0; i < pl.length; i++) pl[i] ^= mask[i & 3];
            buf = buf.slice(off + 4 + len);
            received.push(JSON.parse(pl.toString()));
        }
    });
});
const sleep = ms => new Promise(r => setTimeout(r, ms));
const send = o => clientSock.write(frame(JSON.stringify(o)));
const find = (ev, pred) => received.filter(m => m.event === ev && (!pred || pred(m)));

(async () => {
    await new Promise(r => apolo.listen(0, '127.0.0.1', r));
    await new Promise(r => sdServer.listen(0, '127.0.0.1', r));
    const aport = apolo.address().port;
    const child = spawn(process.execPath, [path.join(__dirname, '..', 'com.igorteuri.apolo.sdPlugin', 'bin', 'plugin.js'),
        '-port', sdServer.address().port, '-pluginUUID', 'PLUG', '-registerEvent', 'registerPlugin', '-info', JSON.stringify({ application: { language: 'pt_BR' } })], { stdio: 'inherit' });
    await sleep(400);
    assert.deepStrictEqual(find('registerPlugin')[0], { event: 'registerPlugin', uuid: 'PLUG' });
    assert(find('getGlobalSettings').length === 1);
    send({ event: 'didReceiveGlobalSettings', payload: { settings: { port: String(aport), key: KEY } } });
    const P = 'com.igorteuri.apolo.';
    send({ event: 'willAppear', action: P + 'playpause', context: 'c1', payload: { settings: { showTitle: true }, controller: 'Keypad' } });
    send({ event: 'willAppear', action: P + 'volume', context: 'c2', payload: { settings: { target: 'music', kind: 'up', step: 10 }, controller: 'Keypad' } });
    send({ event: 'willAppear', action: P + 'volume', context: 'c3', payload: { settings: { target: 'overall' }, controller: 'Encoder' } });
    send({ event: 'willAppear', action: P + 'mode', context: 'c4', payload: { settings: { modeTitle: 'Batalha' }, controller: 'Keypad' } });
    send({ event: 'willAppear', action: P + 'element', context: 'c5', payload: { settings: { modeTitle: 'Batalha', elementId: 21, elementTitle: 'Espada' }, controller: 'Keypad' } });
    send({ event: 'willAppear', action: P + 'repeat', context: 'c6', payload: { settings: {}, controller: 'Keypad' } });
    await sleep(900);
    assert(hostOk, 'Host header must be localhost:PORT');
    assert(find('setState', m => m.context === 'c1').pop().payload.state === 1, 'playing -> state 1');
    assert(find('setTitle', m => m.context === 'c1').pop().payload.title === 'Song A');
    assert.strictEqual(find('setTitle', m => m.context === 'c2').pop().payload.title, 'Música\n+ 50');
    assert.strictEqual(find('setFeedback', m => m.context === 'c3').pop().payload.value, '60%');
    assert.strictEqual(find('setState', m => m.context === 'c4').pop().payload.state, 0);

    send({ event: 'keyDown', action: P + 'playpause', context: 'c1', payload: { settings: { showTitle: true } } });
    send({ event: 'keyDown', action: P + 'volume', context: 'c2', payload: { settings: { target: 'music', kind: 'up', step: 10 } } });
    send({ event: 'dialRotate', action: P + 'volume', context: 'c3', payload: { settings: { target: 'overall' }, ticks: -2 } });
    send({ event: 'keyDown', action: P + 'repeat', context: 'c6', payload: { settings: {} } });
    await sleep(500);
    send({ event: 'keyDown', action: P + 'element', context: 'c5', payload: { settings: { modeTitle: 'Batalha', elementId: 21 } } });
    await sleep(600);
    console.log('commands:', cmds.join(' | '));
    assert(cmds.includes('pause:'));
    assert(cmds.includes('vol_music:60'));
    assert(cmds.includes('vol_overall:54'));   // 60 - 2 ticks * step 3
    assert(cmds.includes('repeat:'));
    assert(cmds.indexOf('mode:1') >= 0 && cmds.indexOf('element:21') > cmds.indexOf('mode:1'), 'mode before element');

    send({ event: 'dialDown', action: P + 'volume', context: 'c3', payload: { settings: { target: 'overall' } } });
    await sleep(300);
    assert(cmds.includes('vol_overall:0'), 'mute');

    send({ event: 'sendToPlugin', action: P + 'mode', context: 'c4', payload: { event: 'getInfo' } });
    await sleep(300);
    const info = find('sendToPropertyInspector').pop();
    assert.strictEqual(info.payload.status, 'ok');
    assert.strictEqual(info.payload.project.length, 2);
    assert.strictEqual(info.payload.hasKey, true);

    send({ event: 'sendToPlugin', action: P + 'mode', context: 'c4', payload: { event: 'saveConnection', port: String(aport), key: 'wrong' } });
    await sleep(800);
    assert.strictEqual(find('sendToPropertyInspector').pop().payload.status, 'badkey');
    assert(find('setGlobalSettings').length === 1);
    console.log('ALL OK');
    child.kill(); process.exit(0);
})().catch(e => { console.error('FAIL', e.message); process.exit(1); });
