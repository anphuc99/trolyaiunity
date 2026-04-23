/**
 * Quick standalone test for Ollama gemma4:e4b.
 *
 * Run with:  npx tsx src/scripts/test-ollama.ts
 *
 * Prerequisites:
 *   1. Ollama running locally: ollama serve
 *   2. Model pulled: ollama pull gemma4:e4b
 */

const OLLAMA_URL = process.env.OLLAMA_URL || "http://localhost:11434";
const MODEL = process.env.OLLAMA_MODEL || "gemma4:e4b";

interface ChatMessage {
  role: "system" | "user" | "assistant";
  content: string;
}

/**
 * Sends a chat completion request to Ollama's OpenAI-compatible API.
 */
async function ollamaChat(messages: ChatMessage[]): Promise<string> {
  const response = await fetch(`${OLLAMA_URL}/v1/chat/completions`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ model: MODEL, messages }),
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(`Ollama API error ${response.status}: ${errorText}`);
  }

  const data = await response.json();
  return data.choices?.[0]?.message?.content?.trim() ?? "(empty response)";
}

/**
 * Sends a raw generate request to Ollama's native API.
 */
async function ollamaGenerate(prompt: string): Promise<string> {
  const response = await fetch(`${OLLAMA_URL}/api/generate`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ model: MODEL, prompt, stream: false }),
  });

  if (!response.ok) {
    const errorText = await response.text();
    throw new Error(`Ollama generate error ${response.status}: ${errorText}`);
  }

  const data = await response.json();
  return data.response?.trim() ?? "(empty response)";
}

async function main() {
  console.log("=".repeat(60));
  console.log(`Ollama Test — Model: ${MODEL}`);
  console.log(`Server: ${OLLAMA_URL}`);
  console.log("=".repeat(60));

  // Test 1: Basic connectivity
  console.log("\n--- Test 1: Check Ollama server ---");
  try {
    const res = await fetch(`${OLLAMA_URL}/api/tags`);
    const data = await res.json();
    const models = (data.models ?? []).map((m: { name: string }) => m.name);
    console.log("Available models:", models.join(", ") || "(none)");

    if (!models.some((m: string) => m.includes("gemma4"))) {
      console.warn(`⚠ Model ${MODEL} not found. Run: ollama pull ${MODEL}`);
    } else {
      console.log(`✓ Model ${MODEL} available`);
    }
  } catch (err) {
    console.error("✗ Cannot connect to Ollama. Is it running? (ollama serve)");
    console.error("  Error:", (err as Error).message);
    process.exit(1);
  }

  // Test 2: Simple generate
  console.log("\n--- Test 2: Simple generate ---");
  try {
    const start = Date.now();
    const reply = await ollamaGenerate("Say hello in Korean. Reply in 1 sentence.");
    const elapsed = Date.now() - start;
    console.log(`✓ Reply (${elapsed}ms):`, reply);
  } catch (err) {
    console.error("✗ Generate failed:", (err as Error).message);
  }

  // Test 3: OpenAI-compatible chat
  console.log("\n--- Test 3: OpenAI-compatible chat API ---");
  try {
    const start = Date.now();
    const reply = await ollamaChat([
      { role: "system", content: "You are a friendly Korean tutor. Reply in Korean." },
      { role: "user", content: "안녕하세요! 오늘 뭐 해요?" },
    ]);
    const elapsed = Date.now() - start;
    console.log(`✓ Reply (${elapsed}ms):`, reply);
  } catch (err) {
    console.error("✗ Chat failed:", (err as Error).message);
  }

  // Test 4: JSON structured output (like the chat controller expects)
  console.log("\n--- Test 4: JSON structured reply ---");
  try {
    const systemPrompt = `You are an AI conversation partner. You MUST reply in valid JSON array format.
Each element has: MessageId (string), CharacterName (string), Text (string), Translation (string).

Example:
[{"MessageId":"msg_1","CharacterName":"Mimi","Text":"안녕하세요!","Translation":"Hello!"}]

Reply ONLY with the JSON array. No markdown, no explanation.`;

    const start = Date.now();
    const reply = await ollamaChat([
      { role: "system", content: systemPrompt },
      { role: "user", content: "Xin chào Mimi!" },
    ]);
    const elapsed = Date.now() - start;
    console.log(`✓ Reply (${elapsed}ms):`, reply);

    // Try to parse as JSON
    try {
      // Strip markdown code blocks if present
      const cleaned = reply.replace(/^```(?:json)?\s*\n?/i, "").replace(/\n?```\s*$/i, "").trim();
      const parsed = JSON.parse(cleaned);
      console.log("✓ Valid JSON:", JSON.stringify(parsed, null, 2));
    } catch {
      console.warn("⚠ Reply is not valid JSON — model may need prompt tuning");
    }
  } catch (err) {
    console.error("✗ Structured chat failed:", (err as Error).message);
  }

  // Test 5: Multi-turn conversation
  console.log("\n--- Test 5: Multi-turn conversation ---");
  try {
    const messages: ChatMessage[] = [
      { role: "system", content: "You are Mimi, a friendly Korean girl. Reply in Korean, keep it short." },
      { role: "user", content: "Mimi야, 이름이 뭐야?" },
    ];

    const start = Date.now();
    const reply1 = await ollamaChat(messages);
    console.log(`  Turn 1 (${Date.now() - start}ms): ${reply1}`);

    messages.push({ role: "assistant", content: reply1 });
    messages.push({ role: "user", content: "취미가 뭐야?" });

    const start2 = Date.now();
    const reply2 = await ollamaChat(messages);
    console.log(`  Turn 2 (${Date.now() - start2}ms): ${reply2}`);

    console.log("✓ Multi-turn OK");
  } catch (err) {
    console.error("✗ Multi-turn failed:", (err as Error).message);
  }

  console.log("\n" + "=".repeat(60));
  console.log("All tests completed.");
  console.log("=".repeat(60));
}

main().catch(console.error);
