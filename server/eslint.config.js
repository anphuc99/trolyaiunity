import js from "@eslint/js";
import eslintConfigPrettier from "eslint-config-prettier";

/**
 * ESLint configuration for the Node.js server.
 *
 * @returns The ESLint flat config array.
 */
export default [
  js.configs.recommended,
  eslintConfigPrettier,
  {
    files: ["**/*.ts"],
    languageOptions: {
      ecmaVersion: "latest",
      sourceType: "module"
    }
  }
];
