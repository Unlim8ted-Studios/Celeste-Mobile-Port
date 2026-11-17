function validateGameData(files) {
  let expected = 0;
  const names = new Set();
  for (const file of files) {
    if (!file.filename.startsWith('/libsdl/Content/') || file.filename.includes('..') || file.filename.includes('\\') ||
        !Number.isSafeInteger(file.start) || !Number.isSafeInteger(file.end) || file.start !== expected || file.end < file.start ||
        names.has(file.filename)) throw new Error('Invalid game data metadata');
    names.add(file.filename);
    expected = file.end;
  }
  if (!expected) throw new Error('Empty game data metadata');
  return expected;
}

window.mountGameData = async function(activeModule, options = {}) {
  if (typeof activeModule.celesteMountContentArchive !== 'function') return false;
  const files = options.metadata || package_metadata;
  const total = validateGameData(files);
  const url = options.url || window.assetblob || '_framework/data/data.data';
  try {
    await activeModule.celesteMountContentArchive(files, new URL(url, location.href).href, total, options.onProgress);
    console.info('[game-data] mounted installed game archive in shared memory');
    return true;
  } catch (error) {
    console.warn('[game-data] direct archive access unavailable; using streaming loader', error);
    return false;
  }
};

// Fallback for runtimes without shared archive support.
window.loadData = async function(activeModule, onComplete, options = {}) {
  const FS = activeModule.FS;
  const files = options.metadata || package_metadata;
  const url = options.url || window.assetblob || '_framework/data/data.data';
  const timeoutMs = options.timeoutMs || 60000;
  const controller = new AbortController();
  let reader, dependency = false, currentFile = '', installed = 0;
  let chunk = new Uint8Array(0), cursor = 0, lastReport = 0, lastLog = 0, lastYield = 0;
  const total = files.length ? files[files.length - 1].end : 0;
  const withTimeout = async operation => {
    let timer;
    try {
      return await Promise.race([operation, new Promise((_, reject) => {
        timer = setTimeout(() => {
          controller.abort();
          reject(new Error('No game data received for ' + timeoutMs / 1000 + ' seconds'));
        }, timeoutMs);
      })]);
    } finally { clearTimeout(timer); }
  };
  function report(force) {
    const now = Date.now();
    if (force || now - lastReport >= 200) {
      lastReport = now;
      options.onProgress?.(installed, total, currentFile);
    }
    if (force || now - lastLog >= 5000) {
      lastLog = now;
      console.info('[game-data] installed ' + installed + '/' + total + ' bytes; ' + currentFile);
    }
  }
  async function nextChunk() {
    const result = await withTimeout(reader.read());
    chunk = result.value || new Uint8Array(0); cursor = 0;
    return !result.done;
  }
  try {
    validateGameData(files);
    activeModule.addRunDependency('datafile_data.data'); dependency = true;
    console.info('[game-data] streaming ' + total + ' bytes into WasmFS');
    const response = await withTimeout(fetch(url, { signal: controller.signal, cache: 'no-store' }));
    if (!response.ok) throw new Error('Game data HTTP ' + response.status);
    if (!response.body?.getReader) throw new Error('Streaming game data is not supported by this browser');
    reader = response.body.getReader();
    const directories = new Set();
    for (const file of files) {
      currentFile = file.filename;
      const directory = currentFile.slice(0, currentFile.lastIndexOf('/'));
      if (!directories.has(directory)) { FS.mkdirTree(directory); directories.add(directory); }
      const stream = FS.open(currentFile, 'w');
      try {
        while (installed < file.end) {
          if (cursor === chunk.length) {
            if (!await nextChunk()) throw new Error('Truncated game data archive');
            if (!chunk.length) continue;
          }
          const count = Math.min(chunk.length - cursor, file.end - installed, 65536);
          // WasmFS takes a BigInt for positioned writes. Sequential writes use
          // the stream's current offset and work across both WasmFS and MEMFS.
          const written = FS.write(stream, chunk, cursor, count);
          if (!Number.isInteger(written) || written <= 0 || written > count) throw new Error('Game data write made no progress');
          cursor += written; installed += written;
          report(false);
          if (Date.now() - lastYield >= 32) {
            await new Promise(resolve => setTimeout(resolve, 0));
            lastYield = Date.now();
          }
        }
      } finally { FS.close(stream); }
    }
    if (cursor < chunk.length) throw new Error('Oversized game data archive');
    while (await nextChunk()) if (chunk.length) throw new Error('Oversized game data archive');
    report(true);
    console.info('[game-data] content installation complete');
  } catch (error) {
    const failure = new Error('Game data installation failed at ' + installed + '/' + total +
      ' bytes' + (currentFile ? ' (' + currentFile + ')' : '') + ': ' + (error.message || error));
    console.error('[game-data]', failure);
    throw failure;
  } finally {
    controller.abort();
    if (reader) { reader.cancel().catch(() => {}); reader.releaseLock(); }
    if (dependency) activeModule.removeRunDependency('datafile_data.data');
  }
  onComplete?.();
};
