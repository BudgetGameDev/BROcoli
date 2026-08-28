'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(
  path.join(__dirname, '..', 'Assets', 'WebGLTemplates', 'Custom', 'sw.js'),
  'utf8'
);

function createHarness({ cachedResponse = null, fetchResult, fetchError = null }) {
  const listeners = new Map();
  const fetchCalls = [];
  const timers = [];
  const cacheWrites = [];
  let abortControllers = 0;

  const cache = {
    addAll: async () => {},
    match: async () => null,
    put: async (request, response) => cacheWrites.push({ request, response })
  };
  const self = {
    registration: { scope: 'https://example.test/BROcoli/BranchStaging/' },
    location: { href: 'https://example.test/BROcoli/BranchStaging/sw.js' },
    clients: { claim: async () => {}, matchAll: async () => [] },
    skipWaiting: async () => {},
    addEventListener: (type, listener) => listeners.set(type, listener)
  };

  class TrackedAbortController extends AbortController {
    constructor() {
      super();
      abortControllers++;
    }
  }

  vm.runInNewContext(source, {
    self,
    caches: {
      match: async () => cachedResponse,
      open: async () => cache,
      keys: async () => [],
      delete: async () => true
    },
    fetch: async (request, options) => {
      fetchCalls.push({ request, options });
      if (fetchError) throw fetchError;
      return fetchResult;
    },
    AbortController: TrackedAbortController,
    Response,
    URL,
    Date,
    console: { log() {}, warn() {}, error() {} },
    setTimeout: (callback, milliseconds) => {
      timers.push({ callback, milliseconds });
      return timers.length;
    },
    clearTimeout() {}
  });

  return { listeners, fetchCalls, timers, cacheWrites, get abortControllers() {
    return abortControllers;
  } };
}

async function dispatchBuildFetch(harness, filename = 'game.wasm') {
  let responsePromise;
  const request = {
    method: 'GET',
    url: `https://example.test/BROcoli/BranchStaging/Build/${filename}`
  };
  harness.listeners.get('fetch')({
    request,
    respondWith(promise) {
      responsePromise = promise;
    }
  });
  assert.ok(responsePromise, 'Build requests must be handled by the service worker');
  return responsePromise;
}

(async () => {
  const networkResponse = new Response('wasm payload', { status: 200 });
  const network = createHarness({ fetchResult: networkResponse });
  const result = await dispatchBuildFetch(network);
  await Promise.resolve();

  assert.strictEqual(result, networkResponse);
  assert.equal(network.fetchCalls.length, 1);
  assert.equal(network.fetchCalls[0].options, undefined, 'Build fetches must not carry an abort signal');
  assert.equal(network.abortControllers, 0, 'Large Build downloads must not create a timeout controller');
  assert.equal(network.timers.length, 0, 'Large Build downloads must not have a fixed timeout');
  assert.equal(network.cacheWrites.length, 1, 'Successful Build downloads must be cached');

  const cachedResponse = new Response('cached payload', { status: 200 });
  const cached = createHarness({ cachedResponse, fetchResult: networkResponse });
  assert.strictEqual(await dispatchBuildFetch(cached, 'game.data'), cachedResponse);
  assert.equal(cached.fetchCalls.length, 0, 'Cached Build files must not be downloaded again');

  const failed = createHarness({ fetchError: new Error('offline') });
  const fallback = await dispatchBuildFetch(failed, 'game.framework.js');
  assert.equal(fallback.status, 503, 'An uncached offline Build request must fail explicitly');

  console.log('webgl service worker behavior: all cases passed');
})().catch((error) => {
  console.error(error);
  process.exit(1);
});
