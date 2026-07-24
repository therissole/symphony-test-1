import { spawn } from "node:child_process";
import { mkdir, writeFile } from "node:fs/promises";
import {
  createServer,
  request as createHttpRequest,
} from "node:http";
import { request as createHttpsRequest } from "node:https";
import { dirname } from "node:path";
import { fileURLToPath } from "node:url";

const port = process.env.PORT;
const apiBaseUrl = process.env.API_BASE_URL;

if (!port || !/^\d+$/.test(port)) {
  throw new Error("Aspire must provide a numeric PORT environment variable.");
}

if (!apiBaseUrl) {
  throw new Error("Aspire must provide the API_BASE_URL environment variable.");
}

const apiUrl = new URL(apiBaseUrl);
const allowedApiHosts = new Set(["localhost", "127.0.0.1", "[::1]"]);

if (
  !["http:", "https:"].includes(apiUrl.protocol) ||
  !allowedApiHosts.has(apiUrl.hostname)
) {
  throw new Error("API_BASE_URL must identify a loopback HTTP endpoint.");
}

const requestFor = (url) =>
  url.protocol === "https:" ? createHttpsRequest : createHttpRequest;

const developmentTlsOptions = (url) =>
  url.protocol === "https:" ? { rejectUnauthorized: false } : {};

const hopByHopHeaders = new Set([
  "connection",
  "host",
  "keep-alive",
  "proxy-authenticate",
  "proxy-authorization",
  "te",
  "trailer",
  "transfer-encoding",
  "upgrade",
]);

const withoutHopByHopHeaders = (headers) =>
  Object.fromEntries(
    Object.entries(headers).filter(
      ([name, value]) =>
        value !== undefined && !hopByHopHeaders.has(name.toLowerCase()),
    ),
  );

const corsHeadersFor = (incoming) => {
  const origin = incoming.headers.origin;

  if (!origin) {
    return {};
  }

  let originUrl;

  try {
    originUrl = new URL(origin);
  } catch {
    return null;
  }

  if (
    !["http:", "https:"].includes(originUrl.protocol) ||
    !allowedApiHosts.has(originUrl.hostname)
  ) {
    return null;
  }

  return {
    "access-control-allow-origin": origin,
    "access-control-allow-methods": "GET, POST, PUT, PATCH, DELETE, OPTIONS",
    "access-control-allow-headers":
      incoming.headers["access-control-request-headers"] ?? "content-type",
    "access-control-allow-private-network": "true",
    vary: "Origin",
  };
};

const bridge = createServer((incoming, outgoing) => {
  const requestTarget = incoming.url ?? "/";
  const corsHeaders = corsHeadersFor(incoming);

  if (corsHeaders === null) {
    outgoing.writeHead(403, { "content-type": "application/problem+json" });
    outgoing.end(
      JSON.stringify({
        title: "The request origin is not allowed by the local API bridge.",
        status: 403,
      }),
    );
    return;
  }

  if (!requestTarget.startsWith("/") || requestTarget.startsWith("//")) {
    outgoing.writeHead(400, {
      "content-type": "application/problem+json",
      ...corsHeaders,
    });
    outgoing.end(
      JSON.stringify({
        title: "The local API bridge requires an origin-form request target.",
        status: 400,
      }),
    );
    return;
  }

  if (incoming.method === "OPTIONS") {
    outgoing.writeHead(204, corsHeaders);
    outgoing.end();
    return;
  }

  const target = new URL(requestTarget, apiUrl);
  const upstream = requestFor(target)(
    target,
    {
      method: incoming.method,
      headers: withoutHopByHopHeaders(incoming.headers),
      ...developmentTlsOptions(target),
    },
    (response) => {
      outgoing.writeHead(
        response.statusCode ?? 502,
        {
          ...withoutHopByHopHeaders(response.headers),
          ...corsHeaders,
        },
      );
      response.pipe(outgoing);
    },
  );

  upstream.on("error", () => {
    if (!outgoing.headersSent) {
      outgoing.writeHead(502, {
        "content-type": "application/problem+json",
        ...corsHeaders,
      });
    }

    outgoing.end(
      JSON.stringify({
        title: "The local API could not be reached.",
        status: 502,
      }),
    );
  });

  incoming.pipe(upstream);
});

await new Promise((resolve, reject) => {
  const onError = (error) => reject(error);
  bridge.once("error", onError);
  bridge.listen(0, "127.0.0.1", () => {
    bridge.off("error", onError);
    resolve();
  });
});

const bridgeAddress = bridge.address();

if (!bridgeAddress || typeof bridgeAddress === "string") {
  throw new Error("The local API bridge did not expose a TCP port.");
}

const bridgeUrl = `http://127.0.0.1:${bridgeAddress.port}`;
const openApiUrl = new URL("/openapi/v1.json", apiUrl);

const openApi = await new Promise((resolve, reject) => {
  const request = requestFor(openApiUrl)(
    openApiUrl,
    developmentTlsOptions(openApiUrl),
    (response) => {
      const chunks = [];
      response.on("data", (chunk) => chunks.push(chunk));
      response.on("end", () => {
        if (
          !response.statusCode ||
          response.statusCode < 200 ||
          response.statusCode >= 300
        ) {
          reject(
            new Error(
              `OpenAPI request failed with HTTP ${response.statusCode ?? "unknown"}.`,
            ),
          );
          return;
        }

        try {
          resolve(JSON.parse(Buffer.concat(chunks).toString("utf8")));
        } catch (error) {
          reject(new Error(`OpenAPI response was not valid JSON: ${error.message}`));
        }
      });
    },
  );

  request.on("error", reject);
  request.end();
});

openApi.servers = [
  {
    url: bridgeUrl,
    description: "Local Aspire API",
  },
];

const generatedOpenApiPath = fileURLToPath(
  new URL("./.generated/openapi.json", import.meta.url),
);
await mkdir(dirname(generatedOpenApiPath), { recursive: true });
await writeFile(
  generatedOpenApiPath,
  `${JSON.stringify(openApi, null, 2)}\n`,
  "utf8",
);

const mintEntryPoint = fileURLToPath(
  new URL("./node_modules/mint/index.js", import.meta.url),
);

const mint = spawn(
  process.execPath,
  [mintEntryPoint, "dev", "--no-open", "--port", port],
  { stdio: "inherit" },
);

for (const signal of ["SIGINT", "SIGTERM"]) {
  process.once(signal, () => {
    bridge.close();
    mint.kill(signal);
  });
}

mint.on("exit", (code) => {
  bridge.close();
  process.exitCode = code ?? 1;
});
