import {
  registerAppResource,
  registerAppTool,
  RESOURCE_MIME_TYPE,
} from "@modelcontextprotocol/ext-apps/server";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type {
  CallToolResult,
  ReadResourceResult,
} from "@modelcontextprotocol/sdk/types.js";
import fs from "node:fs/promises";
import path from "node:path";
import { z } from "zod";

// Works both from source (server.ts) and compiled (dist/server.js).
const DIST_DIR = import.meta.filename.endsWith(".ts")
  ? path.join(import.meta.dirname, "dist")
  : import.meta.dirname;

// Challenge-3 constants. The completion code is only ever shown inside the
// MCP Apps UI — the text fallback intentionally does NOT reveal it, so a player
// genuinely needs an MCP Apps-capable client to finish.
const COMPLETION_CODE = "1337 h4x0r";
const CONGRATS =
  "Congrats, you solved the Agentic Resource Discovery (ARD) challenge!";
const FALLBACK_TEXT = "For this one, you need MCP Apps...";

export function createServer(): McpServer {
  const server = new McpServer({ name: "challenge3-mcp", version: "1.0.0" });

  const resourceUri = "ui://challenge-three/award.html";

  registerAppTool(
    server,
    "reveal_challenge_three",
    {
      title: "Reveal Challenge Three",
      description:
        "Renders the ARD treasure-hunt award. Requires an MCP Apps-capable client.",
      inputSchema: {},
      outputSchema: z.object({ code: z.string(), message: z.string() }),
      _meta: { ui: { resourceUri } }, // links this tool to its UI resource
    },
    async (): Promise<CallToolResult> => ({
      // Text fallback for non-UI hosts (does not leak the code).
      content: [{ type: "text", text: FALLBACK_TEXT }],
      structuredContent: { code: COMPLETION_CODE, message: CONGRATS },
    }),
  );

  registerAppResource(
    server,
    resourceUri,
    resourceUri,
    { mimeType: RESOURCE_MIME_TYPE },
    async (): Promise<ReadResourceResult> => {
      const html = await fs.readFile(
        path.join(DIST_DIR, "award.html"),
        "utf-8",
      );
      return {
        contents: [
          { uri: resourceUri, mimeType: RESOURCE_MIME_TYPE, text: html },
        ],
      };
    },
  );

  return server;
}
