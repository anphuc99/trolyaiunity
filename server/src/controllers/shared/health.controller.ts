import type { Request, Response } from "express";

interface HealthController {
  getHealth: (request: Request, response: Response) => void;
}

/**
 * Builds the shared health controller.
 *
 * @returns The health controller handlers.
 */
export const createHealthController = (): HealthController => {
  const getHealth: HealthController["getHealth"] = (_request, response) => {
    response.json({ status: "ok" });
  };

  return {
    getHealth
  };
};
