'use strict';

const [pageUrl, debugPort = '9223', timeoutText = '120000'] = process.argv.slice(2);
const timeoutMs = Number(timeoutText);
const deadline = Date.now() + timeoutMs;

if (!pageUrl || !Number.isFinite(timeoutMs)) {
  console.error('usage: webgl-smoke.cjs URL [DEBUG_PORT] [TIMEOUT_MS]');
  process.exit(2);
}

const delay = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds));

async function findPageTarget() {
  const endpoint = `http://127.0.0.1:${debugPort}/json`;

  while (Date.now() < deadline) {
    try {
      const targets = await (await fetch(endpoint)).json();
      const target = targets.find(
        (candidate) => candidate.type === 'page' && candidate.url.startsWith(pageUrl)
      );
      if (target?.webSocketDebuggerUrl) return target.webSocketDebuggerUrl;
    } catch {
      // Chrome may still be opening its debugging endpoint.
    }
    await delay(200);
  }

  throw new Error('Chrome debugging endpoint did not expose the game page');
}

async function inspectUnityState(webSocketUrl) {
  const socket = new WebSocket(webSocketUrl);
  const pending = new Map();
  let nextId = 1;

  socket.addEventListener('message', (event) => {
    const message = JSON.parse(event.data);
    const handler = pending.get(message.id);
    if (!handler) return;
    pending.delete(message.id);
    if (message.error) handler.reject(new Error(message.error.message));
    else handler.resolve(message.result);
  });

  await new Promise((resolve, reject) => {
    socket.addEventListener('open', resolve, { once: true });
    socket.addEventListener('error', () => reject(new Error('Chrome debugging connection failed')), {
      once: true
    });
  });

  function command(method, params = {}) {
    return new Promise((resolve, reject) => {
      const id = nextId++;
      pending.set(id, { resolve, reject });
      socket.send(JSON.stringify({ id, method, params }));
    });
  }

  try {
    await command('Runtime.enable');

    while (Date.now() < deadline) {
      const response = await command('Runtime.evaluate', {
        expression: `(() => ({
          state: document.body?.getAttribute('data-unity-state') || '',
          error: document.querySelector('.startup-error p')?.textContent || ''
        }))()`,
        returnByValue: true
      });
      const result = response.result?.value || {};

      if (result.state === 'started') return result;
      if (result.state === 'failed') {
        throw new Error(result.error || 'Unity reported a failed startup state');
      }
      await delay(250);
    }
  } finally {
    socket.close();
  }

  throw new Error(`Unity did not start within ${timeoutMs}ms`);
}

(async () => {
  const webSocketUrl = await findPageTarget();
  await inspectUnityState(webSocketUrl);
  console.log('webgl-smoke: Unity reached the started state');
})().catch((error) => {
  console.error(`webgl-smoke: ${error.message}`);
  process.exit(1);
});
