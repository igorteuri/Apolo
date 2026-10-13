/*
 Apolo - a fork of Ares (Copyright (c) Jörg Ruedenauer), distributed under the GPL.

 Discord bridge: started by Apolo, connects back to Apolo through a local TCP socket, logs in with the
 bot token which Apolo sends, joins a voice channel and plays the PCM audio (48 kHz, stereo, 16 bit)
 which Apolo captures from itself.

 Frames on the socket:  [1 byte type][4 bytes length, big endian][payload]
   type 1: raw PCM, Apolo -> bridge
   type 2: JSON command, Apolo -> bridge
   type 3: JSON event, bridge -> Apolo
 The token is only sent through this local socket; it is never written to disk or to the console.
*/
'use strict';

const net = require('net');
const { Readable } = require('stream');

const port = parseInt(process.argv[2], 10);
const secret = process.argv[3] || '';

let discord, voice;
try {
    discord = require('discord.js');
    voice = require('@discordjs/voice');
} catch (e) {
    // report below, once the socket is up
    discord = null;
}

const FRAME_BYTES = 3840;      // 20 ms of 48 kHz stereo 16 bit
const MAX_QUEUE_BYTES = FRAME_BYTES * 20;   // 400 ms
const KEEP_QUEUE_BYTES = FRAME_BYTES * 5;   // 100 ms

let socket = null;
let client = null;
let connection = null;
let player = null;
let feed = null;
let pumpTimer = null;
let queue = [];
let queued = 0;
let joined = { guildId: null, channelId: null };

function send(obj) {
    if (!socket || socket.destroyed)
        return;
    const body = Buffer.from(JSON.stringify(obj), 'utf8');
    const head = Buffer.alloc(5);
    head[0] = 3;
    head.writeUInt32BE(body.length, 1);
    socket.write(Buffer.concat([head, body]));
}

function describeError(e) {
    if (!e)
        return 'unknown error';
    return (e.message || String(e)).replace(/[A-Za-z0-9_\-]{24,}\.[A-Za-z0-9_\-]{6,}\.[A-Za-z0-9_\-]{20,}/g, '***');
}

// ---------- audio feed: a steady clock of 20 ms frames, silence when nothing arrived ----------

function takeFrame() {
    const out = Buffer.alloc(FRAME_BYTES);
    let filled = 0;
    while (filled < FRAME_BYTES && queue.length > 0) {
        const chunk = queue[0];
        const need = FRAME_BYTES - filled;
        if (chunk.length <= need) {
            chunk.copy(out, filled);
            filled += chunk.length;
            queue.shift();
        } else {
            chunk.copy(out, filled, 0, need);
            queue[0] = chunk.subarray(need);
            filled += need;
        }
    }
    queued -= filled;
    if (queued < 0)
        queued = 0;
    return out; // the rest stays zero = silence
}

function startFeed() {
    stopFeed();
    feed = new Readable({ read() { } });
    let next = Date.now();
    const tick = () => {
        const now = Date.now();
        let guard = 0;
        while (now >= next && guard < 6) {
            feed.push(takeFrame());
            next += 20;
            ++guard;
        }
        if (now - next > 200)
            next = now; // we were stalled for a long time: do not try to catch up
        pumpTimer = setTimeout(tick, Math.max(1, next - Date.now()));
    };
    pumpTimer = setTimeout(tick, 20);
    return feed;
}

function stopFeed() {
    if (pumpTimer) {
        clearTimeout(pumpTimer);
        pumpTimer = null;
    }
    if (feed) {
        feed.destroy();
        feed = null;
    }
    queue = [];
    queued = 0;
}

function onPcm(buf) {
    if (!connection)
        return;
    queue.push(buf);
    queued += buf.length;
    if (queued > MAX_QUEUE_BYTES) {
        // we are behind: drop the oldest audio so the delay stays small
        while (queued > KEEP_QUEUE_BYTES && queue.length > 1)
            queued -= queue.shift().length;
    }
}

// ---------- Discord ----------

function listGuilds() {
    const guilds = [];
    client.guilds.cache.forEach(g => {
        const channels = [];
        g.channels.cache.forEach(c => {
            if (c.type === discord.ChannelType.GuildVoice || c.type === discord.ChannelType.GuildStageVoice)
                channels.push({ id: c.id, name: c.name, position: c.rawPosition || 0 });
        });
        channels.sort((a, b) => a.position - b.position);
        guilds.push({ id: g.id, name: g.name, channels: channels.map(c => ({ id: c.id, name: c.name })) });
    });
    guilds.sort((a, b) => a.name.localeCompare(b.name));
    return guilds;
}

async function login(token) {
    if (client) {
        try { client.destroy(); } catch (e) { }
        client = null;
    }
    client = new discord.Client({ intents: [discord.GatewayIntentBits.Guilds, discord.GatewayIntentBits.GuildVoiceStates] });
    client.on('error', e => send({ ev: 'log', message: describeError(e) }));
    const ready = new Promise((resolve, reject) => {
        client.once('ready', () => resolve());
        client.login(token).catch(reject);
    });
    await ready;
    send({
        ev: 'ready',
        user: client.user.username,
        appId: client.application ? client.application.id : client.user.id,
        guilds: listGuilds()
    });
}

async function join(guildId, channelId) {
    if (!client)
        throw new Error('not logged in');
    leave(false);
    const guild = client.guilds.cache.get(guildId);
    if (!guild)
        throw new Error('server not found');
    const channel = guild.channels.cache.get(channelId);
    if (!channel)
        throw new Error('channel not found');

    connection = voice.joinVoiceChannel({
        channelId: channelId,
        guildId: guildId,
        adapterCreator: guild.voiceAdapterCreator,
        selfDeaf: true,
        selfMute: false
    });
    const conn = connection;
    conn.on('stateChange', (o, n) => send({ ev: 'log', message: 'voice ' + o.status + ' -> ' + n.status }));
    conn.on('error', e => send({ ev: 'log', message: 'voice error: ' + describeError(e) }));
    conn.on(voice.VoiceConnectionStatus.Disconnected, async () => {
        try {
            await Promise.race([
                voice.entersState(conn, voice.VoiceConnectionStatus.Signalling, 5000),
                voice.entersState(conn, voice.VoiceConnectionStatus.Connecting, 5000)
            ]);
            // reconnecting by itself (the bot was moved)
        } catch (e) {
            if (connection === conn) {
                leave(false);
                send({ ev: 'voice', state: 'left', message: 'disconnected' });
            }
        }
    });

    try {
        await voice.entersState(conn, voice.VoiceConnectionStatus.Ready, 25000);
    } catch (e) {
        leave(false);
        throw new Error('could not connect to the voice channel (' + describeError(e) + ')');
    }

    player = voice.createAudioPlayer({ behaviors: { noSubscriber: voice.NoSubscriberBehavior.Play } });
    player.on('error', e => send({ ev: 'log', message: 'player error: ' + describeError(e) }));
    conn.subscribe(player);
    const resource = voice.createAudioResource(startFeed(), { inputType: voice.StreamType.Raw });
    player.play(resource);
    joined = { guildId: guildId, channelId: channelId };
    send({ ev: 'voice', state: 'joined', guildId: guildId, channelId: channelId, guild: guild.name, channel: channel.name });
}

function leave(report) {
    stopFeed();
    if (player) {
        try { player.stop(true); } catch (e) { }
        player = null;
    }
    if (connection) {
        try { connection.destroy(); } catch (e) { }
        connection = null;
    }
    joined = { guildId: null, channelId: null };
    if (report)
        send({ ev: 'voice', state: 'left', message: '' });
}

async function handle(cmd) {
    try {
        switch (cmd.cmd) {
            case 'login':
                await login(cmd.token);
                break;
            case 'refresh':
                if (client)
                    send({ ev: 'guilds', guilds: listGuilds() });
                break;
            case 'join':
                await join(cmd.guildId, cmd.channelId);
                break;
            case 'leave':
                leave(true);
                break;
            case 'logout':
                leave(false);
                if (client) {
                    try { client.destroy(); } catch (e) { }
                    client = null;
                }
                send({ ev: 'loggedOut' });
                break;
            case 'quit':
                shutdown();
                break;
        }
    } catch (e) {
        let msg = describeError(e);
        if (cmd.cmd === 'login') {
            if (/disallowed intents/i.test(msg))
                msg = 'disallowed intents';
            send({ ev: 'loginError', message: msg });
            if (client) {
                try { client.destroy(); } catch (e2) { }
                client = null;
            }
        } else if (cmd.cmd === 'join') {
            send({ ev: 'voice', state: 'error', message: msg });
        } else {
            send({ ev: 'log', message: msg });
        }
    }
}

function shutdown() {
    try { leave(false); } catch (e) { }
    try { if (client) client.destroy(); } catch (e) { }
    setTimeout(() => process.exit(0), 100);
}

// ---------- socket ----------

let pending = Buffer.alloc(0);

function onData(chunk) {
    pending = pending.length ? Buffer.concat([pending, chunk]) : chunk;
    while (pending.length >= 5) {
        const type = pending[0];
        const len = pending.readUInt32BE(1);
        if (pending.length < 5 + len)
            break;
        const payload = pending.subarray(5, 5 + len);
        pending = pending.subarray(5 + len);
        if (type === 1) {
            onPcm(Buffer.from(payload));
        } else if (type === 2) {
            let cmd = null;
            try { cmd = JSON.parse(payload.toString('utf8')); } catch (e) { }
            if (cmd)
                handle(cmd);
        }
    }
}

process.on('uncaughtException', e => send({ ev: 'log', message: 'error: ' + describeError(e) }));
process.on('unhandledRejection', e => send({ ev: 'log', message: 'error: ' + describeError(e) }));

socket = net.connect(port, '127.0.0.1', () => {
    socket.setNoDelay(true);
    send({ ev: 'hello', secret: secret, node: process.version, ok: discord !== null });
    if (discord === null)
        send({ ev: 'fatal', message: 'libraries missing' });
});
socket.on('data', onData);
socket.on('close', () => shutdown());
socket.on('error', () => shutdown());
