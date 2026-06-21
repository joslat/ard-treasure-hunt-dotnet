import express, { type Request, type Response } from "express";

// ---------------------------------------------------------------------------
// ARD dynamic search endpoint (spec §4.5). Targeted by the
// _search._agents.nullpointer.se SRV record. For ANY query it returns the
// challenge-3 MCP server card (score 100) — the query text is ignored.
// ---------------------------------------------------------------------------
// Self-host tweak (José/.NET toolkit): the result card URL is overridable via env
// so the same service works against your own infra; defaults to Andreas's original.
const CARD3_URL =
  process.env.CARD3_URL ??
  "https://nptreasurehuntstorage.blob.core.windows.net/ardchallenge/cards/52409da636c0425fb58c9140eb7f87e6.mcp.json";

const SEARCH_RESPONSE = {
  results: [
    {
      identifier: "urn:ai:nullpointer.se:server:challenge-three",
      displayName: "ARD Treasure Hunt — Challenge 3",
      type: "application/mcp-server+json",
      url: CARD3_URL,
      score: 100,
    },
  ],
  referrals: [],
  pageToken: null,
};

const app = express();
app.use(express.json());

// POST /search at the root path so the SRV target (host:443) reaches it directly.
app.post("/search", (_req: Request, res: Response) => {
  res.json(SEARCH_RESPONSE);
});

// Liveness probe for Azure App Service.
app.get("/healthz", (_req: Request, res: Response) => {
  res.status(200).send("ok");
});

const port = Number(process.env.PORT) || 3000;
app.listen(port, () => {
  console.log(`challenge3-search listening on :${port} (POST /search)`);
});
