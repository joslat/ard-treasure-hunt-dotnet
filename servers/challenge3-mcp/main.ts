import express, { type Request, type Response } from "express";
import cors from "cors";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";
import { createServer } from "./server.js";

const app = express();
app.use(cors());
app.use(express.json());

// Streamable HTTP MCP endpoint (stateless). app.all so the transport handles
// POST (JSON-RPC) and any GET/DELETE itself.
app.all("/mcp", async (req: Request, res: Response) => {
  const server = createServer();
  const transport = new StreamableHTTPServerTransport({
    sessionIdGenerator: undefined,
  });

  res.on("close", () => {
    transport.close().catch(() => {});
    server.close().catch(() => {});
  });

  try {
    await server.connect(transport);
    await transport.handleRequest(req, res, req.body);
  } catch (error) {
    console.error("MCP error:", error);
    if (!res.headersSent) {
      res.status(500).json({
        jsonrpc: "2.0",
        error: { code: -32603, message: "Internal server error" },
        id: null,
      });
    }
  }
});

// Liveness probe for Azure App Service.
app.get("/healthz", (_req: Request, res: Response) => {
  res.status(200).send("ok");
});

const port = Number(process.env.PORT) || 3000;
app.listen(port, () => {
  console.log(`challenge3-mcp listening on :${port} (/mcp)`);
});
