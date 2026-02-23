import type { Request, Response } from "express";
import type { DataSource } from "typeorm";
import MessageEntity from "../../models/message.entity.js";

interface HomeController {
  getMessage: (request: Request, response: Response) => Promise<void>;
}

/**
 * Builds the Home controller with injected data source dependencies.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns The Home controller handlers.
 */
export const createHomeController = (dataSource: DataSource): HomeController => {
  const repository = dataSource.getRepository(MessageEntity);

  const getMessage: HomeController["getMessage"] = async (_request, response) => {
    if (!_request.user) {
      response.status(401).json({ message: "Unauthorized" });
      return;
    }

    try {
      const latestMessage = await repository.findOne({
        where: {
          userId: _request.user.id
        },
        order: {
          createdAt: "DESC"
        }
      });

      if (!latestMessage) {
        response.json({
          message: "Hello from the Node.js server!",
          timestamp: new Date().toISOString()
        });
        return;
      }

      response.json({
        message: latestMessage.content,
        timestamp: latestMessage.createdAt.toISOString()
      });
    } catch (error) {
      console.error("Failed to load message.", error);
      response.status(500).json({
        message: "Failed to load message",
        error: error instanceof Error ? error.message : "Unknown error"
      });
    }
  };

  return {
    getMessage
  };
};
