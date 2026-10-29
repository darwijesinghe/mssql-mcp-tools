#!/usr/bin/env node
'use strict';

/*
main
├─ 1. selectAsset
│    ├─ win32 / x64      mssql-mcp-tools-win-x64.zip       mssql-mcp-tools.exe
│    ├─ linux / x64      mssql-mcp-tools-linux-x64.tar.gz  mssql-mcp-tools
│    ├─ darwin / arm64   mssql-mcp-tools-osx-arm64.tar.gz  mssql-mcp-tools
│    ├─ darwin / x64     mssql-mcp-tools-osx-x64.tar.gz    mssql-mcp-tools
│    └─ otherwise        log this platform and the supported list, exit 1
├─ 2. installDirectory, then mkdir
│    └─ <home>/.mssql-mcp-tools/<RELEASE_TAG>   create the folder and any parents
├─ 3. ensureInstalled
│    ├─ binary already there   reuse it
│    └─ missing
│         ├─ download the GitHub Release asset, following redirects
│         │    status not ok, or no body -> throw
│         ├─ extract with tar   '-xf' for zip, '-xzf' for tar.gz
│         │    tar cannot start, or exits non-zero -> throw
│         ├─ finally   delete the archive only if that file exists
│         └─ extract returned and the binary is still absent -> throw
├─ 4. configIsReady
│    ├─ no appsettings.local.json
│    │  or 'YOUR_SERVER', 'YOUR_PASSWORD', or 'YOUR_LOG_PATH' is still in it
│    │  -> printSetup, exit 1
│    └─ file exists and all three placeholders are gone
└─ 5. startServer
     ├─ non-Windows   chmod 755
     ├─ spawn in the install folder, no args, stdio inherited
     │    spawn error -> stderr, exit 1
     ├─ forward the first SIGINT or SIGTERM; skip a later one
     └─ child exit
          ├─ signal -> re-raise that signal on this process
          └─ code -> exit with that code, or 1 when the code is null

thrown errors from main (mkdir, download, tar, missing binary, chmod) -> stderr, exit 1
*/

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawn, spawnSync } = require('child_process');
const { pipeline } = require('stream/promises');
const { Readable } = require('stream');

// Pinned to the GitHub Release that this package version installs
const RELEASE_TAG = 'v1.0.0';
const REPO = 'darwijesinghe/mssql-mcp-tools';

/**
 * Writes a single line to stderr so install progress stays off stdout.
 * @param {string} message Text to print.
 */
function log(message) {
  // Keep launcher messages on stderr so the server owns stdout
  process.stderr.write(`${message}\n`);
}

/**
 * Picks the GitHub Release archive and binary name for the current OS and CPU.
 * @returns {{ archive: string, binary: string } | null} Asset names, or null when the platform is unsupported.
 */
function selectAsset() {
  const { platform, arch } = process;

  // Windows x64 ships as a zip with an .exe
  if (platform === 'win32' && arch === 'x64') {
    return { archive: 'mssql-mcp-tools-win-x64.zip', binary: 'mssql-mcp-tools.exe' };
  }

  // Linux x64 ships as a gzip tarball
  if (platform === 'linux' && arch === 'x64') {
    return { archive: 'mssql-mcp-tools-linux-x64.tar.gz', binary: 'mssql-mcp-tools' };
  }

  // macOS Apple Silicon
  if (platform === 'darwin' && arch === 'arm64') {
    return { archive: 'mssql-mcp-tools-osx-arm64.tar.gz', binary: 'mssql-mcp-tools' };
  }

  // macOS Intel
  if (platform === 'darwin' && arch === 'x64') {
    return { archive: 'mssql-mcp-tools-osx-x64.tar.gz', binary: 'mssql-mcp-tools' };
  }

  // No published build for this OS and CPU
  return null;
}

/**
 * Returns the per-user folder where this release tag is installed.
 * @returns {string} Absolute path under the home directory.
 */
function installDirectory() {
  // One folder per release tag so upgrades do not overwrite an older install
  return path.join(os.homedir(), '.mssql-mcp-tools', RELEASE_TAG);
}

/**
 * Downloads a release asset to disk, following redirects.
 * @param {string} url Release asset URL.
 * @param {string} destination Local file path to write.
 * @returns {Promise<void>}
 */
async function download(url, destination) {
  // Follow GitHub's release redirect and request the raw file
  const response = await fetch(url, {
    redirect: 'follow',
    headers: {
      'User-Agent': 'mssql-mcp-tools',
      Accept: 'application/octet-stream'
    }
  });

  if (!response.ok || !response.body) {
    throw new Error(`Download failed (${response.status}) for ${url}`);
  }

  // Stream the body to disk instead of buffering the whole archive
  await pipeline(Readable.fromWeb(response.body), fs.createWriteStream(destination));
}

/**
 * Extracts a zip or tar.gz archive into the install folder using tar.
 * @param {string} archivePath Path of the downloaded archive.
 * @param {string} destination Directory to extract into.
 */
function extract(archivePath, destination) {
  // zip uses -xf; tar.gz also needs gzip (-xzf)
  const args = archivePath.endsWith('.zip')
    ? ['-xf', archivePath, '-C', destination]
    : ['-xzf', archivePath, '-C', destination];

  // Show tar errors on stderr and ignore its stdin
  const result = spawnSync('tar', args, { stdio: ['ignore', 'inherit', 'inherit'] });

  if (result.error) {
    throw new Error(`Could not run tar: ${result.error.message}`);
  }

  if (result.status !== 0) {
    throw new Error(`tar exited with code ${result.status}`);
  }
}

/**
 * Checks that appsettings.local.json exists and no longer contains placeholder values.
 * @param {string} directory Install directory that holds the config files.
 * @returns {boolean} True when a usable local config is present.
 */
function configIsReady(directory) {
  const localPath = path.join(directory, 'appsettings.local.json');

  // The shipped template is not enough; the user must copy it to the local file
  if (!fs.existsSync(localPath)) {
    return false;
  }

  const text = fs.readFileSync(localPath, 'utf8');
  // Still unfinished if 'YOUR_SERVER', 'YOUR_PASSWORD', or 'YOUR_LOG_PATH' remains
  return !text.includes('YOUR_SERVER')
    && !text.includes('YOUR_PASSWORD')
    && !text.includes('YOUR_LOG_PATH');
}

/**
 * Prints the steps required to create a local SQL connection config.
 * @param {string} directory Install directory where the config files live.
 */
function printSetup(directory) {
  log('mssql-mcp-tools needs a SQL connection string before it can start.');
  log('Copy appsettings.json to appsettings.local.json in this folder and replace the YOUR_* values:');
  // Print the folder so the user knows where to edit the file
  log(directory);
}

/**
 * Returns the installed binary, downloading and extracting the release when it is missing.
 * @param {string} directory Install directory for this release tag.
 * @param {{ archive: string, binary: string }} asset Archive and binary names for this platform.
 * @returns {Promise<string>} Absolute path of the server binary.
 */
async function ensureInstalled(directory, asset) {
  const binaryPath = path.join(directory, asset.binary);

  // Skip the download when this release is already unpacked
  if (fs.existsSync(binaryPath)) {
    return binaryPath;
  }

  const archivePath = path.join(directory, asset.archive);
  const url = `https://github.com/${REPO}/releases/download/${RELEASE_TAG}/${asset.archive}`;

  log(`Downloading ${RELEASE_TAG} ${asset.archive}...`);

  try {
    await download(url, archivePath);
    extract(archivePath, directory);
  } finally {
    // Remove the archive whether extract succeeded or failed
    if (fs.existsSync(archivePath)) {
      fs.rmSync(archivePath, { force: true });
    }
  }

  if (!fs.existsSync(binaryPath)) {
    throw new Error(`The release archive did not contain ${asset.binary}.`);
  }

  return binaryPath;
}

/**
 * Starts the server binary and forwards shutdown signals to the child process.
 * @param {string} binaryPath Absolute path of the server executable.
 * @param {string} directory Working directory so the server can read its config.
 */
function startServer(binaryPath, directory) {
  // Make sure the Unix binary can be executed after extract
  if (process.platform !== 'win32') {
    fs.chmodSync(binaryPath, 0o755);
  }

  // Share stdin, stdout, and stderr so MCP traffic passes through
  const child = spawn(binaryPath, [], { cwd: directory, stdio: 'inherit' });

  child.on('error', (error) => {
    log(error.message);
    process.exit(1);
  });

  /**
   * Relays the first termination signal to the server.
   * A later call is skipped once kill() has set child.killed.
   * @param {NodeJS.Signals} signal Signal received by this process.
   */
  const forward = (signal) => {
    // kill() sets child.killed; do not send a second signal
    if (!child.killed) {
      child.kill(signal);
    }
  };

  // Ctrl+C and service stop should shut the server down too
  process.on('SIGINT', () => forward('SIGINT'));
  process.on('SIGTERM', () => forward('SIGTERM'));

  child.on('exit', (code, signal) => {
    // Keep the child's signal or exit code as this process's result
    if (signal) {
      process.kill(process.pid, signal);
      return;
    }

    process.exit(code ?? 1);
  });
}

/**
 * Selects the platform asset, installs it if needed, then starts the server.
 * @returns {Promise<void>}
 */
async function main() {
  // Pick the release archive and binary for this OS and CPU
  const asset = selectAsset();

  if (!asset) {
    log(`Unsupported platform ${process.platform} ${process.arch}.`);
    log('Supported: Windows x64, Linux x64, macOS arm64, and macOS x64.');
    process.exit(1);
  }

  // Create the per-user install folder for this release tag
  const directory = installDirectory();
  fs.mkdirSync(directory, { recursive: true });

  // Reuse the installed binary, or download and extract the release
  const binaryPath = await ensureInstalled(directory, asset);

  // Stop until the local config drops 'YOUR_SERVER', 'YOUR_PASSWORD', and 'YOUR_LOG_PATH'
  if (!configIsReady(directory)) {
    printSetup(directory);
    process.exit(1);
  }

  // Run the server and forward shutdown signals to it
  startServer(binaryPath, directory);
}

// Print thrown install errors, then exit non-zero
main().catch((error) => {
  log(error && error.message ? error.message : String(error));
  process.exit(1);
});
