import "../env.js";
import { AppDataSource } from "../data-source.js";

/**
 * Synchronizes the database schema with the current entity definitions.
 * Creates missing tables and columns without dropping data.
 */
const sync = async () => {
  try {
    await AppDataSource.initialize();
    await AppDataSource.synchronize();
    console.log("Database schema synchronized.");
    await AppDataSource.destroy();
  } catch (error) {
    console.error("Failed to synchronize database.", error);
    process.exitCode = 1;
  }
};

void sync();
