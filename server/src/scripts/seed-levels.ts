import "reflect-metadata";
import { AppDataSource } from "../data-source.js";
import { seedDefaultLevels, seedDefaultVoices } from "../services/seed.service.js";

/**
 * Seeds the default CEFR levels into the database.
 */
const run = async () => {
  let dataSource;

  try {
    dataSource = await AppDataSource.initialize();
    const levelResult = await seedDefaultLevels(dataSource);
    const voiceResult = await seedDefaultVoices(dataSource);
    console.log(
      `Seed completed successfully. Levels inserted=${levelResult.inserted}, levels updated=${levelResult.updated}, voices inserted=${voiceResult.inserted}.`
    );
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
