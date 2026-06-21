# challenge3-search (S4) — stub

HTTP **search service** for **Challenge 3**, targeted by the `_search._agents.nullpointer.se` SRV
record. Implements the ARD dynamic search endpoint.

**Contract:** `POST /search` (served at root path so the SRV `host:443` reaches it).

Request (per ARD §4.5):

```json
{ "query": { "text": "..." }, "federation": "none", "pageSize": 10 }
```

Response — returns the challenge-3 entry for **every** query (ignores `query.text`), score 100:

```json
{
  "results": [
    {
      "identifier": "urn:ai:nullpointer.se:server:challenge-three",
      "displayName": "ARD Treasure Hunt — Challenge 3",
      "type": "application/mcp-server+json",
      "url": "https://<storage>/cards/challenge3.mcp.json",
      "score": 100
    }
  ],
  "referrals": [],
  "pageToken": null
}
```

**To implement:** a tiny Express app (reuse S1's `package.json`/`tsconfig.json`) with a single
`POST /search` handler returning the constant payload above, plus `GET /healthz`.
