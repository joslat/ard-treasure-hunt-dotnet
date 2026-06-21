import express, { type Request, type Response } from "express";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";

// ---------------------------------------------------------------------------
// Challenge config — the ONLY block that differs from challenge1-mcp.
// ---------------------------------------------------------------------------
const SERVER_NAME = "challenge2-mcp";
const SERVER_VERSION = "1.0.0";
const TOOL_NAME = "reveal_challenge_two";
const TOOL_TITLE = "Reveal Challenge Two";
const TOOL_DESCRIPTION =
  "Reveals the solution to the second ARD treasure-hunt challenge.";
const SOLUTION_TEXT =
  'Awesome! You solved the second challenge! Completion code: "Sean Astrakhan" (share this to prove you reached the MCP server). Hint: DNS again — but this time an SRV record pointing to a search endpoint...';

// ---------------------------------------------------------------------------
// MCP server factory. Stateless: a fresh server + transport is created per
// request so there is no session affinity to manage on Azure App Service.
// ---------------------------------------------------------------------------
function createMcpServer(): McpServer {
  const server = new McpServer({ name: SERVER_NAME, version: SERVER_VERSION });

  server.registerTool(
    TOOL_NAME,
    {
      title: TOOL_TITLE,
      description: TOOL_DESCRIPTION,
      inputSchema: {},
    },
    async () => ({
      content: [{ type: "text", text: SOLUTION_TEXT }],
    }),
  );

  return server;
}

const app = express();
app.use(express.json());

// Streamable HTTP MCP endpoint (stateless mode).
app.post("/mcp", async (req: Request, res: Response) => {
  const server = createMcpServer();
  const transport = new StreamableHTTPServerTransport({
    sessionIdGenerator: undefined,
  });

  res.on("close", () => {
    void transport.close();
    void server.close();
  });

  try {
    await server.connect(transport);
    await transport.handleRequest(req, res, req.body);
  } catch (err) {
    console.error("MCP request error:", err);
    if (!res.headersSent) {
      res.status(500).json({
        jsonrpc: "2.0",
        error: { code: -32603, message: "Internal server error" },
        id: null,
      });
    }
  }
});

// Stateless server: GET (SSE stream) and DELETE (session teardown) are unused.
function methodNotAllowed(_req: Request, res: Response): void {
  res.status(405).json({
    jsonrpc: "2.0",
    error: { code: -32000, message: "Method not allowed." },
    id: null,
  });
}
app.get("/mcp", methodNotAllowed);
app.delete("/mcp", methodNotAllowed);

// Liveness probe for Azure App Service.
app.get("/healthz", (_req: Request, res: Response) => {
  res.status(200).send("ok");
});

const port = Number(process.env.PORT) || 3000;
app.listen(port, () => {
  console.log(`${SERVER_NAME} listening on :${port} (POST /mcp)`);
});
