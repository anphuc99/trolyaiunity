const fs = require('fs');
const path = require('path');
const os = require('os');
const { execFileSync } = require('child_process');
const http = require('http');
const https = require('https');

const DEFAULT_SERVER_URL = process.env.GIT_BUNDLE_SERVER || 'http://localhost:5000/api/bundles';
const DEFAULT_REMOTE = process.env.GIT_REMOTE || 'origin';

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i += 1) {
    const token = argv[i];
    if (!token.startsWith('--')) continue;
    const [flag, inlineValue] = token.split('=');
    const key = flag.replace(/^--/, '');
    if (inlineValue !== undefined) {
      args[key] = inlineValue;
      continue;
    }
    const next = argv[i + 1];
    if (next && !next.startsWith('--')) {
      args[key] = next;
      i += 1;
    } else {
      args[key] = true;
    }
  }
  return args;
}

function runGit(args, options = {}) {
  return execFileSync('git', args, {
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe'],
    ...options,
  }).trim();
}

function ensureGitRepo() {
  try {
    runGit(['rev-parse', '--is-inside-work-tree']);
  } catch (error) {
    throw new Error('Not inside a git repository.');
  }
}

function createBundle(branch, outputDir, bundleNameOverride) {
  const sanitizedBranch = branch.replace(/[/\\]/g, '_');
  const bundleName = bundleNameOverride || `${sanitizedBranch}-${Date.now()}.bundle`;
  const bundlePath = path.join(outputDir, bundleName);
  runGit(['bundle', 'create', bundlePath, branch]);
  return bundlePath;
}

function buildFieldPart(boundary, name, value) {
  return `--${boundary}\r\nContent-Disposition: form-data; name="${name}"\r\n\r\n${value}\r\n`;
}

function buildFileHeader(boundary, name, filename) {
  return (
    `--${boundary}\r\n` +
    `Content-Disposition: form-data; name="${name}"; filename="${filename}"\r\n` +
    'Content-Type: application/octet-stream\r\n\r\n'
  );
}

function inferAuthorRepo(remoteUrl) {
  if (!remoteUrl) return {};
  const sanitized = remoteUrl.replace(/\.git$/, '');
  try {
    const parsed = new URL(sanitized);
    const segments = parsed.pathname.replace(/^\/+/, '').split('/').filter(Boolean);
    if (segments.length >= 2) {
      const repo = segments.pop();
      const author = segments.pop();
      return { author, repo };
    }
  } catch (error) {
    // fall through to handle scp-style remotes
  }

  let pathPart = sanitized;
  if (!sanitized.includes('://') && sanitized.includes(':')) {
    pathPart = sanitized.split(':').slice(-1)[0];
  }
  const parts = pathPart.replace(/^\/+/, '').split('/').filter(Boolean);
  if (parts.length >= 2) {
    const repo = parts.pop();
    const author = parts.pop();
    return { author, repo };
  }
  return {};
}

function uploadBundle({ bundlePath, serverUrl, remoteUrl, remoteName, branch, headSha, author, repo }) {
  return new Promise((resolve, reject) => {
    const url = new URL(serverUrl);
    const boundary = `----GitBundle${Date.now().toString(16)}${Math.random().toString(16).slice(2)}`;
    const protocol = url.protocol === 'https:' ? https : http;

    const options = {
      method: 'POST',
      hostname: url.hostname,
      port: url.port || (url.protocol === 'https:' ? 443 : 80),
      path: `${url.pathname}${url.search}` || '/',
      headers: {
        'Content-Type': `multipart/form-data; boundary=${boundary}`,
        'Transfer-Encoding': 'chunked',
      },
    };

    const req = protocol.request(options, (res) => {
      let body = '';
      res.setEncoding('utf8');
      res.on('data', (chunk) => {
        body += chunk;
      });
      res.on('end', () => {
        if (res.statusCode && res.statusCode >= 200 && res.statusCode < 300) {
          try {
            resolve(body ? JSON.parse(body) : {});
          } catch (err) {
            resolve({ raw: body });
          }
        } else {
          reject(new Error(`Server responded with ${res.statusCode}: ${body}`));
        }
      });
    });

    req.on('error', reject);

    const textFields = [
      ['branch', branch],
      ['remoteUrl', remoteUrl],
      ['headSha', headSha],
      ['author', author],
      ['repo', repo],
    ];

    if (remoteName) {
      textFields.push(['remote', remoteName]);
    }

    textFields.forEach(([name, value]) => {
      req.write(buildFieldPart(boundary, name, value));
    });

    const fileHeader = buildFileHeader(boundary, 'bundle', path.basename(bundlePath));
    req.write(fileHeader);

    const fileStream = fs.createReadStream(bundlePath);
    fileStream.on('error', (error) => {
      req.destroy(error);
      reject(error);
    });

    fileStream.on('end', () => {
      req.write(`\r\n--${boundary}--\r\n`);
      req.end();
    });

    fileStream.pipe(req, { end: false });
  });
}

function cleanupTempDir(dirPath) {
  fs.rmSync(dirPath, { recursive: true, force: true });
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const serverUrl = args.server || DEFAULT_SERVER_URL;
  const remoteName = args.remote || DEFAULT_REMOTE;

  ensureGitRepo();

  const branch = args.branch || runGit(['rev-parse', '--abbrev-ref', 'HEAD']);
  const headSha = runGit(['rev-parse', 'HEAD']);
  const remoteUrl = args.remoteUrl || runGit(['remote', 'get-url', remoteName]);
  const inferred = inferAuthorRepo(remoteUrl);
  const author = args.author || inferred.author;
  const repo = args.repo || inferred.repo;

  if (!author || !repo) {
    throw new Error('Unable to determine repository author/name. Provide --author and --repo.');
  }

  const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'git-bundle-'));
  try {
    const bundlePath = createBundle(branch, tempDir, args.bundleName);
    console.log(`Bundle created at ${bundlePath}`);
    const result = await uploadBundle({
      bundlePath,
      serverUrl,
      remoteUrl,
      remoteName,
      branch,
      headSha,
      author,
      repo,
    });
    console.log('Server response:', result);
  } finally {
    cleanupTempDir(tempDir);
  }
}

main().catch((error) => {
  console.error('Failed to push bundle:', error.message);
  process.exit(1);
});
