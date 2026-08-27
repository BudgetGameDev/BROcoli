'use strict';

const zlib = require('node:zlib');

const [pageUrl, debugPort = '9223', timeoutText = '120000'] = process.argv.slice(2);
const timeoutMs = Number(timeoutText);
const deadline = Date.now() + timeoutMs;

if (!pageUrl || !Number.isFinite(timeoutMs)) {
  console.error('usage: webgl-smoke.cjs URL [DEBUG_PORT] [TIMEOUT_MS]');
  process.exit(2);
}

const delay = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds));

function paeth(left, up, upperLeft) {
  const estimate = left + up - upperLeft;
  const leftDistance = Math.abs(estimate - left);
  const upDistance = Math.abs(estimate - up);
  const upperLeftDistance = Math.abs(estimate - upperLeft);
  if (leftDistance <= upDistance && leftDistance <= upperLeftDistance) return left;
  return upDistance <= upperLeftDistance ? up : upperLeft;
}

function inspectScreenshot(base64Png) {
  const png = Buffer.from(base64Png, 'base64');
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  if (!png.subarray(0, signature.length).equals(signature)) {
    throw new Error('Chrome returned an invalid PNG screenshot');
  }

  let offset = signature.length;
  let width;
  let height;
  let bitDepth;
  let colorType;
  let interlace;
  const imageChunks = [];

  while (offset < png.length) {
    const length = png.readUInt32BE(offset);
    const type = png.toString('ascii', offset + 4, offset + 8);
    const data = png.subarray(offset + 8, offset + 8 + length);
    offset += length + 12;

    if (type === 'IHDR') {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      bitDepth = data[8];
      colorType = data[9];
      interlace = data[12];
    } else if (type === 'IDAT') {
      imageChunks.push(data);
    } else if (type === 'IEND') {
      break;
    }
  }

  const channels = colorType === 2 ? 3 : colorType === 6 ? 4 : 0;
  if (!width || !height || bitDepth !== 8 || !channels || interlace !== 0) {
    throw new Error(
      `Unsupported Chrome screenshot format (${width}x${height}, depth=${bitDepth}, color=${colorType})`
    );
  }

  const packed = zlib.inflateSync(Buffer.concat(imageChunks));
  const stride = width * channels;
  let packedOffset = 0;
  let previous = Buffer.alloc(stride);
  let litPixels = 0;

  for (let y = 0; y < height; y++) {
    const filter = packed[packedOffset++];
    const current = Buffer.allocUnsafe(stride);

    for (let x = 0; x < stride; x++) {
      const source = packed[packedOffset++];
      const left = x >= channels ? current[x - channels] : 0;
      const up = previous[x];
      const upperLeft = x >= channels ? previous[x - channels] : 0;
      let value;

      if (filter === 0) value = source;
      else if (filter === 1) value = source + left;
      else if (filter === 2) value = source + up;
      else if (filter === 3) value = source + Math.floor((left + up) / 2);
      else if (filter === 4) value = source + paeth(left, up, upperLeft);
      else throw new Error(`Unsupported PNG filter ${filter}`);

      current[x] = value & 0xff;
    }

    for (let x = 0; x < stride; x += channels) {
      if (current[x] > 8 || current[x + 1] > 8 || current[x + 2] > 8) litPixels++;
    }
    previous = current;
  }

  return { width, height, litRatio: litPixels / (width * height) };
}

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
    await command('Page.enable');

    while (Date.now() < deadline) {
      const response = await command('Runtime.evaluate', {
        expression: `(() => ({
          state: document.body?.getAttribute('data-unity-state') || '',
          error: document.querySelector('.startup-error p')?.textContent || ''
        }))()`,
        returnByValue: true
      });
      const result = response.result?.value || {};

      if (result.state === 'started') {
        while (Date.now() < deadline) {
          await delay(500);
          const screenshot = await command('Page.captureScreenshot', {
            format: 'png',
            fromSurface: true
          });
          const visual = inspectScreenshot(screenshot.data);
          if (visual.litRatio >= 0.001) return { ...result, visual };
        }
        throw new Error('Unity started but Chrome kept presenting an all-black frame');
      }
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
  const result = await inspectUnityState(webSocketUrl);
  console.log(
    `webgl-smoke: Unity rendered a visible ${result.visual.width}x${result.visual.height} frame`
  );
})().catch((error) => {
  console.error(`webgl-smoke: ${error.message}`);
  process.exit(1);
});
