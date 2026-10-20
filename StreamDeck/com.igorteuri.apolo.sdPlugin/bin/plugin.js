/*
 Apolo Stream Deck plugin - a fork of Ares (Copyright (c) Jörg Ruedenauer), distributed under the GPL.

 No dependencies: a tiny WebSocket client talks to the Stream Deck software, and plain HTTP calls
 talk to the local server of Apolo (menu Extras > Stream Deck).
*/
'use strict';

const net = require('net');
const http = require('http');
const crypto = require('crypto');

const PREFIX = 'com.igorteuri.apolo.';

// ---------------------------------------------------------------- WebSocket client (RFC 6455, enough for Stream Deck)

class WebSocketClient {
    constructor(port, handlers) {
        this.handlers = handlers;
        this.buffer = Buffer.alloc(0);
        this.upgraded = false;
        this.fragments = [];
        this.socket = net.connect(parseInt(port, 10), '127.0.0.1');
        const key = crypto.randomBytes(16).toString('base64');
        this.socket.on('connect', () => {
            this.socket.write('GET / HTTP/1.1\r\nHost: 127.0.0.1:' + port + '\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n' +
                'Sec-WebSocket-Key: ' + key + '\r\nSec-WebSocket-Version: 13\r\n\r\n');
        });
        this.socket.on('data', d => this.onData(d));
        this.socket.on('close', () => handlers.close && handlers.close());
        this.socket.on('error', () => { });
    }

    onData(data) {
        this.buffer = Buffer.concat([this.buffer, data]);
        if (!this.upgraded) {
            const end = this.buffer.indexOf('\r\n\r\n');
            if (end < 0)
                return;
            const head = this.buffer.slice(0, end).toString('latin1');
            this.buffer = this.buffer.slice(end + 4);
            if (!/^HTTP\/1\.1 101/.test(head)) {
                this.socket.destroy();
                return;
            }
            this.upgraded = true;
            this.handlers.open && this.handlers.open();
        }
        for (;;) {
            if (this.buffer.length < 2)
                return;
            const b0 = this.buffer[0];
            const b1 = this.buffer[1];
            const fin = (b0 & 0x80) !== 0;
            const opcode = b0 & 0x0f;
            const masked = (b1 & 0x80) !== 0;
            let len = b1 & 0x7f;
            let offset = 2;
            if (len === 126) {
                if (this.buffer.length < 4)
                    return;
                len = this.buffer.readUInt16BE(2);
                offset = 4;
            } else if (len === 127) {
                if (this.buffer.length < 10)
                    return;
                len = this.buffer.readUInt32BE(2) * 4294967296 + this.buffer.readUInt32BE(6);
                offset = 10;
            }
            let mask = null;
            if (masked) {
                if (this.buffer.length < offset + 4)
                    return;
                mask = this.buffer.slice(offset, offset + 4);
                offset += 4;
            }
            if (this.buffer.length < offset + len)
                return;
            let payload = Buffer.from(this.buffer.slice(offset, offset + len));
            this.buffer = this.buffer.slice(offset + len);
            if (mask) {
                for (let i = 0; i < payload.length; i++)
                    payload[i] ^= mask[i & 3];
            }
            if (opcode === 8) {
                this.socket.end();
                return;
            }
            if (opcode === 9) {
                this.write(10, payload);
                continue;
            }
            if (opcode === 10)
                continue;
            if (opcode === 1 || opcode === 2 || opcode === 0) {
                this.fragments.push(payload);
                if (fin) {
                    const text = Buffer.concat(this.fragments).toString('utf8');
                    this.fragments = [];
                    this.handlers.message && this.handlers.message(text);
                }
            }
        }
    }

    write(opcode, payload) {
        if (!this.socket || this.socket.destroyed)
            return;
        const len = payload.length;
        let head;
        if (len < 126) {
            head = Buffer.alloc(2);
            head[1] = 0x80 | len;
        } else if (len < 65536) {
            head = Buffer.alloc(4);
            head[1] = 0x80 | 126;
            head.writeUInt16BE(len, 2);
        } else {
            head = Buffer.alloc(10);
            head[1] = 0x80 | 127;
            head.writeUInt32BE(0, 2);
            head.writeUInt32BE(len, 6);
        }
        head[0] = 0x80 | opcode;
        const mask = crypto.randomBytes(4);
        const body = Buffer.from(payload);
        for (let i = 0; i < body.length; i++)
            body[i] ^= mask[i & 3];
        this.socket.write(Buffer.concat([head, mask, body]));
    }

    send(text) {
        this.write(1, Buffer.from(text, 'utf8'));
    }
}

// ---------------------------------------------------------------- Stream Deck

const args = {};
for (let i = 2; i + 1 < process.argv.length; i += 2)
    args[process.argv[i]] = process.argv[i + 1];

let ws = null;
const contexts = new Map(); // context -> { action, settings, controller, last: {} }
let globalSettings = { port: '8767', key: '' };

function sd(obj) {
    if (ws)
        ws.send(JSON.stringify(obj));
}

function log(message) {
    sd({ event: 'logMessage', payload: { message: String(message) } });
}

// ---------------------------------------------------------------- Apolo

const apolo = { state: null, status: 'start', token: null, busy: false };

function httpRequest(method, path, body, token) {
    return new Promise((resolve, reject) => {
        const data = body ? Buffer.from(body, 'utf8') : null;
        const port = parseInt(globalSettings.port, 10) || 8767;
        const headers = { Host: 'localhost:' + port };
        if (token)
            headers['X-Token'] = token;
        if (data) {
            headers['Content-Type'] = 'application/x-www-form-urlencoded';
            headers['Content-Length'] = data.length;
        }
        const req = http.request({ host: '127.0.0.1', port: port, method: method, path: path, headers: headers, timeout: 3000 }, res => {
            const chunks = [];
            res.on('data', c => chunks.push(c));
            res.on('end', () => resolve({ status: res.statusCode, body: Buffer.concat(chunks).toString('utf8') }));
        });
        req.on('timeout', () => req.destroy(new Error('timeout')));
        req.on('error', reject);
        if (data)
            req.write(data);
        req.end();
    });
}

async function login() {
    if (!globalSettings.key) {
        apolo.status = 'nokey';
        return false;
    }
    try {
        const r = await httpRequest('POST', '/api/login', 'password=' + encodeURIComponent(globalSettings.key));
        if (r.status === 200) {
            apolo.token = r.body.trim();
            return true;
        }
        apolo.status = r.status === 429 ? 'locked' : 'badkey';
    } catch (e) {
        apolo.status = 'offline';
    }
    apolo.token = null;
    return false;
}

// request with automatic login (and a second try when the token is no longer valid)
async function api(method, path, body) {
    for (let attempt = 0; attempt < 2; attempt++) {
        if (!apolo.token && !(await login()))
            return null;
        try {
            const r = await httpRequest(method, path, body, apolo.token);
            if (r.status === 401) {
                apolo.token = null;
                continue;
            }
            if (r.status === 200)
                return r.body;
            apolo.status = 'error';
            return null;
        } catch (e) {
            apolo.status = 'offline';
            return null;
        }
    }
    return null;
}

async function command(cmd, arg) {
    const text = await api('POST', '/api/cmd', 'cmd=' + encodeURIComponent(cmd) + '&arg=' + encodeURIComponent(arg === undefined ? '' : String(arg)));
    return text !== null;
}

async function poll() {
    if (apolo.busy || contexts.size === 0 || !globalSettings.key)
        return;
    apolo.busy = true;
    try {
        const text = await api('GET', '/api/state');
        if (text !== null) {
            try {
                apolo.state = JSON.parse(text);
                apolo.status = 'ok';
            } catch (e) {
                apolo.status = 'error';
            }
        } else {
            apolo.state = null;
        }
        refreshAll();
        if (apolo.state)
            loadLooks();
    } finally {
        apolo.busy = false;
    }
}


// ---------------------------------------------------------------- pictures and colors of the buttons (as set in the Apolo Player)

const looks = { byId: new Map(), loadedAt: 0, loading: false };
const iconCache = new Map(); // file name -> data URI ('' when it could not be loaded)

function httpBinary(path, token) {
    return new Promise((resolve, reject) => {
        const port = parseInt(globalSettings.port, 10) || 8767;
        const req = http.request({ host: '127.0.0.1', port: port, method: 'GET', path: path, headers: { Host: 'localhost:' + port, 'X-Token': token }, timeout: 3000 }, res => {
            const chunks = [];
            res.on('data', c => chunks.push(c));
            res.on('end', () => resolve({ status: res.statusCode, body: Buffer.concat(chunks) }));
        });
        req.on('timeout', () => req.destroy(new Error('timeout')));
        req.on('error', reject);
        req.end();
    });
}

async function loadIcon(name) {
    if (iconCache.has(name) || !apolo.token)
        return;
    iconCache.set(name, '');
    try {
        const r = await httpBinary('/api/icon?n=' + encodeURIComponent(name), apolo.token);
        if (r.status === 200 && r.body.length > 0)
            iconCache.set(name, 'data:image/png;base64,' + r.body.toString('base64'));
        else
            iconCache.delete(name);
    } catch (e) {
        iconCache.delete(name);
    }
    refreshAll();
}

async function loadLooks() {
    if (looks.loading || Date.now() - looks.loadedAt < 10000)
        return;
    looks.loading = true;
    try {
        const text = await api('GET', '/api/project');
        if (text !== null) {
            const map = new Map();
            for (const mode of JSON.parse(text)) {
                for (const e of (mode.elements || [])) {
                    if (e.img || e.color)
                        map.set(mode.title + '\u0000' + e.id, { img: e.img || '', color: e.color || '' });
                }
            }
            looks.byId = map;
            looks.loadedAt = Date.now();
            refreshAll();
        }
    } catch (e) {
        looks.loadedAt = Date.now();
    } finally {
        looks.loading = false;
    }
}

function findLook(modeTitle, elementId) {
    const exact = looks.byId.get((modeTitle || '') + '\u0000' + elementId);
    if (exact || modeTitle)
        return exact || null;
    for (const [key, value] of looks.byId) {
        if (key.endsWith('\u0000' + elementId))
            return value;
    }
    return null;
}

function xml(text) {
    return String(text).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&apos;' })[c]);
}

// key picture: color background, emoji / picture on top (the title of the key is drawn by Stream Deck over it), ring when playing
function keyImage(look, active) {
    const color = /^#[0-9A-Fa-f]{6}$/.test(look.color) ? look.color : '#1f2430';
    const icon = look.img ? iconCache.get(look.img) : '';
    let svg = '<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="144" height="144" viewBox="0 0 144 144">' +
        '<rect width="144" height="144" rx="14" fill="' + xml(color) + '"' + (look.color ? '' : ' fill-opacity="1"') + '/>';
    if (look.color)
        svg += '<rect width="144" height="144" rx="14" fill="#000" fill-opacity="0.25"/>';
    if (icon)
        svg += '<image x="32" y="14" width="80" height="80" xlink:href="' + icon + '" href="' + icon + '"/>';
    if (active)
        svg += '<rect x="4" y="4" width="136" height="136" rx="12" fill="none" stroke="#ffb627" stroke-width="8"/>';
    svg += '</svg>';
    return 'data:image/svg+xml;charset=utf8,' + encodeURIComponent(svg);
}

// ---------------------------------------------------------------- showing state on the keys

const VOLUME_LABELS = {
    pt: { overall: 'Geral', music: 'Música', sound: 'Sons' },
    en: { overall: 'Master', music: 'Music', sound: 'Sounds' }
};

function lang() {
    try {
        const info = JSON.parse(args['-info']);
        return /^pt/i.test(info.application.language) ? 'pt' : 'en';
    } catch (e) {
        return 'pt';
    }
}

function volumeKey(target) {
    return target === 'music' ? 'music' : (target === 'sound' ? 'sound' : 'overall');
}

function shortText(text, max) {
    text = text || '';
    return text.length > max ? text.substring(0, max - 1) + '…' : text;
}

function setIfChanged(ctx, entry, kind, value, send) {
    const key = kind;
    const text = JSON.stringify(value);
    if (entry.last[key] === text)
        return;
    entry.last[key] = text;
    send();
}

function refreshOne(ctx, entry) {
    const st = apolo.state;
    const settings = entry.settings || {};
    const name = entry.action.substring(PREFIX.length);
    const connected = st !== null;
    switch (name) {
        case 'playpause': {
            const on = connected && st.playing && !st.paused;
            setIfChanged(ctx, entry, 'state', on ? 1 : 0, () => sd({ event: 'setState', context: ctx, payload: { state: on ? 1 : 0 } }));
            if (settings.showTitle) {
                const title = connected && st.playing ? shortText(st.title, 16) : '';
                setIfChanged(ctx, entry, 'title', title, () => sd({ event: 'setTitle', context: ctx, payload: { title: title, target: 0 } }));
            }
            break;
        }
        case 'repeat': {
            const on = connected && !!st.repeat;
            setIfChanged(ctx, entry, 'state', on ? 1 : 0, () => sd({ event: 'setState', context: ctx, payload: { state: on ? 1 : 0 } }));
            break;
        }
        case 'volume': {
            const target = volumeKey(settings.target);
            const label = VOLUME_LABELS[lang()][target];
            const value = connected ? st.vol[target] : null;
            if (entry.controller === 'Encoder') {
                const fb = { title: label, value: value === null ? '—' : value + '%', indicator: { value: value === null ? 0 : value } };
                setIfChanged(ctx, entry, 'fb', fb, () => sd({ event: 'setFeedback', context: ctx, payload: fb }));
            } else {
                const kind = settings.kind || 'up';
                const sign = kind === 'up' ? '+' : (kind === 'down' ? '−' : (kind === 'mute' ? '🔇' : '='));
                const title = label + '\n' + sign + ' ' + (value === null ? '—' : value);
                setIfChanged(ctx, entry, 'title', title, () => sd({ event: 'setTitle', context: ctx, payload: { title: title, target: 0 } }));
            }
            break;
        }
        case 'mode': {
            const title = settings.modeTitle || '';
            const current = connected && st.mode >= 0 && st.modes[st.mode] === title;
            setIfChanged(ctx, entry, 'title', title, () => sd({ event: 'setTitle', context: ctx, payload: { title: shortText(title, 30), target: 0 } }));
            setIfChanged(ctx, entry, 'state', current ? 1 : 0, () => sd({ event: 'setState', context: ctx, payload: { state: current ? 1 : 0 } }));
            break;
        }
        case 'element': {
            const title = settings.elementTitle || '';
            let active = false;
            if (connected && st.elements) {
                if (Array.isArray(st.active)) {
                    // everything which plays, whatever the folder: a key of a button in a subfolder lights up too
                    active = st.active.includes(settings.elementId);
                } else {
                    const modeOk = !settings.modeTitle || (st.mode >= 0 && st.modes[st.mode] === settings.modeTitle);
                    if (modeOk)
                        active = st.elements.some(e => e.id === settings.elementId && e.active);
                }
            }
            setIfChanged(ctx, entry, 'title', title, () => sd({ event: 'setTitle', context: ctx, payload: { title: shortText(title, 30), target: 0 } }));
            // emoji / color of the button, as set in the Apolo Player
            const look = findLook(settings.modeTitle, settings.elementId);
            if (look && look.img)
                loadIcon(look.img);
            const picture = look && (look.color || (look.img && iconCache.get(look.img))) ? look : null;
            const stamp = picture ? JSON.stringify([picture, picture.img ? !!iconCache.get(picture.img) : false]) : '';
            setIfChanged(ctx, entry, 'look', stamp, () => {
                if (picture) {
                    sd({ event: 'setImage', context: ctx, payload: { image: keyImage(picture, false), target: 0, state: 0 } });
                    sd({ event: 'setImage', context: ctx, payload: { image: keyImage(picture, true), target: 0, state: 1 } });
                } else {
                    sd({ event: 'setImage', context: ctx, payload: { target: 0, state: 0 } });
                    sd({ event: 'setImage', context: ctx, payload: { target: 0, state: 1 } });
                }
            });
            setIfChanged(ctx, entry, 'state', active ? 1 : 0, () => sd({ event: 'setState', context: ctx, payload: { state: active ? 1 : 0 } }));
            break;
        }
    }
}

function refreshAll() {
    contexts.forEach((entry, ctx) => refreshOne(ctx, entry));
}

function alert(ctx) {
    sd({ event: 'showAlert', context: ctx });
}

// ---------------------------------------------------------------- actions

const mutedVolumes = {};

async function setVolume(target, value) {
    value = Math.max(0, Math.min(100, Math.round(value)));
    const ok = await command('vol_' + target, value);
    if (ok && apolo.state && apolo.state.vol)
        apolo.state.vol[target] = value; // shown at once, the next poll confirms it
    refreshAll();
    return ok;
}

async function changeVolume(ctx, entry, kind, ticks) {
    const settings = entry.settings || {};
    const target = volumeKey(settings.target);
    const step = parseInt(settings.step, 10) || (entry.controller === 'Encoder' ? 3 : 5);
    const current = apolo.state && apolo.state.vol ? apolo.state.vol[target] : null;
    if (current === null || current === undefined) {
        alert(ctx);
        return;
    }
    let ok = true;
    if (kind === 'up')
        ok = await setVolume(target, current + step * ticks);
    else if (kind === 'down')
        ok = await setVolume(target, current - step * ticks);
    else if (kind === 'set')
        ok = await setVolume(target, parseInt(settings.value, 10) || 0);
    else if (kind === 'mute') {
        if (current > 0) {
            mutedVolumes[target] = current;
            ok = await setVolume(target, 0);
        } else {
            ok = await setVolume(target, mutedVolumes[target] || 50);
        }
    }
    if (!ok)
        alert(ctx);
}

async function press(ctx, entry) {
    const name = entry.action.substring(PREFIX.length);
    const settings = entry.settings || {};
    const st = apolo.state;
    let ok = true;
    switch (name) {
        case 'playpause': ok = await command('pause'); break;
        case 'stop': ok = await command('stop'); break;
        case 'prev': ok = await command('prev'); break;
        case 'next': ok = await command('next'); break;
        case 'repeat': ok = await command('repeat'); break;
        case 'volume': await changeVolume(ctx, entry, settings.kind || 'up', 1); return;
        case 'mode': {
            const idx = st ? st.modes.indexOf(settings.modeTitle) : -1;
            ok = idx >= 0 && await command('mode', idx);
            break;
        }
        case 'element': {
            if (!settings.elementId && settings.elementId !== 0) { ok = false; break; }
            if (settings.modeTitle && st) {
                const idx = st.modes.indexOf(settings.modeTitle);
                if (idx < 0) { ok = false; break; }
                if (idx !== st.mode) {
                    ok = await command('mode', idx);
                    if (!ok)
                        break;
                    await new Promise(r => setTimeout(r, 150));
                }
            }
            ok = await command('element', settings.elementId);
            break;
        }
    }
    if (!ok)
        alert(ctx);
    else
        setTimeout(poll, 120);
}

// ---------------------------------------------------------------- property inspector

async function sendInfo(ctx) {
    let project = [];
    const text = await api('GET', '/api/project');
    if (text !== null) {
        try { project = JSON.parse(text); } catch (e) { project = []; }
    }
    sd({
        event: 'sendToPropertyInspector', context: ctx, action: contexts.has(ctx) ? contexts.get(ctx).action : undefined,
        payload: { status: apolo.status, port: globalSettings.port, hasKey: !!globalSettings.key, project: project }
    });
}

function onSendToPlugin(msg) {
    const p = msg.payload || {};
    if (p.event === 'getInfo') {
        sendInfo(msg.context);
    } else if (p.event === 'saveConnection') {
        globalSettings = { port: String(p.port || '8767'), key: p.key ? String(p.key) : globalSettings.key };
        sd({ event: 'setGlobalSettings', context: args['-pluginUUID'], payload: globalSettings });
        apolo.token = null;
        apolo.state = null;
        apolo.status = 'start';
        api('GET', '/api/state').then(() => sendInfo(msg.context));
    }
}

// ---------------------------------------------------------------- events from Stream Deck

function onMessage(text) {
    let msg;
    try { msg = JSON.parse(text); } catch (e) { return; }
    const payload = msg.payload || {};
    switch (msg.event) {
        case 'willAppear':
            contexts.set(msg.context, { action: msg.action, settings: payload.settings || {}, controller: payload.controller || 'Keypad', last: {} });
            refreshOne(msg.context, contexts.get(msg.context));
            poll();
            break;
        case 'willDisappear':
            contexts.delete(msg.context);
            break;
        case 'didReceiveSettings':
            if (contexts.has(msg.context)) {
                const entry = contexts.get(msg.context);
                entry.settings = payload.settings || {};
                entry.last = {};
                refreshOne(msg.context, entry);
            }
            break;
        case 'didReceiveGlobalSettings':
            if (payload.settings && (payload.settings.key !== undefined || payload.settings.port !== undefined)) {
                globalSettings = { port: String(payload.settings.port || '8767'), key: payload.settings.key || '' };
                apolo.token = null;
                poll();
            }
            break;
        case 'keyDown': {
            const entry = contexts.get(msg.context);
            if (entry) {
                entry.settings = payload.settings || entry.settings;
                press(msg.context, entry);
            }
            break;
        }
        case 'dialRotate': {
            const entry = contexts.get(msg.context);
            if (entry && entry.action === PREFIX + 'volume') {
                entry.settings = payload.settings || entry.settings;
                changeVolume(msg.context, entry, payload.ticks >= 0 ? 'up' : 'down', Math.abs(payload.ticks || 1));
            }
            break;
        }
        case 'dialDown':
        case 'touchTap': {
            const entry = contexts.get(msg.context);
            if (entry && entry.action === PREFIX + 'volume') {
                entry.settings = payload.settings || entry.settings;
                changeVolume(msg.context, entry, 'mute', 1);
            }
            break;
        }
        case 'sendToPlugin':
            onSendToPlugin(msg);
            break;
    }
}

ws = new WebSocketClient(args['-port'], {
    open: () => {
        ws.send(JSON.stringify({ event: args['-registerEvent'], uuid: args['-pluginUUID'] }));
        ws.send(JSON.stringify({ event: 'getGlobalSettings', context: args['-pluginUUID'] }));
        setInterval(poll, 500);
    },
    message: onMessage,
    close: () => process.exit(0)
});

process.on('uncaughtException', e => { try { log('error: ' + e.message); } catch (x) { } });
process.on('unhandledRejection', e => { try { log('error: ' + (e && e.message)); } catch (x) { } });
