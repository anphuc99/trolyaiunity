import "reflect-metadata";
import { DataSource } from "typeorm";
import { AppDataSource } from "../data-source.js";

/**
 * Initializes a data source with schema reset enabled for local setup.
 *
 * @returns An initialized TypeORM data source instance.
 */
const createResetDataSource = async () => {
  const dataSource = new DataSource({
    ...AppDataSource.options,
    synchronize: true,
    dropSchema: true
  });

  await dataSource.initialize();
  return dataSource;
};

/**
 * Runs a destructive schema reset for local development.
 */
const run = async () => {
  let dataSource: DataSource | null = null;

  try {
    dataSource = await createResetDataSource();
    console.log("Database schema reset successfully.");
  } catch (error) {
    console.error("Failed to reset database schema.", error);
    process.exitCode = 1;
  } finally {
    if (dataSource?.isInitialized) {
      await dataSource.destroy();
    }
  }
};

void run();
