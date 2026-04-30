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
const VOICES = ["Zephyr"]; // Các giọng Gemini TTS "Puck", "Charon", "Fenrir", "Leda", 

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
    const scripts = [
        { "text": "看到你平安归来我真是太高兴了!", "emotion": "happy", "intensity": "high" },
        { "text": "这个生日派对真的办得非常棒啊!", "emotion": "happy", "intensity": "high" },
        { "text": "终于买到了梦寐以求的新款手机!", "emotion": "happy", "intensity": "high" },
        { "text": "我们这次考试竟然全都顺利及格!", "emotion": "happy", "intensity": "medium" },
        { "text": "周末能和你一起逛街真是太开心!", "emotion": "happy", "intensity": "high" },
        { "text": "这家餐厅的招牌菜味道真是太赞!", "emotion": "happy", "intensity": "medium" },
        { "text": "听到你升职的消息我由衷地高兴!", "emotion": "happy", "intensity": "high" },
        { "text": "谢谢你今天特地准备的惊喜礼物!", "emotion": "happy", "intensity": "high" },
        { "text": "能够认识你这样的好朋友真幸运!", "emotion": "happy", "intensity": "medium" },
        { "text": "窗外的风景那么美，我们去拍照!", "emotion": "happy", "intensity": "low" },
        { "text": "你的新发型看起来真是很漂亮啊!", "emotion": "happy", "intensity": "medium" },
        { "text": "终于把这个大项目完成了好轻松!", "emotion": "happy", "intensity": "high" },
        { "text": "只要大家都在一起每天都好快乐!", "emotion": "happy", "intensity": "high" },
        { "text": "这么快就拿到了奖金去吃顿好的!", "emotion": "happy", "intensity": "high" },
        { "text": "我们养的小猫今天学会用猫砂了!", "emotion": "happy", "intensity": "medium" },
        { "text": "收到你的明信片上面的风景好美!", "emotion": "happy", "intensity": "low" },
        { "text": "刚才看的那部喜剧电影真的搞笑!", "emotion": "happy", "intensity": "high" },
        { "text": "这件衣服穿在你身上简直太合适!", "emotion": "happy", "intensity": "medium" },
        { "text": "祝你生日快乐希望你永远开心啊!", "emotion": "happy", "intensity": "high" },
        { "text": "外面的空气好清新赶紧去散散步!", "emotion": "happy", "intensity": "low" },
        { "text": "为什么最后会变成这个样子... ...", "emotion": "sad", "intensity": "high" },
        { "text": "他怎么可以背着我做这种事... ...", "emotion": "sad", "intensity": "high" },
        { "text": "这种结果真的让人大失所望... ...", "emotion": "sad", "intensity": "medium" },
        { "text": "我现在再也不想看到他了啊... ...", "emotion": "sad", "intensity": "high" },
        { "text": "为什么上天对我这么不公平... ...", "emotion": "sad", "intensity": "high" },
        { "text": "失去你之后我真的不知咋办... ...", "emotion": "sad", "intensity": "high" },
        { "text": "看来一切都太迟了无法挽回... ...", "emotion": "sad", "intensity": "medium" },
        { "text": "所有的这些努力最终全白费... ...", "emotion": "sad", "intensity": "medium" },
        { "text": "也许我们俩真的非常不合适... ...", "emotion": "sad", "intensity": "low" },
        { "text": "只能把这些委屈全藏在心里... ...", "emotion": "sad", "intensity": "medium" },
        { "text": "感觉这个世界突然变好孤单... ...", "emotion": "sad", "intensity": "high" },
        { "text": "想到这里眼泪忍不住掉下来... ...", "emotion": "sad", "intensity": "high" },
        { "text": "原来这一切只是我一厢情愿... ...", "emotion": "sad", "intensity": "medium" },
        { "text": "我对未来的所有希望全破灭... ...", "emotion": "sad", "intensity": "high" },
        { "text": "是谁无情偷走了我最初梦想... ...", "emotion": "sad", "intensity": "high" },
        { "text": "没有你陪伴的日子真好难熬... ...", "emotion": "sad", "intensity": "high" },
        { "text": "我的心里此刻就像针扎样痛... ...", "emotion": "sad", "intensity": "high" },
        { "text": "我们再也回不到过去的时光... ...", "emotion": "sad", "intensity": "high" },
        { "text": "这段刻骨铭心感情到此为止... ...", "emotion": "sad", "intensity": "medium" },
        { "text": "我付出的这一切都没人能懂... ...", "emotion": "sad", "intensity": "medium" },
        { "text": "你这个人到底胡说八道什么!!!", "emotion": "angry", "intensity": "high" },
        { "text": "马上给我从这个房间滚出去!!!", "emotion": "angry", "intensity": "high" },
        { "text": "警告你绝对不要挑战我底线!!!", "emotion": "angry", "intensity": "high" },
        { "text": "你怎么能够做出这种无耻事!!!", "emotion": "angry", "intensity": "high" },
        { "text": "我再也不想听你的虚假谎言!!!", "emotion": "angry", "intensity": "high" },
        { "text": "你们全都是一群没用废弃物!!!", "emotion": "angry", "intensity": "high" },
        { "text": "到底是谁允许你随便动我物!!!", "emotion": "angry", "intensity": "high" },
        { "text": "别再用这种恶劣的态度对我!!!", "emotion": "angry", "intensity": "medium" },
        { "text": "你这个人简直就是不可理喻!!!", "emotion": "angry", "intensity": "high" },
        { "text": "赶紧把我的钱一分不少还来!!!", "emotion": "angry", "intensity": "high" },
        { "text": "我绝对不会原谅你的所作为!!!", "emotion": "angry", "intensity": "high" },
        { "text": "这种糟糕烂摊子我再也不管!!!", "emotion": "angry", "intensity": "medium" },
        { "text": "你别以为这样可以轻松蒙混!!!", "emotion": "angry", "intensity": "medium" },
        { "text": "闭上你的臭嘴不要再说话了!!!", "emotion": "angry", "intensity": "high" },
        { "text": "刚才明明就是你故意撞我的!!!", "emotion": "angry", "intensity": "medium" },
        { "text": "你到底还有没有一点责任心!!!", "emotion": "angry", "intensity": "high" },
        { "text": "真是气死我了以后绝对不理!!!", "emotion": "angry", "intensity": "high" },
        { "text": "把这破事马上给我解释清楚!!!", "emotion": "angry", "intensity": "high" },
        { "text": "别再用敷衍的借口来欺骗我!!!", "emotion": "angry", "intensity": "high" },
        { "text": "你真是让我心底感到恶心透!!!", "emotion": "angry", "intensity": "high" },
        { "text": "哇! 我们终于中超级大奖了!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 这场演唱会真是太精彩!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 天上流星雨真的好漂亮!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 我们竟然赢了这场比赛!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 亲眼看到偶像真的激动!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 巨型过山车实在是刺激!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 你终于顺利考上理想校!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 你的新发型看起来很酷!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 今天的游乐园真太好玩!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 他居然当街向你求婚了!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 终于成功买到限量包包!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 超级大蛋糕真是太精致!!!", "emotion": "excited", "intensity": "medium" },
        { "text": "哇! 竟然能够在这里遇到你!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 能够顺利拿到升职机会!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 新款超级跑车真的好帅!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 全新上市智能手机到手!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 我们明天就要飞去欧洲!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 这简直是不可思议奇迹!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 游戏全通关真的成就感!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇! 豪华晚餐真的太丰盛了!!!", "emotion": "excited", "intensity": "medium" },
        { "text": "请务必在明天之前完成这份报告.", "emotion": "serious", "intensity": "medium" },
        { "text": "这次会议做出的决定非常的重要.", "emotion": "serious", "intensity": "medium" },
        { "text": "我希望大家都能端正自己的态度.", "emotion": "serious", "intensity": "medium" },
        { "text": "大家必须严格遵守公司的新规定.", "emotion": "serious", "intensity": "high" },
        { "text": "这棘手问题需要马上找解决方案.", "emotion": "serious", "intensity": "medium" },
        { "text": "我们绝对不能再犯同等低级错误.", "emotion": "serious", "intensity": "medium" },
        { "text": "请仔细核对这组重要的数据报表.", "emotion": "serious", "intensity": "high" },
        { "text": "目前的严峻形势对我们非常不利.", "emotion": "serious", "intensity": "high" },
        { "text": "大家必须同心协力才能渡过难关.", "emotion": "serious", "intensity": "high" },
        { "text": "我认为你提出的计划存在大风险.", "emotion": "serious", "intensity": "medium" },
        { "text": "请把所有相关的背景资料整理好.", "emotion": "serious", "intensity": "medium" },
        { "text": "这次重要合作需签订严保密协议.", "emotion": "serious", "intensity": "high" },
        { "text": "任何人都绝不能越过安全红底线.", "emotion": "serious", "intensity": "high" },
        { "text": "时间紧迫我们必须马上采取行动.", "emotion": "serious", "intensity": "high" },
        { "text": "对违规者公司将给予最严厉处罚.", "emotion": "serious", "intensity": "high" },
        { "text": "我需要你立刻给个非常合理解释.", "emotion": "serious", "intensity": "medium" },
        { "text": "事实已经证明你的推论完全错误.", "emotion": "serious", "intensity": "medium" },
        { "text": "我们必须要为接下来的挑战准备.", "emotion": "serious", "intensity": "medium" },
        { "text": "现在实际预算已远超我们的预期.", "emotion": "serious", "intensity": "medium" },
        { "text": "这件事情的严重性必须彻底搞清.", "emotion": "serious", "intensity": "high" },
        { "text": "请帮我把这份机密文件复印两份。", "emotion": "neutral", "intensity": "low" },
        { "text": "明天的天气预报说下午阵雨转晴。", "emotion": "neutral", "intensity": "low" },
        { "text": "今天下午三点在会议室有个例会。", "emotion": "neutral", "intensity": "low" },
        { "text": "我已经把最新文档发到工作邮箱。", "emotion": "neutral", "intensity": "low" },
        { "text": "附近这家超市营业时间到晚十点。", "emotion": "neutral", "intensity": "low" },
        { "text": "请在前面的第二个十字路口右转。", "emotion": "neutral", "intensity": "low" },
        { "text": "这本历史书的作者是位著名学者。", "emotion": "neutral", "intensity": "low" },
        { "text": "从这里慢慢走到前面地铁站十分。", "emotion": "neutral", "intensity": "low" },
        { "text": "我的手机电量快完全用完得充电。", "emotion": "neutral", "intensity": "low" },
        { "text": "冰箱里还有一些昨天晚上的沙拉。", "emotion": "neutral", "intensity": "low" },
        { "text": "今天的中午饭我想吃简单小面条。", "emotion": "neutral", "intensity": "low" },
        { "text": "你知道这附近有比较好的打印店？", "emotion": "neutral", "intensity": "low" },
        { "text": "请把你的正确联系方式填表上面。", "emotion": "neutral", "intensity": "low" },
        { "text": "下周的系统培训课程因故全取消。", "emotion": "neutral", "intensity": "low" },
        { "text": "这件漂亮衣服材质是百分百纯棉。", "emotion": "neutral", "intensity": "low" },
        { "text": "记得出门前把屋子里的电灯关掉。", "emotion": "neutral", "intensity": "low" },
        { "text": "麻烦你把桌子上的空水杯全收走。", "emotion": "neutral", "intensity": "low" },
        { "text": "这个软件需要更新到最新版能用。", "emotion": "neutral", "intensity": "low" },
        { "text": "刚才的固定电话是一位王总打来。", "emotion": "neutral", "intensity": "low" },
        { "text": "这趟拥挤公交车还需五分钟才到。", "emotion": "neutral", "intensity": "low" },
        { "text": "咦?! 你今天怎么没有去上班?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 这个玻璃水杯怎么碎了?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 太阳今天怎从西边出来?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 你的长发怎么突然剪短?!", "emotion": "surprised", "intensity": "medium" },
        { "text": "咦?! 房间怎么无故多出个人?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 他居然没来参加这聚会?!", "emotion": "surprised", "intensity": "medium" },
        { "text": "咦?! 这铁大门怎么打不开了?!", "emotion": "surprised", "intensity": "medium" },
        { "text": "咦?! 昨天买的红苹果都没有?!", "emotion": "surprised", "intensity": "low" },
        { "text": "咦?! 这是在什么时候发生事?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 我怎么把钥匙忘在家里?!", "emotion": "surprised", "intensity": "medium" },
        { "text": "咦?! 外面怎么突然下起大雨?!", "emotion": "surprised", "intensity": "medium" },
        { "text": "咦?! 这道超级难题竟然这么?!", "emotion": "surprised", "intensity": "medium" },
        { "text": "咦?! 你们俩到底啥时候认识?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 我的黑色旧钱包怎么没?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 这里怎么还有多余资料?!", "emotion": "surprised", "intensity": "medium" },
        { "text": "咦?! 神秘大包裹是谁寄给我?!", "emotion": "surprised", "intensity": "low" },
        { "text": "咦?! 刚才明明还放在桌子上?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 这种离奇事情怎么可能?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 大家都用这种眼神看我?!", "emotion": "surprised", "intensity": "medium" },
        { "text": "咦?! 他竟然能够考全班第一?!", "emotion": "surprised", "intensity": "high" },
        { "text": "啊... 好像闪过一个鬼影... ...", "emotion": "scared", "intensity": "high" },
        { "text": "啊... 废弃的地方好阴森... ...", "emotion": "scared", "intensity": "high" },
        { "text": "啊... 千万别过来赶紧走... ...", "emotion": "scared", "intensity": "high" },
        { "text": "啊... 真的好害怕黑夜晚... ...", "emotion": "scared", "intensity": "high" },
        { "text": "啊... 窗外那个雷声好响... ...", "emotion": "scared", "intensity": "medium" },
        { "text": "啊... 有毒蛇在前面爬行... ...", "emotion": "scared", "intensity": "high" },
        { "text": "啊... 悬崖边不敢往下看... ...", "emotion": "scared", "intensity": "high" },
        { "text": "啊... 半夜奇怪声音吓人... ...", "emotion": "scared", "intensity": "high" },
        { "text": "啊... 空荡的屋子里人哭... ...", "emotion": "scared", "intensity": "high" },
        { "text": "啊... 偏僻的小路实在黑... ...", "emotion": "scared", "intensity": "medium" },
        { "text": "啊... 总感觉后面好像人... ...", "emotion": "scared", "intensity": "high" },
        { "text": "啊... 大门锁好像被撬开... ...", "emotion": "scared", "intensity": "high" },
        { "text": "啊... 电影大怪物好丑陋... ...", "emotion": "scared", "intensity": "high" },
        { "text": "啊... 手电筒怎么没电了... ...", "emotion": "scared", "intensity": "high" },
        { "text": "啊... 不要把我一人丢下... ...", "emotion": "scared", "intensity": "high" },
        { "text": "啊... 气氛让人毛骨悚然... ...", "emotion": "scared", "intensity": "high" },
        { "text": "啊... 别再讲恐怖鬼故事... ...", "emotion": "scared", "intensity": "medium" },
        { "text": "啊... 黑暗中有东西抓我... ...", "emotion": "scared", "intensity": "high" },
        { "text": "啊... 老旧吊桥晃得厉害... ...", "emotion": "scared", "intensity": "high" },
        { "text": "啊... 那个黑影真吓死我... ...", "emotion": "scared", "intensity": "high" },
        { "text": "呃... 下水道味道真好臭... ...", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... 满地都是脏烂泥巴... ...", "emotion": "disgusted", "intensity": "medium" },
        { "text": "呃... 公厕环境真的恶心... ...", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... 桌子上剩饭早馊了... ...", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... 流浪汉不讲究卫生... ...", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... 这种场面大倒胃口... ...", "emotion": "disgusted", "intensity": "medium" },
        { "text": "呃... 垃圾桶绿头苍蝇飞... ...", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... 厨房垃圾几天没倒... ...", "emotion": "disgusted", "intensity": "medium" },
        { "text": "呃... 这种人真的不要脸... ...", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... 看见这东西就反胃... ...", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... 油腻脏盘子难忍受... ...", "emotion": "disgusted", "intensity": "medium" },
        { "text": "呃... 地上那么多恶心虫... ...", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... 他身上奇怪味难闻... ...", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... 衣服沾了好多污渍... ...", "emotion": "disgusted", "intensity": "medium" },
        { "text": "呃... 绝对不吃怪异的菜... ...", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... 踩到了一坨臭狗屎... ...", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... 烂死苹果里长出蛆... ...", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... 吃饭声音真惹讨厌... ...", "emotion": "disgusted", "intensity": "medium" },
        { "text": "呃... 脏乱差房间怎么住... ...", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... 令人作呕行为差劲... ...", "emotion": "disgusted", "intensity": "high" },
        { "text": "其实我一直默默地喜欢你... ", "emotion": "shy", "intensity": "low" },
        { "text": "这个小礼物是我亲手做的... ", "emotion": "shy", "intensity": "low" },
        { "text": "不知你愿不愿意和我交往... ", "emotion": "shy", "intensity": "medium" },
        { "text": "每次看到你都会红了脸颊... ", "emotion": "shy", "intensity": "high" },
        { "text": "其实我好久之前就想表白... ", "emotion": "shy", "intensity": "medium" },
        { "text": "可以的话想邀你看场电影... ", "emotion": "shy", "intensity": "low" },
        { "text": "和你单独一起时总是紧张... ", "emotion": "shy", "intensity": "medium" },
        { "text": "不知道为何不敢直视你眼... ", "emotion": "shy", "intensity": "medium" },
        { "text": "希望这份微薄心意能收下... ", "emotion": "shy", "intensity": "low" },
        { "text": "昨天梦到了我们牵手散步... ", "emotion": "shy", "intensity": "medium" },
        { "text": "只要远远看着你就很幸福... ", "emotion": "shy", "intensity": "low" },
        { "text": "我想成为能够保护你的人... ", "emotion": "shy", "intensity": "medium" },
        { "text": "听到你声音心跳不由加速... ", "emotion": "shy", "intensity": "high" },
        { "text": "这信里写满了对你的感情... ", "emotion": "shy", "intensity": "medium" },
        { "text": "如果被你拒绝肯定会难过... ", "emotion": "shy", "intensity": "medium" },
        { "text": "你的一颦一笑印在了脑海... ", "emotion": "shy", "intensity": "low" },
        { "text": "愿意陪你去做想做的事情... ", "emotion": "shy", "intensity": "medium" },
        { "text": "能够遇到你是我最大幸运... ", "emotion": "shy", "intensity": "medium" },
        { "text": "我会一直默默等你的回复... ", "emotion": "shy", "intensity": "low" },
        { "text": "希望未来的每天有你陪伴... ", "emotion": "shy", "intensity": "medium" }
    ]

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
