import "reflect-metadata";
import { AppDataSource } from "../data-source.js";
import { seedDefaultLevels } from "../services/seed.service.js";

/**
 * Seeds the default CEFR levels into the database.
 */
const run = async () => {
  let dataSource;

  try {
    dataSource = await AppDataSource.initialize();
    const result = await seedDefaultLevels(dataSource);
    console.log(`Levels seeded successfully. Inserted: ${result.inserted}, updated: ${result.updated}.`);
  } catch (error) {
    console.error("Failed to seed levels.", error);
    process.exitCode = 1;
  } finally {
    if (dataSource?.isInitialized) {
      await dataSource.destroy();
    }
  }
};

void run();
