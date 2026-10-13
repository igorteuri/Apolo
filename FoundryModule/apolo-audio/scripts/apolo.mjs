/**
 * Apolo - Áudio ao vivo (Foundry VTT)
 *
 * O Apolo (player de áudio para RPG de mesa) transmite o que está tocando por VDO.Ninja.
 * Este módulo mostra um botão discreto na tela de cada jogador; ao clicar, o navegador abre a
 * transmissão (num iframe invisível) e o jogador ouve a música e os efeitos junto com a mesa.
 *
 * Nada é enviado ao servidor do Foundry: o áudio vai direto do computador do mestre para os jogadores.
 */

const MODULE_ID = "apolo-audio";
const FRAME_ID = "apolo-audio-frame";
const WIDGET_ID = "apolo-audio-widget";
const VDO_ORIGIN = "https://vdo.ninja";

const state = {
  mode: "idle",            // idle | connecting | live
  frame: null,
  widget: null,
  userInteracted: false,
  manuallyStopped: false,
  volumeTimer: null,
  loudnessSeen: 0,
  connectTimer: null
};

const t = (key) => game.i18n.localize(`APOLO.${key}`);

/* ---------------------------------------------------------------- settings */

Hooks.once("init", () => {
  game.settings.register(MODULE_ID, "streamId", {
    name: "APOLO.Settings.StreamId.Name",
    hint: "APOLO.Settings.StreamId.Hint",
    scope: "world",
    config: true,
    restricted: true,
    type: String,
    default: "",
    onChange: () => {
      if (state.mode !== "idle") {
        disconnect();
        connect();
      }
      updateWidget();
    }
  });
  game.settings.register(MODULE_ID, "autoConnect", {
    name: "APOLO.Settings.AutoConnect.Name",
    hint: "APOLO.Settings.AutoConnect.Hint",
    scope: "client",
    config: true,
    type: Boolean,
    default: true
  });
  game.settings.register(MODULE_ID, "gmListens", {
    name: "APOLO.Settings.GMListens.Name",
    hint: "APOLO.Settings.GMListens.Hint",
    scope: "client",
    config: true,
    type: Boolean,
    default: false
  });
  game.settings.register(MODULE_ID, "resetPosition", {
    name: "APOLO.Settings.ResetPosition.Name",
    hint: "APOLO.Settings.ResetPosition.Hint",
    scope: "client",
    config: true,
    type: Boolean,
    default: false,
    onChange: async (value) => {
      if (!value) return;
      await game.settings.set(MODULE_ID, "position", null);
      await game.settings.set(MODULE_ID, "resetPosition", false);
      placeWidget();
    }
  });
  game.settings.register(MODULE_ID, "volume", { scope: "client", config: false, type: Number, default: 0.8 });
  game.settings.register(MODULE_ID, "muted", { scope: "client", config: false, type: Boolean, default: false });
  game.settings.register(MODULE_ID, "position", { scope: "client", config: false, type: Object, default: null });
});

/* ---------------------------------------------------------------- stream id */

/** Accepts the listener link ("https://vdo.ninja/?view=abc") or only the id ("abc"). */
function getStreamId() {
  let raw = String(game.settings.get(MODULE_ID, "streamId") ?? "").trim();
  if (!raw) return "";
  const match = raw.match(/[?&]view=([^&#\s]+)/i);
  if (match) raw = decodeURIComponent(match[1]);
  return raw.replace(/[^A-Za-z0-9_\-]/g, "");
}

function listenerUrl(id) {
  return `${VDO_ORIGIN}/?view=${encodeURIComponent(id)}&cleanoutput&novideo&label=Foundry`;
}

function mayListen() {
  if (!getStreamId()) return false;
  if (game.user.isGM && !game.settings.get(MODULE_ID, "gmListens")) return false;
  return true;
}

/* ---------------------------------------------------------------- connection */

function connect() {
  const id = getStreamId();
  if (!id) {
    ui.notifications.warn(t("NoStream"));
    return;
  }
  if (state.frame) return;
  state.manuallyStopped = false;
  state.loudnessSeen = 0;
  const frame = document.createElement("iframe");
  frame.id = FRAME_ID;
  frame.allow = "autoplay";
  frame.setAttribute("aria-hidden", "true");
  frame.tabIndex = -1;
  frame.addEventListener("load", () => {
    sendToFrame({ volume: currentVolume() });
    sendToFrame({ mute: !!game.settings.get(MODULE_ID, "muted") });
    sendToFrame({ getLoudness: true });
  });
  frame.src = listenerUrl(id);
  document.body.appendChild(frame);
  state.frame = frame;
  setMode("connecting");
  // the iframe has loaded -> consider it live (the sound itself shows through the pulsing dot)
  clearTimeout(state.connectTimer);
  state.connectTimer = setTimeout(() => {
    if (state.frame === frame) setMode("live");
  }, 2500);
  // some browsers forget the volume when the stream starts later: send it again from time to time
  clearInterval(state.volumeTimer);
  state.volumeTimer = setInterval(() => {
    sendToFrame({ volume: currentVolume() });
    sendToFrame({ mute: !!game.settings.get(MODULE_ID, "muted") });
    // no loudness message for a while -> quiet
    if (state.widget) state.widget.classList.toggle("apolo-sound", Date.now() - state.loudnessSeen < 2500);
  }, 3000);
}

function disconnect() {
  clearTimeout(state.connectTimer);
  clearInterval(state.volumeTimer);
  if (state.frame) {
    sendToFrame({ close: true });
    state.frame.remove();
    state.frame = null;
  }
  setMode("idle");
}

function sendToFrame(message) {
  try {
    state.frame?.contentWindow?.postMessage(message, VDO_ORIGIN);
  } catch (err) {
    /* the frame is not ready yet */
  }
}

function currentVolume() {
  return Math.max(0, Math.min(1, Number(game.settings.get(MODULE_ID, "volume")) || 0));
}

// loudness reports from VDO.Ninja make the little dot pulse while there is sound
window.addEventListener("message", (event) => {
  if (!state.frame || event.source !== state.frame.contentWindow) return;
  const data = event.data;
  if (!data || typeof data !== "object") return;
  if ("loudness" in data) {
    const values = typeof data.loudness === "object" && data.loudness !== null ? Object.values(data.loudness) : [data.loudness];
    const level = values.reduce((max, v) => Math.max(max, Number(typeof v === "object" && v ? v.loudness ?? 0 : v) || 0), 0);
    if (level > 1) state.loudnessSeen = Date.now();
    if (state.mode === "connecting") setMode("live");
    if (state.widget) state.widget.classList.toggle("apolo-sound", Date.now() - state.loudnessSeen < 2500);
  }
});

function setMode(mode) {
  state.mode = mode;
  updateWidget();
}

/* ---------------------------------------------------------------- widget */

function buildWidget() {
  const widget = document.createElement("div");
  widget.id = WIDGET_ID;
  widget.innerHTML = `
    <span class="apolo-grip" title="" data-apolo="grip"><i class="fa-solid fa-grip-vertical"></i></span>
    <button type="button" class="apolo-main" data-apolo="main">
      <span class="apolo-dot"></span>
      <i class="fa-solid fa-headphones"></i>
      <span class="apolo-label"></span>
    </button>
    <span class="apolo-controls">
      <button type="button" class="apolo-mute" data-apolo="mute" title="${t("Mute")}"><i class="fa-solid fa-volume-high"></i></button>
      <input type="range" class="apolo-vol" min="0" max="100" step="1" title="${t("Volume")}">
    </span>
    ${game.user.isGM ? `<button type="button" class="apolo-call" data-apolo="call" title="${t("CallAll")}"><i class="fa-solid fa-bullhorn"></i></button>` : ""}
  `;
  document.body.appendChild(widget);
  state.widget = widget;

  const slider = widget.querySelector(".apolo-vol");
  slider.value = Math.round(currentVolume() * 100);
  slider.addEventListener("input", async () => {
    const v = Number(slider.value) / 100;
    await game.settings.set(MODULE_ID, "volume", v);
    if (v > 0 && game.settings.get(MODULE_ID, "muted")) await game.settings.set(MODULE_ID, "muted", false);
    sendToFrame({ volume: v });
    sendToFrame({ mute: !!game.settings.get(MODULE_ID, "muted") });
    updateWidget();
  });

  widget.addEventListener("click", async (event) => {
    const target = event.target.closest("[data-apolo]");
    if (!target) return;
    const action = target.dataset.apolo;
    if (action === "main") {
      state.userInteracted = true;
      if (state.mode === "idle") connect();
      else {
        state.manuallyStopped = true;
        disconnect();
      }
    } else if (action === "mute") {
      await game.settings.set(MODULE_ID, "muted", !game.settings.get(MODULE_ID, "muted"));
      sendToFrame({ mute: !!game.settings.get(MODULE_ID, "muted") });
      updateWidget();
    } else if (action === "call") {
      game.socket.emit(`module.${MODULE_ID}`, { type: "listen" });
    }
  });

  enableDragging(widget);
  placeWidget();
  updateWidget();
}

function updateWidget() {
  const widget = state.widget;
  if (!widget) return;
  const hasStream = !!getStreamId();
  widget.style.display = hasStream || game.user.isGM ? "" : "none";
  widget.classList.toggle("apolo-live", state.mode === "live");
  widget.classList.toggle("apolo-connecting", state.mode === "connecting");
  if (state.mode === "idle") widget.classList.remove("apolo-sound");
  const label = widget.querySelector(".apolo-label");
  const main = widget.querySelector(".apolo-main");
  if (state.mode === "idle") {
    label.textContent = t("Listen");
    main.title = t("Connect");
  } else if (state.mode === "connecting") {
    label.textContent = t("Connecting");
    main.title = t("Disconnect");
  } else {
    label.textContent = t("Live");
    main.title = t("Disconnect");
  }
  const muted = !!game.settings.get(MODULE_ID, "muted") || currentVolume() === 0;
  const icon = widget.querySelector(".apolo-mute i");
  if (icon) icon.className = muted ? "fa-solid fa-volume-xmark" : "fa-solid fa-volume-high";
}

function placeWidget() {
  const widget = state.widget;
  if (!widget) return;
  const pos = game.settings.get(MODULE_ID, "position");
  if (pos && Number.isFinite(pos.left) && Number.isFinite(pos.top)) {
    widget.style.left = `${clamp(pos.left, 0, window.innerWidth - 60)}px`;
    widget.style.top = `${clamp(pos.top, 0, window.innerHeight - 30)}px`;
  } else {
    // default: bottom left, above the players list
    widget.style.left = "14px";
    widget.style.top = `${Math.max(10, window.innerHeight - 150)}px`;
  }
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

function enableDragging(widget) {
  const grip = widget.querySelector(".apolo-grip");
  grip.title = "";
  grip.addEventListener("pointerdown", (event) => {
    event.preventDefault();
    const rect = widget.getBoundingClientRect();
    const dx = event.clientX - rect.left;
    const dy = event.clientY - rect.top;
    const move = (e) => {
      widget.style.left = `${clamp(e.clientX - dx, 0, window.innerWidth - 60)}px`;
      widget.style.top = `${clamp(e.clientY - dy, 0, window.innerHeight - 30)}px`;
    };
    const up = async () => {
      window.removeEventListener("pointermove", move);
      window.removeEventListener("pointerup", up);
      const r = widget.getBoundingClientRect();
      await game.settings.set(MODULE_ID, "position", { left: Math.round(r.left), top: Math.round(r.top) });
    };
    window.addEventListener("pointermove", move);
    window.addEventListener("pointerup", up);
  });
  window.addEventListener("resize", placeWidget);
}

/* ---------------------------------------------------------------- start */

Hooks.once("ready", () => {
  buildWidget();

  // the browser only allows sound after a click: the first click anywhere starts the listening
  const firstClick = () => {
    state.userInteracted = true;
    if (game.settings.get(MODULE_ID, "autoConnect") && mayListen() && state.mode === "idle" && !state.manuallyStopped) {
      // do not hijack the click on the Apolo button itself
      setTimeout(() => {
        if (state.mode === "idle" && !state.manuallyStopped) connect();
      }, 50);
    }
  };
  document.addEventListener("pointerdown", firstClick, { once: true, capture: true });

  // the GM asked everybody to listen
  game.socket.on(`module.${MODULE_ID}`, (data) => {
    if (!data || data.type !== "listen") return;
    if (game.user.isGM && !game.settings.get(MODULE_ID, "gmListens")) return;
    if (!getStreamId()) return;
    ui.notifications.info(t("CalledByGM"));
    state.widget?.classList.add("apolo-called");
    setTimeout(() => state.widget?.classList.remove("apolo-called"), 4000);
    if (state.mode === "idle") {
      state.manuallyStopped = false;
      if (state.userInteracted) connect();
    }
  });
});
