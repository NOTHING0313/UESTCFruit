import fs from "node:fs/promises";
import path from "node:path";
import OpenAI from "openai";
import { fetch as undiciFetch, ProxyAgent } from "undici";

const apiKey = process.env.OPENAI_API_KEY;
const proxyUrl = process.env.OPENAI_PROXY_URL;

if (!apiKey) {
  throw new Error("OPENAI_API_KEY is not set.");
}

const dispatcher = proxyUrl ? new ProxyAgent(proxyUrl) : undefined;

const fetchWithProxy = dispatcher
  ? async (url, init = {}) => {
      return undiciFetch(url, {
        ...init,
        dispatcher
      });
    }
  : undefined;

const client = new OpenAI({
  apiKey,
  timeout: 900_000,
  maxRetries: 1,
  ...(fetchWithProxy ? { fetch: fetchWithProxy } : {})
});

const outputDir = process.env.IMAGE_MCP_OUTPUT_ROOT
  ? path.join(process.env.IMAGE_MCP_OUTPUT_ROOT, "Test")
  : path.resolve("./generated/Test");

await fs.mkdir(outputDir, { recursive: true });

console.log("Proxy:", proxyUrl ?? "none");
console.log("Start image generation...");

const result = await client.images.generate({
  model: process.env.OPENAI_IMAGE_MODEL ?? "gpt-image-2",
  prompt: "A simple flat 2D sci-fi factory game UI icon, dark metal, blue glow, no text, no watermark.",
  size: "1024x1024",
  quality: "low"
});

const imageBase64 = result.data?.[0]?.b64_json;

if (!imageBase64) {
  console.log(JSON.stringify(result, null, 2));
  throw new Error("No b64_json returned.");
}

const imagePath = path.join(outputDir, "direct_openai_test.png");
await fs.writeFile(imagePath, Buffer.from(imageBase64, "base64"));

console.log("Image saved:", imagePath);
