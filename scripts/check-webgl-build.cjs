'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const buildDirectory = path.resolve(process.argv[2] || 'build/WebGL');
const buildFilesDirectory = path.join(buildDirectory, 'Build');
const minimumInitialMemoryBytes = 256 * 1024 * 1024;

function findFiles(directory, suffix) {
  return fs.readdirSync(directory)
    .filter((filename) => filename.endsWith(suffix))
    .map((filename) => path.join(directory, filename));
}

function readUnsignedLeb128(bytes, cursor) {
  let value = 0;
  let shift = 0;
  while (true) {
    assert.ok(cursor.offset < bytes.length, 'Unexpected end of WASM file');
    const byte = bytes[cursor.offset++];
    value += (byte & 0x7f) * (2 ** shift);
    if ((byte & 0x80) === 0) return value;
    shift += 7;
    assert.ok(shift <= 49, 'Unsupported WASM integer width');
  }
}

function readInitialMemoryPages(wasmPath) {
  const bytes = fs.readFileSync(wasmPath);
  assert.ok(
    bytes.subarray(0, 8).equals(Buffer.from([0, 97, 115, 109, 1, 0, 0, 0])),
    `${path.basename(wasmPath)} is not an uncompressed WebAssembly module`
  );

  const cursor = { offset: 8 };
  while (cursor.offset < bytes.length) {
    const sectionId = bytes[cursor.offset++];
    const sectionSize = readUnsignedLeb128(bytes, cursor);
    const sectionEnd = cursor.offset + sectionSize;
    assert.ok(sectionEnd <= bytes.length, 'WASM section extends beyond the file');

    if (sectionId === 5) {
      const memoryCount = readUnsignedLeb128(bytes, cursor);
      assert.equal(memoryCount, 1, 'Unity player must declare exactly one WASM memory');
      const flags = readUnsignedLeb128(bytes, cursor);
      const initialPages = readUnsignedLeb128(bytes, cursor);
      if ((flags & 1) !== 0) {
        const maximumPages = readUnsignedLeb128(bytes, cursor);
        assert.ok(maximumPages >= initialPages, 'WASM maximum memory is below its initial memory');
      }
      return initialPages;
    }
    cursor.offset = sectionEnd;
  }

  assert.fail('Compiled Unity player does not contain a WASM memory section');
}

assert.ok(fs.existsSync(path.join(buildDirectory, 'index.html')), 'WebGL build is missing index.html');
assert.ok(fs.existsSync(buildFilesDirectory), 'WebGL build is missing its Build directory');
assert.equal(findFiles(buildFilesDirectory, '.loader.js').length, 1, 'Expected one Unity loader');

const wasmFiles = findFiles(buildFilesDirectory, '.wasm');
assert.equal(wasmFiles.length, 1, 'Expected one uncompressed Unity WASM module');
const initialMemoryBytes = readInitialMemoryPages(wasmFiles[0]) * 65536;
assert.ok(
  initialMemoryBytes >= minimumInitialMemoryBytes,
  `WASM starts with ${initialMemoryBytes / 1048576} MiB; expected at least 256 MiB`
);

console.log(`webgl-build: compiled WASM starts with ${initialMemoryBytes / 1048576} MiB`);
