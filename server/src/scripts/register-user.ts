import "../env.js";
import readline from "readline";
import bcrypt from "bcryptjs";
import { AppDataSource } from "../data-source.js";
import UserEntity from "../models/user.entity.js";

const BCRYPT_ROUNDS = 10;

/**
 * Prompts the user for input in the terminal.
 *
 * @param question - The prompt text.
 * @param hidden - Whether to hide input (for passwords).
 * @returns The user's input string.
 */
const prompt = (question: string, hidden = false): Promise<string> => {
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });

  return new Promise((resolve) => {
    if (hidden && process.stdin.isTTY) {
      process.stdout.write(question);
      const stdin = process.stdin;
      stdin.setRawMode(true);
      stdin.resume();
      stdin.setEncoding("utf8");

      let input = "";
      const onData = (char: string) => {
        // Enter
        if (char === "\r" || char === "\n") {
          stdin.setRawMode(false);
          stdin.removeListener("data", onData);
          rl.close();
          process.stdout.write("\n");
          resolve(input);
          return;
        }
        // Backspace
        if (char === "\u007F" || char === "\b") {
          if (input.length > 0) {
            input = input.slice(0, -1);
            process.stdout.write("\b \b");
          }
          return;
        }
        // Ctrl+C
        if (char === "\u0003") {
          rl.close();
          process.exit(0);
        }
        input += char;
        process.stdout.write("*");
      };
      stdin.on("data", onData);
    } else {
      rl.question(question, (answer) => {
        rl.close();
        resolve(answer);
      });
    }
  });
};

/**
 * Interactive CLI tool to register a new user account.
 */
const register = async () => {
  console.log("=== Register New User ===\n");

  const username = (await prompt("Username: ")).trim();
  if (!username) {
    console.error("Error: Username is required.");
    process.exitCode = 1;
    return;
  }

  const password = await prompt("Password: ", true);
  if (!password || password.length < 4) {
    console.error("Error: Password must be at least 4 characters.");
    process.exitCode = 1;
    return;
  }

  const confirmPassword = await prompt("Confirm password: ", true);
  if (password !== confirmPassword) {
    console.error("Error: Passwords do not match.");
    process.exitCode = 1;
    return;
  }

  const name = (await prompt("Display name (optional): ")).trim() || null;

  const ageInput = (await prompt("Age (optional): ")).trim();
  let age: number | null = null;
  if (ageInput) {
    age = Number.parseInt(ageInput, 10);
    if (!Number.isInteger(age) || age < 0 || age > 150) {
      console.error("Error: Invalid age (must be 0-150).");
      process.exitCode = 1;
      return;
    }
  }

  try {
    await AppDataSource.initialize();
    const userRepository = AppDataSource.getRepository(UserEntity);

    const existing = await userRepository.findOne({ where: { username } });
    if (existing) {
      console.error(`Error: Username "${username}" already exists.`);
      process.exitCode = 1;
      await AppDataSource.destroy();
      return;
    }

    const passwordHash = await bcrypt.hash(password, BCRYPT_ROUNDS);

    const user = userRepository.create({
      username,
      passwordHash,
      name,
      age,
      rewardPoints: 0,
      money: 0
    });

    const saved = await userRepository.save(user);
    console.log(`\nUser registered successfully! (id: ${saved.id}, username: ${saved.username})`);

    await AppDataSource.destroy();
  } catch (error) {
    console.error("Registration failed.", error);
    process.exitCode = 1;
  }
};

void register();
