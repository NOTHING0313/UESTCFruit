import fs from "node:fs/promises";
import { createReadStream } from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import OpenAI from "openai";
import { z } from "zod";
import { fetch as undiciFetch, ProxyAgent } from "undici";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
const OUTPUT_ROOT = path.resolve(process.env.IMAGE_MCP_OUTPUT_ROOT ?? "./generated");
const PROJECT_ROOT = path.resolve(process.env.IMAGE_MCP_PROJECT_ROOT ?? path.join(OUTPUT_ROOT, "../../.."));
const DEFAULT_OPENAI_PROXY_URL = process.env.OPENAI_PROXY_URL?.trim() || "";
const JOBS_ROOT = path.join(OUTPUT_ROOT, "_jobs");
const MAX_JOBS_PER_SESSION = parsePositiveInt(process.env.IMAGE_MCP_MAX_JOBS_PER_SESSION, 10);
const MAX_ASSETS_PER_MANIFEST = parsePositiveInt(process.env.IMAGE_MCP_MAX_ASSETS_PER_MANIFEST, 12);
function parsePositiveInt(value, fallback) {
    const parsed = Number.parseInt(value ?? "", 10);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}
const ProviderSchema = z.enum(["openai", "volcengine"]);
const QualitySchema = z.enum(["low", "medium", "high", "auto"]);
const ResponseFormatSchema = z.enum(["url", "b64_json"]);
const ReferenceRoleSchema = z.enum(["sketch", "style", "shape", "color", "composition", "content", "other"]);
const imageJobs = new Map();
function nowIso() {
    return new Date().toISOString();
}
function toErrorMessage(error) {
    if (!(error instanceof Error))
        return String(error);
    const anyError = error;
    const details = {
        name: error.name,
        message: error.message,
        stack: error.stack,
        code: anyError.code,
        status: anyError.status,
        type: anyError.type,
        cause: anyError.cause
            ? {
                name: anyError.cause.name,
                message: anyError.cause.message,
                code: anyError.cause.code,
                stack: anyError.cause.stack
            }
            : null
    };
    return JSON.stringify(details, null, 2);
}
function getDefaultProvider() {
    const raw = (process.env.IMAGE_PROVIDER ?? "openai").toLowerCase();
    return raw === "volcengine" ? "volcengine" : "openai";
}
function getDefaultModel(provider) {
    if (provider === "volcengine")
        return process.env.VOLCENGINE_IMAGE_MODEL ?? "doubao-seedream-5-0-260128";
    return process.env.OPENAI_IMAGE_MODEL ?? "gpt-image-2";
}
function getDefaultSize(provider) {
    if (provider === "volcengine")
        return process.env.VOLCENGINE_IMAGE_SIZE ?? "2K";
    return process.env.OPENAI_IMAGE_SIZE ?? "1024x1024";
}
function getDefaultPreviewSize(provider) {
    if (provider === "volcengine")
        return process.env.VOLCENGINE_IMAGE_PREVIEW_SIZE ?? getDefaultSize(provider);
    return process.env.OPENAI_IMAGE_PREVIEW_SIZE ?? "1024x1024";
}
function requireProviderKey(provider) {
    if (provider === "volcengine") {
        if (!process.env.ARK_API_KEY)
            throw new Error("ARK_API_KEY is not set.");
        return;
    }
    if (!process.env.OPENAI_API_KEY)
        throw new Error("OPENAI_API_KEY is not set.");
}
/** 获取不同 provider 对应的代理地址。 */
function getProxyUrl(provider) {
    if (provider === "volcengine")
        return process.env.VOLCENGINE_PROXY_URL?.trim() || undefined;
    return process.env.OPENAI_PROXY_URL?.trim() || DEFAULT_OPENAI_PROXY_URL || undefined;
}
/** 创建代理 fetch；不要使用 fetchOptions.dispatcher，避免 undici 兼容问题。 */
function createProxyFetch(proxyUrl) {
    if (!proxyUrl)
        return undefined;
    const dispatcher = new ProxyAgent(proxyUrl);
    return async (url, init = {}) => {
        return undiciFetch(url, {
            ...init,
            dispatcher
        });
    };
}
/** 创建图像客户端。 */
function createImageClient(provider) {
    const proxyFetch = createProxyFetch(getProxyUrl(provider));
    const commonOptions = {
        timeout: 900_000,
        maxRetries: 1
    };
    if (proxyFetch)
        commonOptions.fetch = proxyFetch;
    if (provider === "volcengine") {
        return new OpenAI({
            baseURL: process.env.VOLCENGINE_BASE_URL ?? "https://ark.cn-beijing.volces.com/api/v3",
            apiKey: process.env.ARK_API_KEY,
            ...commonOptions
        });
    }
    return new OpenAI({
        apiKey: process.env.OPENAI_API_KEY,
        ...commonOptions
    });
}
function sanitizeFileName(name) {
    return name
        .trim()
        .replace(/[\\/:*?"<>|]/g, "_")
        .replace(/\s+/g, "_")
        .replace(/_+/g, "_")
        .toLowerCase();
}
function ensureInsideOutputRoot(targetPath) {
    const resolved = path.resolve(targetPath);
    const relative = path.relative(OUTPUT_ROOT, resolved);
    if (relative.startsWith("..") || path.isAbsolute(relative))
        throw new Error(`Blocked path outside IMAGE_MCP_OUTPUT_ROOT: ${resolved}`);
    return resolved;
}
async function ensureDir(dir) {
    await fs.mkdir(dir, { recursive: true });
}
async function exists(filePath) {
    try {
        await fs.access(filePath);
        return true;
    }
    catch {
        return false;
    }
}
async function createVersionedFilePath(folder, baseName, extension) {
    const safeBaseName = sanitizeFileName(baseName);
    let filePath = path.join(folder, `${safeBaseName}.${extension}`);
    if (!(await exists(filePath)))
        return filePath;
    const stamp = new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14);
    filePath = path.join(folder, `${safeBaseName}_${stamp}.${extension}`);
    if (!(await exists(filePath)))
        return filePath;
    const random = crypto.randomBytes(3).toString("hex");
    return path.join(folder, `${safeBaseName}_${stamp}_${random}.${extension}`);
}
async function writeJson(filePath, data) {
    await fs.writeFile(filePath, JSON.stringify(data, null, 2), "utf-8");
}
async function readJson(filePath) {
    const raw = await fs.readFile(filePath, "utf-8");
    return JSON.parse(raw);
}
function getJobPath(jobId) {
    const safeJobId = sanitizeFileName(jobId);
    return path.join(JOBS_ROOT, `${safeJobId}.json`);
}
async function persistJob(job) {
    await ensureDir(JOBS_ROOT);
    await writeJson(getJobPath(job.jobId), job);
}
function persistJobSoon(job) {
    void persistJob(job).catch(() => {
        // MCP stdio must not be polluted by console output. Ignore persistence errors here;
        // the active in-memory job remains available during the current process.
    });
}
async function loadJob(jobId) {
    const existing = imageJobs.get(jobId);
    if (existing)
        return existing;
    const filePath = getJobPath(jobId);
    if (!(await exists(filePath)))
        return undefined;
    const job = await readJson(filePath);
    imageJobs.set(job.jobId, job);
    return job;
}
async function listPersistedJobs() {
    if (!(await exists(JOBS_ROOT)))
        return [];
    const files = await fs.readdir(JOBS_ROOT);
    const jobs = [];
    for (const file of files.filter(fileName => fileName.endsWith(".json"))) {
        try {
            jobs.push(await readJson(path.join(JOBS_ROOT, file)));
        }
        catch {
            // Ignore malformed persisted job records.
        }
    }
    return jobs;
}
async function downloadToFile(url, filePath, provider) {
    const proxyFetch = createProxyFetch(getProxyUrl(provider));
    const response = proxyFetch ? await proxyFetch(url) : await fetch(url);
    if (!response.ok)
        throw new Error(`Failed to download image from URL. status=${response.status}, url=${url}`);
    const arrayBuffer = await response.arrayBuffer();
    await fs.writeFile(filePath, Buffer.from(arrayBuffer));
}
async function resolveReferenceImagePath(referencePath) {
    const candidates = path.isAbsolute(referencePath)
        ? [path.resolve(referencePath)]
        : [
            path.resolve(PROJECT_ROOT, referencePath),
            path.resolve(OUTPUT_ROOT, referencePath),
            path.resolve(referencePath)
        ];
    for (const candidate of candidates) {
        if (await exists(candidate))
            return candidate;
    }
    throw new Error(`Reference image not found: ${referencePath}`);
}
async function resolveReferenceImagePaths(referenceImages) {
    const referencesWithPath = (referenceImages ?? []).filter(reference => !!reference.path?.trim());
    const resolved = [];
    for (const reference of referencesWithPath)
        resolved.push(await resolveReferenceImagePath(reference.path));
    return resolved;
}
function buildPrompt(input) {
    const referenceLines = (input.referenceImages ?? []).map((reference, index) => {
        const title = reference.label?.trim() || `reference_${index + 1}`;
        const note = reference.note?.trim() ? `, note: ${reference.note.trim()}` : "";
        const pathHint = reference.path?.trim() ? `, source: ${reference.path.trim()}` : "";
        return `- ${title}, role: ${reference.role}${note}${pathHint}`;
    });
    return [
        input.style ? `Art direction: ${input.style}` : null,
        input.assetType ? `Asset type: ${input.assetType}` : null,
        input.usage ? `Usage in Unity UI: ${input.usage}` : null,
        referenceLines.length > 0 ? "Reference guidance:" : null,
        referenceLines.length > 0 ? referenceLines.join("\n") : null,
        input.prompt,
        "Requirements: game UI asset, clean silhouette, readable at small size.",
        "Avoid embedded text unless the prompt explicitly asks for text.",
        "Avoid watermark unless explicitly requested."
    ].filter(Boolean).join("\n");
}
function createImageRequest(input) {
    if (input.provider === "volcengine") {
        return {
            model: input.model,
            prompt: input.prompt,
            size: input.size,
            response_format: input.responseFormat
        };
    }
    return {
        model: input.model,
        prompt: input.prompt,
        size: input.size,
        quality: input.quality
    };
}
async function requestOpenAIImageWithOptionalReferences(input) {
    if (input.referenceImagePaths.length <= 0) {
        return input.client.images.generate({
            model: input.model,
            prompt: input.prompt,
            size: input.size,
            quality: input.quality
        });
    }
    const imageStreams = input.referenceImagePaths.map(imagePath => createReadStream(imagePath));
    const imageInput = imageStreams.length === 1 ? imageStreams[0] : imageStreams;
    return input.client.images.edit({
        model: input.model,
        image: imageInput,
        prompt: input.prompt,
        size: input.size,
        quality: input.quality
    });
}
async function generateAndSaveImage(input, stage = "final") {
    requireProviderKey(input.provider);
    const client = createImageClient(input.provider);
    const actualModel = input.model?.trim() || getDefaultModel(input.provider);
    const actualSize = stage === "preview"
        ? (input.previewSize?.trim() || getDefaultPreviewSize(input.provider))
        : (input.finalSize?.trim() || input.size?.trim() || getDefaultSize(input.provider));
    const actualQuality = stage === "preview"
        ? (input.previewQuality ?? "low")
        : (input.finalQuality ?? input.quality ?? "low");
    const actualResponseFormat = input.responseFormat ?? (input.provider === "volcengine" ? "url" : "b64_json");
    const folder = ensureInsideOutputRoot(path.join(OUTPUT_ROOT, input.outputFolder));
    await ensureDir(folder);
    const referenceImagePaths = await resolveReferenceImagePaths(input.referenceImages);
    const finalPrompt = buildPrompt({
        style: input.style,
        usage: input.usage,
        assetType: input.assetType,
        prompt: input.prompt,
        referenceImages: input.referenceImages
    });
    let result;
    if (input.provider === "openai") {
        result = await requestOpenAIImageWithOptionalReferences({
            client,
            model: actualModel,
            prompt: finalPrompt,
            size: actualSize,
            quality: actualQuality,
            referenceImagePaths
        });
    }
    else {
        const requestBody = createImageRequest({
            provider: input.provider,
            model: actualModel,
            prompt: finalPrompt,
            size: actualSize,
            quality: actualQuality,
            responseFormat: actualResponseFormat
        });
        result = await client.images.generate(requestBody, {
            extraBody: {
                watermark: input.watermark ?? false
            }
        });
    }
    const first = result.data?.[0];
    if (!first)
        throw new Error("Image generation returned no image data.");
    const fileBaseName = stage === "preview" ? `${input.assetName}_preview` : input.assetName;
    const imagePath = await createVersionedFilePath(folder, fileBaseName, "png");
    if (first.b64_json) {
        await fs.writeFile(imagePath, Buffer.from(first.b64_json, "base64"));
    }
    else if (first.url) {
        await downloadToFile(first.url, imagePath, input.provider);
    }
    else {
        throw new Error("Image generation returned neither b64_json nor url.");
    }
    const metadataPath = `${imagePath}.json`;
    await writeJson(metadataPath, {
        provider: input.provider,
        model: actualModel,
        assetName: input.assetName,
        stage,
        outputFolder: input.outputFolder,
        size: actualSize,
        quality: actualQuality,
        responseFormat: actualResponseFormat,
        watermark: input.watermark ?? false,
        style: input.style ?? null,
        usage: input.usage ?? null,
        assetType: input.assetType ?? null,
        referenceImages: input.referenceImages ?? [],
        usedReferenceImagePaths: referenceImagePaths,
        prompt: finalPrompt,
        imagePath,
        createdAt: nowIso()
    });
    return {
        provider: input.provider,
        model: actualModel,
        imagePath,
        metadataPath,
        stage,
        usedReferenceImagePaths: referenceImagePaths
    };
}
function startImageGenerationJob(input) {
    if (imageJobs.size >= MAX_JOBS_PER_SESSION)
        throw new Error(`IMAGE_MCP_MAX_JOBS_PER_SESSION reached: ${MAX_JOBS_PER_SESSION}`);
    const jobId = crypto.randomUUID();
    const time = nowIso();
    const job = {
        jobId,
        status: "pending",
        request: input,
        createdAt: time,
        updatedAt: time
    };
    imageJobs.set(jobId, job);
    persistJobSoon(job);
    void (async () => {
        job.status = "running";
        job.updatedAt = nowIso();
        persistJobSoon(job);
        try {
            if (input.previewFirst) {
                job.previewResult = await generateAndSaveImage(input, "preview");
                job.status = "preview_succeeded";
                job.updatedAt = nowIso();
                persistJobSoon(job);
                return;
            }
            job.result = await generateAndSaveImage(input, "final");
            job.status = "succeeded";
            job.updatedAt = nowIso();
            persistJobSoon(job);
        }
        catch (error) {
            job.error = toErrorMessage(error);
            job.status = "failed";
            job.updatedAt = nowIso();
            persistJobSoon(job);
        }
    })();
    return job;
}
async function promoteImageGenerationJob(jobId) {
    const job = await loadJob(jobId);
    if (!job)
        throw new Error(`Job not found: ${jobId}`);
    if (job.status !== "preview_succeeded")
        throw new Error(`Job ${jobId} is not ready for promotion. Current status: ${job.status}`);
    job.status = "running";
    job.updatedAt = nowIso();
    persistJobSoon(job);
    void (async () => {
        try {
            job.result = await generateAndSaveImage(job.request, "final");
            job.status = "succeeded";
            job.updatedAt = nowIso();
            persistJobSoon(job);
        }
        catch (error) {
            job.error = toErrorMessage(error);
            job.status = "failed";
            job.updatedAt = nowIso();
            persistJobSoon(job);
        }
    })();
    return job;
}
function serializeJob(job) {
    return {
        jobId: job.jobId,
        status: job.status,
        createdAt: job.createdAt,
        updatedAt: job.updatedAt,
        request: {
            provider: job.request.provider,
            model: job.request.model ?? getDefaultModel(job.request.provider),
            assetName: job.request.assetName,
            outputFolder: job.request.outputFolder,
            usage: job.request.usage ?? null,
            assetType: job.request.assetType ?? null,
            style: job.request.style ?? null,
            size: job.request.size ?? null,
            quality: job.request.quality ?? null,
            previewFirst: job.request.previewFirst ?? false,
            previewSize: job.request.previewSize ?? null,
            previewQuality: job.request.previewQuality ?? null,
            finalSize: job.request.finalSize ?? null,
            finalQuality: job.request.finalQuality ?? null,
            referenceImages: job.request.referenceImages ?? []
        },
        previewResult: job.previewResult ?? null,
        result: job.result ?? null,
        error: job.error ?? null
    };
}
const ReferenceImageSchema = z.object({
    path: z.string().min(1).optional(),
    label: z.string().optional(),
    role: ReferenceRoleSchema.default("other"),
    note: z.string().optional()
});
const ImageGenerateToolSchema = {
    provider: ProviderSchema.default(getDefaultProvider()),
    model: z.string().optional(),
    assetName: z.string().min(1),
    prompt: z.string().min(1),
    outputFolder: z.string().min(1).describe("Relative folder under IMAGE_MCP_OUTPUT_ROOT."),
    style: z.string().optional(),
    usage: z.string().optional(),
    assetType: z.string().optional(),
    size: z.string().optional(),
    quality: QualitySchema.default("low"),
    responseFormat: ResponseFormatSchema.optional(),
    watermark: z.boolean().optional(),
    referenceImages: z.array(ReferenceImageSchema).optional(),
    previewFirst: z.boolean().optional(),
    previewSize: z.string().optional(),
    previewQuality: QualitySchema.optional(),
    finalSize: z.string().optional(),
    finalQuality: QualitySchema.optional()
};
const server = new McpServer({
    name: "image-mcp-server",
    version: "0.7.0"
});
server.tool("health_check", "Return image MCP configuration and safety limits without generating images.", {}, async () => {
    return {
        content: [
            {
                type: "text",
                text: JSON.stringify({
                    ok: true,
                    name: "image-mcp-server",
                    version: "0.7.0",
                    projectRoot: PROJECT_ROOT,
                    outputRoot: OUTPUT_ROOT,
                    provider: getDefaultProvider(),
                    model: getDefaultModel(getDefaultProvider()),
                    hasOpenAIKey: !!process.env.OPENAI_API_KEY,
                    hasArkKey: !!process.env.ARK_API_KEY,
                    openAIProxyUrl: getProxyUrl("openai") ?? null,
                    volcengineProxyUrl: getProxyUrl("volcengine") ?? null,
                    maxJobsPerSession: MAX_JOBS_PER_SESSION,
                    maxAssetsPerManifest: MAX_ASSETS_PER_MANIFEST,
                    persistedJobsRoot: JOBS_ROOT
                }, null, 2)
            }
        ]
    };
});
server.tool("save_asset_manifest", "Validate and save a Unity UI image asset manifest. This does not generate images.", {
    uiName: z.string().min(1),
    style: z.string().min(1),
    outputFolder: z.string().min(1).describe("Relative folder under IMAGE_MCP_OUTPUT_ROOT."),
    provider: ProviderSchema.optional(),
    model: z.string().optional(),
    assets: z.array(z.object({
        provider: ProviderSchema.optional(),
        model: z.string().optional(),
        assetName: z.string().min(1),
        type: z.string().min(1),
        size: z.string().optional(),
        quality: QualitySchema.optional(),
        responseFormat: ResponseFormatSchema.optional(),
        watermark: z.boolean().optional(),
        usage: z.string().min(1),
        prompt: z.string().min(1),
        referenceImages: z.array(ReferenceImageSchema).optional(),
        previewFirst: z.boolean().optional(),
        previewSize: z.string().optional(),
        previewQuality: QualitySchema.optional(),
        finalSize: z.string().optional(),
        finalQuality: QualitySchema.optional()
    })).min(1)
}, async ({ uiName, style, outputFolder, provider, model, assets }) => {
    const folder = ensureInsideOutputRoot(path.join(OUTPUT_ROOT, outputFolder));
    await ensureDir(folder);
    const manifest = {
        uiName,
        style,
        outputFolder,
        provider: provider ?? getDefaultProvider(),
        model,
        assets,
        createdAt: nowIso()
    };
    const manifestPath = path.join(folder, "asset_manifest.json");
    await writeJson(manifestPath, manifest);
    return {
        content: [
            {
                type: "text",
                text: JSON.stringify({
                    ok: true,
                    manifestPath,
                    assetCount: assets.length
                }, null, 2)
            }
        ]
    };
});
server.tool("generate_image_asset", "Generate one Unity UI image asset synchronously. Prefer start_image_generation_job for Codex.", ImageGenerateToolSchema, async ({ provider, model, assetName, prompt, outputFolder, style, usage, assetType, size, quality, responseFormat, watermark, referenceImages, previewFirst, previewSize, previewQuality, finalSize, finalQuality }) => {
    const result = await generateAndSaveImage({
        provider,
        model,
        assetName,
        prompt,
        outputFolder,
        style,
        usage,
        assetType,
        size,
        quality,
        responseFormat,
        watermark,
        referenceImages,
        previewFirst,
        previewSize,
        previewQuality,
        finalSize,
        finalQuality
    }, previewFirst ? "preview" : "final");
    return {
        content: [
            {
                type: "text",
                text: JSON.stringify({
                    ok: true,
                    ...result
                }, null, 2)
            }
        ]
    };
});
server.tool("start_image_generation_job", "Start one image generation job asynchronously and return jobId immediately. Supports reference images and preview-first workflow.", ImageGenerateToolSchema, async ({ provider, model, assetName, prompt, outputFolder, style, usage, assetType, size, quality, responseFormat, watermark, referenceImages, previewFirst, previewSize, previewQuality, finalSize, finalQuality }) => {
    const job = startImageGenerationJob({
        provider,
        model,
        assetName,
        prompt,
        outputFolder,
        style,
        usage,
        assetType,
        size,
        quality,
        responseFormat,
        watermark,
        referenceImages,
        previewFirst,
        previewSize,
        previewQuality,
        finalSize,
        finalQuality
    });
    return {
        content: [
            {
                type: "text",
                text: JSON.stringify({
                    ok: true,
                    ...serializeJob(job)
                }, null, 2)
            }
        ]
    };
});
server.tool("get_image_generation_job", "Get the status/result/error of an async image generation job.", {
    jobId: z.string().min(1)
}, async ({ jobId }) => {
    const job = await loadJob(jobId);
    if (!job) {
        return {
            content: [
                {
                    type: "text",
                    text: JSON.stringify({
                        ok: false,
                        error: `Job not found: ${jobId}`
                    }, null, 2)
                }
            ]
        };
    }
    return {
        content: [
            {
                type: "text",
                text: JSON.stringify({
                    ok: true,
                    ...serializeJob(job)
                }, null, 2)
            }
        ]
    };
});
server.tool("promote_image_generation_job", "Promote a preview-succeeded job into final high-quality generation.", {
    jobId: z.string().min(1)
}, async ({ jobId }) => {
    try {
        const job = await promoteImageGenerationJob(jobId);
        return {
            content: [
                {
                    type: "text",
                    text: JSON.stringify({
                        ok: true,
                        ...serializeJob(job)
                    }, null, 2)
                }
            ]
        };
    }
    catch (error) {
        return {
            content: [
                {
                    type: "text",
                    text: JSON.stringify({
                        ok: false,
                        error: toErrorMessage(error)
                    }, null, 2)
                }
            ]
        };
    }
});
server.tool("list_image_generation_jobs", "List recent async image generation jobs in this MCP server process.", {}, async () => {
    const persisted = await listPersistedJobs();
    const byId = new Map();
    for (const job of persisted)
        byId.set(job.jobId, job);
    for (const job of imageJobs.values())
        byId.set(job.jobId, job);
    const jobs = Array.from(byId.values())
        .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
        .slice(0, 30)
        .map(serializeJob);
    return {
        content: [
            {
                type: "text",
                text: JSON.stringify({
                    ok: true,
                    jobs
                }, null, 2)
            }
        ]
    };
});
server.tool("generate_assets_from_manifest", "Generate images from a saved manifest synchronously. Use only after user approval.", {
    manifestRelativePath: z.string().min(1).describe("Relative path under IMAGE_MCP_OUTPUT_ROOT, for example BuildPanel/asset_manifest.json."),
    maxCount: z.number().int().min(1).max(MAX_ASSETS_PER_MANIFEST).default(Math.min(4, MAX_ASSETS_PER_MANIFEST))
}, async ({ manifestRelativePath, maxCount }) => {
    const manifestPath = ensureInsideOutputRoot(path.join(OUTPUT_ROOT, manifestRelativePath));
    const manifest = await readJson(manifestPath);
    const generated = [];
    for (const asset of manifest.assets.slice(0, maxCount)) {
        try {
            const provider = asset.provider ?? manifest.provider ?? getDefaultProvider();
            const result = await generateAndSaveImage({
                provider,
                model: asset.model ?? manifest.model,
                assetName: asset.assetName,
                prompt: asset.prompt,
                outputFolder: manifest.outputFolder,
                style: manifest.style,
                usage: asset.usage,
                assetType: asset.type,
                size: asset.size,
                quality: asset.quality,
                responseFormat: asset.responseFormat,
                watermark: asset.watermark,
                referenceImages: asset.referenceImages,
                previewFirst: asset.previewFirst,
                previewSize: asset.previewSize,
                previewQuality: asset.previewQuality,
                finalSize: asset.finalSize,
                finalQuality: asset.finalQuality
            }, asset.previewFirst ? "preview" : "final");
            generated.push({
                ok: true,
                assetName: asset.assetName,
                ...result
            });
        }
        catch (error) {
            generated.push({
                ok: false,
                assetName: asset.assetName,
                error: toErrorMessage(error)
            });
        }
    }
    return {
        content: [
            {
                type: "text",
                text: JSON.stringify({
                    ok: true,
                    manifestPath,
                    generated
                }, null, 2)
            }
        ]
    };
});
server.tool("list_generated_assets", "List generated PNG assets under a relative folder inside IMAGE_MCP_OUTPUT_ROOT.", {
    outputFolder: z.string().min(1).default(".")
}, async ({ outputFolder }) => {
    const folder = ensureInsideOutputRoot(path.join(OUTPUT_ROOT, outputFolder));
    await ensureDir(folder);
    const files = await fs.readdir(folder);
    const assets = files
        .filter(file => file.endsWith(".png"))
        .map(file => path.join(folder, file));
    return {
        content: [
            {
                type: "text",
                text: JSON.stringify({
                    ok: true,
                    folder,
                    assets
                }, null, 2)
            }
        ]
    };
});
server.tool("resolve_reference_image_paths", "Resolve local reference image paths relative to the Unity project root or IMAGE_MCP_OUTPUT_ROOT. This does not generate images.", {
    referenceImages: z.array(ReferenceImageSchema).min(1)
}, async ({ referenceImages }) => {
    const resolved = [];
    for (const reference of referenceImages) {
        resolved.push({
            ...reference,
            resolvedPath: reference.path ? await resolveReferenceImagePath(reference.path) : null
        });
    }
    return {
        content: [
            {
                type: "text",
                text: JSON.stringify({
                    ok: true,
                    projectRoot: PROJECT_ROOT,
                    outputRoot: OUTPUT_ROOT,
                    resolved
                }, null, 2)
            }
        ]
    };
});
const transport = new StdioServerTransport();
await server.connect(transport);
