import fs from 'fs';
import path from 'path';
import dotenv from 'dotenv';
import { GoogleGenerativeAI } from '@google/generative-ai';
import { synthesizeGeminiTts } from '../services/gemini-tts.service.js';

// Load biến môi trường từ file .env (Để lấy GEMINI_API_KEY_VOICE1... vv)
dotenv.config();

// ==========================================
// CẤU HÌNH ĐƯỜNG DẪN VÀ THAM SỐ
// ==========================================
const OUTPUT_DIR = path.join(process.cwd(), 'data/dataset_chinese');
const REFERENCE_DIR = path.join(process.cwd(), 'data/reference_audios');
const LIST_FILE = path.join(process.cwd(), 'data/dataset.list');

// Tạo thư mục nếu chưa có
if (!fs.existsSync(OUTPUT_DIR)) fs.mkdirSync(OUTPUT_DIR, { recursive: true });
if (!fs.existsSync(REFERENCE_DIR)) fs.mkdirSync(REFERENCE_DIR, { recursive: true });

const TARGET_SENTENCES = 200;
const VOICES = ["Puck", "Charon", "Fenrir", "Leda", "Zephyr"]; // Các giọng Gemini TTS 

// ==========================================
// PHẦN 1: DÙNG GEMINI TẠO KỊCH BẢN (TEXT)
// ==========================================
async function generateScriptsWithGemini(targetCount: number): Promise<any[]> {
    console.log(`[*] Đang yêu cầu Gemini tạo ${targetCount} câu thoại tiếng Trung (mỗi câu < 20 chữ)...`);

    // Sử dụng API key đầu tiên để sinh text
    const apiKey = process.env.GOOGLE_API_KEY;
    if (!apiKey) {
        throw new Error("Không tìm thấy GEMINI_API_KEY trong môi trường.");
    }

    const genAI = new GoogleGenerativeAI(apiKey);
    const model = genAI.getGenerativeModel({ model: "gemini-flash-lite-latest" });

    const allScripts: any[] = [];

    while (allScripts.length < targetCount) {
        const needed = targetCount - allScripts.length;
        const batchSize = Math.min(needed, 50);
        console.log(`  -> Yêu cầu sinh thêm ${batchSize} câu (Đã có: ${allScripts.length}/${targetCount})`);

        const prompt = `
        You are a scriptwriter. Generate exactly ${batchSize} short conversational sentences in Chinese (Simplified).
        CRITICAL RULE: Each sentence MUST be very short, strictly 15-20 Chinese characters.
        The sentences should cover a wide variety of daily conversations, exclamations, questions, and answers.
        For each sentence, assign an emotion (must be one of: happy, sad, angry, neutral,...) and an intensity (low, medium, high).
        
        ====================================
        TEXT STYLE MARKERS (APPLY TO Text FIELD ONLY)
        ====================================
        These indicators are for the Text field only. Do NOT copy these symbols/phrases into Tone.
        Angry: !!!
        Shouting: !!!!!
        Disgusted: 呃... ...  
        Sad: ... ...  
        Scared: 啊... ...  
        Surprised: 咦?! ?!  
        Shy: ...  
        Affectionate: 嗯...  
        Happy: !  
        Excited: 哇! !!!  
        Serious: .  
        Neutral: unchanged

        Examples:
        [{"text": "我好开心啊！", "emotion": "happy", "intensity": "high"}, {"text": "你为什么生气了？", "emotion": "angry", "intensity": "medium"}]

        Output strictly in the following JSON array format, with no extra markdown or text:
        [
            {"text": "Chinese text here", "emotion": "happy", "intensity": "high"},
            ...
        ]
        `;

        try {
            const result = await model.generateContent(prompt);
            let textContent = result.response.text().trim();

            // Clean markdown
            if (textContent.startsWith("```json")) textContent = textContent.slice(7);
            if (textContent.startsWith("```")) textContent = textContent.slice(3);
            if (textContent.endsWith("```")) textContent = textContent.slice(0, -3);

            const scripts = JSON.parse(textContent.trim());
            allScripts.push(...scripts);
            console.log(`  -> Nhận được ${scripts.length} câu hợp lệ.`);
        } catch (e) {
            console.error(`  [!] Lỗi khi gọi Gemini hoặc parse JSON:`, e);
            console.log("  [!] Thử lại trong 2 giây...");
            await new Promise(r => setTimeout(r, 2000));
        }
    }

    console.log(`[+] Hoàn tất! Đã thu thập đủ ${allScripts.length} câu thoại.`);
    return allScripts.slice(0, targetCount);
}

// ==========================================
// PHẦN 2: DÙNG GEMINI TTS (Nội bộ) TẠO ÂM THANH
// ==========================================
async function synthesizeAudioAndSave(scripts: any[]) {
    console.log(`[*] Bắt đầu gọi Gemini TTS nội bộ để tạo âm thanh cho ${VOICES.length} giọng...`);

    // Đọc file index cũ nếu có để ghi nối thêm
    let referenceIndex: any[] = [];
    const indexPath = path.join(REFERENCE_DIR, 'reference_index.json');
    if (fs.existsSync(indexPath)) {
        try {
            const existingData = fs.readFileSync(indexPath, 'utf8');
            referenceIndex = JSON.parse(existingData);
            console.log(`[*] Đã tải ${referenceIndex.length} mồi cũ từ reference_index.json`);
        } catch (e) {
            console.error("[!] Lỗi đọc reference_index.json cũ, sẽ tạo mới mảng rỗng.");
        }
    }

    for (const voiceName of VOICES) {
        // Kiểm tra xem giọng này đã được tạo mồi chưa
        const isVoiceDone = referenceIndex.some(entry => entry.voice === voiceName);
        if (isVoiceDone) {
            console.log(`\n[!] Giọng ${voiceName} đã có trong reference_index.json, bỏ qua để tiết kiệm tài nguyên.`);
            continue;
        }

        console.log(`\n--- Đang xử lý cho giọng: ${voiceName} ---`);

        // Tạo thư mục con cho từng giọng
        const voiceOutputDir = path.join(OUTPUT_DIR, voiceName);
        const voiceRefDir = path.join(REFERENCE_DIR, voiceName);
        if (!fs.existsSync(voiceOutputDir)) fs.mkdirSync(voiceOutputDir, { recursive: true });
        if (!fs.existsSync(voiceRefDir)) fs.mkdirSync(voiceRefDir, { recursive: true });

        for (let i = 0; i < scripts.length; i++) {
            const item = scripts[i];
            const text = item.text;
            const emotion = item.emotion || "neutral";
            const intensity = item.intensity || "medium";

            if (!text) continue;

            // Chuyển đổi emotion và intensity thành tone cho service
            let toneDesc = `${emotion},${intensity} pitch`;

            try {
                // Gọi service có sẵn của bạn
                const audioBuffer = await synthesizeGeminiTts(text, voiceName, toneDesc);

                const fileName = `audio_${voiceName}_${Date.now()}_${String(i).padStart(3, '0')}.wav`;
                const filePath = path.join(voiceOutputDir, fileName);

                // Ghi file âm thanh (.wav)
                fs.writeFileSync(filePath, audioBuffer);

                // Ghi file dataset.list (đổi tên Speaker thành tên giọng)
                const absPath = path.resolve(filePath);
                const listLine = `${absPath}|${voiceName}|ZH|${text}\n`;
                fs.appendFileSync(LIST_FILE, listLine, 'utf8');

                console.log(`  -> Đã lưu ${fileName} [Tone: ${toneDesc}]`);

                // Lưu vào bảng chỉ mục tra cứu để tra mồi
                referenceIndex.push({
                    voice: voiceName,
                    emotion: emotion,
                    intensity: intensity,
                    text: text,
                    file: fileName
                });

                // Nghỉ 1 chút để tránh rate limit của model TTS
                await new Promise(r => setTimeout(r, 1000));

            } catch (e) {
                console.error(`[!] Lỗi khi gọi Gemini TTS cho câu '${text}':`, e);
            }
        }
    }

    // Ghi đè file JSON tra cứu (đã bao gồm data cũ + data mới)
    fs.writeFileSync(indexPath, JSON.stringify(referenceIndex, null, 4), 'utf8');
    console.log(`\n[+] Đã cập nhật file tra cứu các âm thanh mẫu tại: ${indexPath}`);
}

// ==========================================
// CHƯƠNG TRÌNH CHÍNH
// ==========================================
async function main() {
    console.log("=== BẮT ĐẦU QUÁ TRÌNH TẠO DATASET TỰ ĐỘNG BẰNG GEMINI TTS ===");
    const scripts = await generateScriptsWithGemini(TARGET_SENTENCES);

    if (scripts && scripts.length > 0) {
        // Log kịch bản cho người dùng duyệt
        console.log("\n=== DANH SÁCH KỊCH BẢN ĐÃ TẠO ===");
        scripts.forEach((s, idx) => {
            console.log(`${idx + 1}. [${s.emotion.toUpperCase()} - ${s.intensity.toUpperCase()}] ${s.text}`);
        });

        // Chờ người dùng duyệt
        const readline = (await import('readline')).createInterface({
            input: process.stdin,
            output: process.stdout
        });

        const answer = await new Promise(resolve => {
            readline.question("\n[?] Bạn có đồng ý với kịch bản trên không? Nhấn 'y' để tiếp tục chạy TTS, nhấn phím khác để thoát: ", resolve);
        });

        readline.close();

        if (answer !== 'y' && answer !== 'Y') {
            console.log("[*] Đã dừng quá trình theo yêu cầu của người dùng.");
            return;
        }

        await synthesizeAudioAndSave(scripts);
        console.log(`\n[+] HOÀN TẤT! Dữ liệu đã lưu tại '${OUTPUT_DIR}'`);
        console.log(`[+] File cấu hình GPT-SoVITS: '${LIST_FILE}'`);
        console.log(`[+] Các file âm thanh mẫu (để làm mồi cảm xúc) nằm ở '${REFERENCE_DIR}'`);
    } else {
        console.log("\n[!] Không có kịch bản để tạo âm thanh.");
    }
}

main();
