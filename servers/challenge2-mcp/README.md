# challenge2-mcp (S2) — stub

Streamable-HTTP MCP server for **Challenge 2** (DNS TXT → manifest). One tool,
`reveal_challenge_two`, returning:

> `Awesome! You solved the second challenge! Hint: DNS again — but this time an SRV record pointing to a search endpoint...`

**To implement:** copy `servers/challenge1-mcp/` here and change only the *Challenge config* block
in `src/index.ts` (`SERVER_NAME=challenge2-mcp`, `TOOL_NAME=reveal_challenge_two`, solution text
above). Everything else (transport, deploy) is identical to S1.
