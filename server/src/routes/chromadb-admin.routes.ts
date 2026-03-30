import { Router, type Request, type Response } from "express";
import { ChromaClient, IncludeEnum } from "chromadb";
import { createChromaClient } from "../services/chroma-client.factory.js";

/**
 * Creates routes for the ChromaDB admin web UI.
 * Provides endpoints for browsing collections, searching vectors, and CRUD operations.
 *
 * @returns Express router with ChromaDB admin routes.
 */
export const createChromaDbAdminRoutes = (): Router => {
  const router = Router();
  const chromaUrl = process.env.CHROMA_URL ?? "http://localhost:8000";

  /**
   * Returns a ChromaClient instance connected to the configured URL.
   */
  const getClient = (): ChromaClient => createChromaClient(chromaUrl);

  // ── GET /status ─ Check ChromaDB connection ────────────────────────────
  router.get("/status", async (_req: Request, res: Response) => {
    try {
      const client = getClient();
      const heartbeat = await client.heartbeat();
      res.json({ ok: true, chromaUrl, heartbeat });
    } catch (error: any) {
      res.status(503).json({ ok: false, chromaUrl, error: error.message });
    }
  });

  // ── GET /collections ─ List all collections ────────────────────────────
  router.get("/collections", async (_req: Request, res: Response) => {
    try {
      const client = getClient();
      const collections = await client.listCollections();
      res.json({ collections });
    } catch (error: any) {
      res.status(500).json({ error: error.message });
    }
  });

  // ── GET /collections/:name ─ Get collection info & items ───────────────
  router.get("/collections/:name", async (req: Request, res: Response) => {
    try {
      const client = getClient();
      const collection = await client.getCollection({ name: req.params.name });
      const count = await collection.count();
      const limit = Math.min(Number(req.query.limit) || 50, 200);
      const offset = Number(req.query.offset) || 0;

      const data = await collection.get({
        limit,
        offset,
        include: [IncludeEnum.documents, IncludeEnum.metadatas]
      });

      res.json({
        name: req.params.name,
        count,
        limit,
        offset,
        ids: data.ids,
        documents: data.documents,
        metadatas: data.metadatas
      });
    } catch (error: any) {
      res.status(500).json({ error: error.message });
    }
  });

  // ── POST /collections/:name/query ─ Semantic search ────────────────────
  router.post("/collections/:name/query", async (req: Request, res: Response) => {
    try {
      const client = getClient();
      const collection = await client.getCollection({ name: req.params.name });

      const { queryText, nResults = 10, where } = req.body;
      if (!queryText || typeof queryText !== "string") {
        res.status(400).json({ error: "queryText is required" });
        return;
      }

      const results = await collection.query({
        queryTexts: [queryText],
        nResults: Math.min(Number(nResults), 50),
        ...(where ? { where } : {}),
        include: [IncludeEnum.documents, IncludeEnum.metadatas, IncludeEnum.distances]
      });

      res.json({
        ids: results.ids?.[0] ?? [],
        documents: results.documents?.[0] ?? [],
        metadatas: results.metadatas?.[0] ?? [],
        distances: results.distances?.[0] ?? []
      });
    } catch (error: any) {
      res.status(500).json({ error: error.message });
    }
  });

  // ── POST /collections/:name/add ─ Add documents ───────────────────────
  router.post("/collections/:name/add", async (req: Request, res: Response) => {
    try {
      const client = getClient();
      const collection = await client.getCollection({ name: req.params.name });

      const { id, document, metadata } = req.body;
      if (!id || !document) {
        res.status(400).json({ error: "id and document are required" });
        return;
      }

      await collection.upsert({
        ids: [String(id)],
        documents: [String(document)],
        metadatas: metadata ? [metadata] : undefined
      });

      res.json({ ok: true, id });
    } catch (error: any) {
      res.status(500).json({ error: error.message });
    }
  });

  // ── PUT /collections/:name/:id ─ Update a document ────────────────────
  router.put("/collections/:name/:id", async (req: Request, res: Response) => {
    try {
      const client = getClient();
      const collection = await client.getCollection({ name: req.params.name });
      const docId = req.params.id;

      const { document, metadata } = req.body;

      await collection.update({
        ids: [docId],
        ...(document !== undefined ? { documents: [String(document)] } : {}),
        ...(metadata !== undefined ? { metadatas: [metadata] } : {})
      });

      res.json({ ok: true, id: docId });
    } catch (error: any) {
      res.status(500).json({ error: error.message });
    }
  });

  // ── DELETE /collections/:name/:id ─ Delete a document ──────────────────
  router.delete("/collections/:name/:id", async (req: Request, res: Response) => {
    try {
      const client = getClient();
      const collection = await client.getCollection({ name: req.params.name });

      await collection.delete({ ids: [req.params.id] });

      res.json({ ok: true, id: req.params.id });
    } catch (error: any) {
      res.status(500).json({ error: error.message });
    }
  });

  // ── DELETE /collections/:name ─ Delete entire collection ───────────────
  router.delete("/collections/:name", async (req: Request, res: Response) => {
    try {
      const client = getClient();
      await client.deleteCollection({ name: req.params.name });
      res.json({ ok: true, deleted: req.params.name });
    } catch (error: any) {
      res.status(500).json({ error: error.message });
    }
  });

  // ── POST /collections ─ Create a new collection ────────────────────────
  router.post("/collections", async (req: Request, res: Response) => {
    try {
      const { name } = req.body;
      if (!name || typeof name !== "string") {
        res.status(400).json({ error: "name is required" });
        return;
      }

      const client = getClient();
      await client.getOrCreateCollection({ name });
      res.json({ ok: true, name });
    } catch (error: any) {
      res.status(500).json({ error: error.message });
    }
  });

  return router;
};
