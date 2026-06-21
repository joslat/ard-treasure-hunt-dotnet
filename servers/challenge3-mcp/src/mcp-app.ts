/**
 * @file Challenge-3 award MCP App (vanilla JS).
 * Displays a trophy, the congrats message, a name field, and the completion code.
 */
import {
  App,
  applyDocumentTheme,
  applyHostFonts,
  applyHostStyleVariables,
  type McpUiHostContext,
} from "@modelcontextprotocol/ext-apps";
import type { CallToolResult } from "@modelcontextprotocol/sdk/types.js";
import "./award.css";

const headlineEl = document.getElementById("headline") as HTMLElement;
const codeEl = document.getElementById("completion-code") as HTMLInputElement;
const nameEl = document.getElementById("player-name") as HTMLInputElement;
const greetingEl = document.getElementById("player-greeting") as HTMLElement;
const copyBtn = document.getElementById("copy-code") as HTMLButtonElement;

function handleHostContextChanged(ctx: McpUiHostContext) {
  if (ctx.theme) applyDocumentTheme(ctx.theme);
  if (ctx.styles?.variables) applyHostStyleVariables(ctx.styles.variables);
  if (ctx.styles?.css?.fonts) applyHostFonts(ctx.styles.css.fonts);
}

// Personalised greeting as the player types their name.
nameEl.addEventListener("input", () => {
  const name = nameEl.value.trim();
  greetingEl.textContent = name ? `Nice work, ${name}! 🎉` : "";
});

// Copy the completion code to the clipboard.
copyBtn.addEventListener("click", async () => {
  try {
    await navigator.clipboard.writeText(codeEl.value);
    const prev = copyBtn.textContent;
    copyBtn.textContent = "Copied!";
    setTimeout(() => (copyBtn.textContent = prev), 1200);
  } catch {
    codeEl.select();
  }
});

const app = new App({ name: "ARD Award", version: "1.0.0" });

// Register handlers BEFORE connecting.
app.onerror = console.error;
app.onhostcontextchanged = handleHostContextChanged;
app.onteardown = async () => ({});

// The server is the source of truth for the code + message.
app.ontoolresult = (result: CallToolResult) => {
  const sc = result.structuredContent as
    | { code?: string; message?: string }
    | undefined;
  if (sc?.code) codeEl.value = sc.code;
  if (sc?.message) headlineEl.textContent = sc.message;
};

// Connect to the host, then apply initial theme.
app.connect().then(() => {
  const ctx = app.getHostContext();
  if (ctx) handleHostContextChanged(ctx);
});
