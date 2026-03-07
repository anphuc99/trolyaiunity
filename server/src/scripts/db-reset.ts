import "../env.js";
import { AppDataSource } from "../data-source.js";

/**
 * Drops and re-creates all tables. Destructive — all data will be lost.
 */
const reset = async () => {
  try {
    await AppDataSource.initialize();
    await AppDataSource.synchronize(true);
    console.log("Database reset complete — all tables dropped and re-created.");
    await AppDataSource.destroy();
  } catch (error) {
    console.error("Failed to reset database.", error);
    process.exitCode = 1;
  }
};

void reset();
