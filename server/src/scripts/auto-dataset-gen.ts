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
const VOICES = ["Achernar"]; // Các giọng Gemini TTS "Puck", "Charon", "Fenrir", "Leda", 

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
        if (!fs.existsSync(voiceOutputDir)) fs.mkdirSync(voiceOutputDir, { recursive: true });

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
        { "text": "终于完成了这周的工作任务可以好好地休息了!", "emotion": "happy", "intensity": "high" },
        { "text": "没想到新开的餐厅味道这么好真是让人开心啊!", "emotion": "happy", "intensity": "high" },
        { "text": "收到你送的这份生日礼物我特别喜欢谢谢你啊!", "emotion": "happy", "intensity": "high" },
        { "text": "努力了这么久这次期末考试终于拿到了第一名!", "emotion": "happy", "intensity": "high" },
        { "text": "我们一家人好久没去海边度假了这次玩得很开心!", "emotion": "happy", "intensity": "medium" },
        { "text": "刚才买到了最后一份烤冷面今天的运气真是不错!", "emotion": "happy", "intensity": "low" },
        { "text": "看着阳台上种的花终于开出第一朵好有成就感啊!", "emotion": "happy", "intensity": "medium" },
        { "text": "能够和你一起散步吹吹晚风就是最幸福的事情了!", "emotion": "happy", "intensity": "medium" },
        { "text": "房贷终于全部还清了感觉身上的石头终于落下了!", "emotion": "happy", "intensity": "high" },
        { "text": "听说老板明天请大家喝下午茶这真是个好消息啊!", "emotion": "happy", "intensity": "low" },
        { "text": "狗狗学会了握手的新技能它实在是太聪明乖巧了!", "emotion": "happy", "intensity": "medium" },
        { "text": "周末没有任何安排可以在家里舒舒服服地躺着了!", "emotion": "happy", "intensity": "low" },
        { "text": "最喜欢的小说终于大结局了而且是非常完美结局!", "emotion": "happy", "intensity": "high" },
        { "text": "在旧外套口袋里发现一百块钱这简直就是小确幸!", "emotion": "happy", "intensity": "medium" },
        { "text": "刚出炉的蛋挞奶香味十足一口咬下去真的好满足!", "emotion": "happy", "intensity": "medium" },
        { "text": "今天的夕阳美得就像是一幅画让人忍不住想拍照!", "emotion": "happy", "intensity": "low" },
        { "text": "今天天气真不错和朋友去公园野餐真的是太棒了!", "emotion": "happy", "intensity": "medium" },
        { "text": "哇!这件限量版外套被我成功买到了太棒了!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇!那个超级大明星竟然出现在公司楼下了!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇!老板刚才宣布今年大家的年终奖翻倍了!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇!我最爱的球队在最后三秒成功绝杀对手!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇!妈妈终于同意明天带我去游乐园游玩了!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇!这款新出的智能手机拍照效果太清晰了!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇!你这次画的水彩风景图简直是大师级别!!!", "emotion": "excited", "intensity": "medium" },
        { "text": "哇!我们终于要坐飞机去欧洲旅行半个月了!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇!这家店的冰淇淋竟然有二十多种新口味!!!", "emotion": "excited", "intensity": "medium" },
        { "text": "哇!你今天做的红烧肉闻起来也太香喷喷了!!!", "emotion": "excited", "intensity": "medium" },
        { "text": "哇!抽奖系统居然显示我中了一辆全新汽车!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇!我刚才查到我被心仪的重点大学录取了!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇!这篇文章刚发出去一小时就有一万点赞!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇!没想到这道超级复杂的数学题被解开了!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇!朋友从远方给我寄来一整箱各种零食啊!!!", "emotion": "excited", "intensity": "medium" },
        { "text": "哇!男主向女主求婚的那个浪漫场景太感人!!!", "emotion": "excited", "intensity": "high" },
        { "text": "哇!这部新出动漫的动作打斗戏份燃到爆炸!!!", "emotion": "excited", "intensity": "high" },
        { "text": "... ... 养了十年的小猫昨晚离开我了。", "emotion": "sad", "intensity": "high" },
        { "text": "... ... 辛苦准备那么久的面试没通过。", "emotion": "sad", "intensity": "medium" },
        { "text": "... ... 看着他离开的背影我还是哭了。", "emotion": "sad", "intensity": "high" },
        { "text": "... ... 拼命努力却还是达不到你要求。", "emotion": "sad", "intensity": "high" },
        { "text": "... ... 新手机摔粉碎珍贵照片全丢了。", "emotion": "sad", "intensity": "medium" },
        { "text": "... ... 外面一直下大雨心情变得糟糕。", "emotion": "sad", "intensity": "low" },
        { "text": "... ... 我们现在变成了熟悉的陌生人。", "emotion": "sad", "intensity": "high" },
        { "text": "... ... 听闻奶奶突然生病我感到无助。", "emotion": "sad", "intensity": "high" },
        { "text": "... ... 辛辛苦苦写的报告因停电没了。", "emotion": "sad", "intensity": "high" },
        { "text": "... ... 钱包里没钱了现在的我只能叹气。", "emotion": "sad", "intensity": "low" },
        { "text": "... ... 约好过节他却说公司突然要加班。", "emotion": "sad", "intensity": "medium" },
        { "text": "... ... 电影男女主角没在一起真的遗憾。", "emotion": "sad", "intensity": "low" },
        { "text": "... ... 以为他懂我其实他根本就不在乎。", "emotion": "sad", "intensity": "high" },
        { "text": "... ... 偌大的城市竟没有我的容身之处。", "emotion": "sad", "intensity": "high" },
        { "text": "... ... 曾经许下的美好诺言就像个笑话。", "emotion": "sad", "intensity": "high" },
        { "text": "... ... 现在真的很想念你却没有理由找。", "emotion": "sad", "intensity": "high" },
        { "text": "... ... 深夜里的那种孤独感总是让人疲惫。", "emotion": "sad", "intensity": "medium" },
        { "text": "明明答应过不迟到的结果又让我等一小时!!!", "emotion": "angry", "intensity": "high" },
        { "text": "无良商家居然故意卖假货给我一定要投诉!!!", "emotion": "angry", "intensity": "high" },
        { "text": "凭什么所有的错都要我承担这实在不公平!!!", "emotion": "angry", "intensity": "high" },
        { "text": "请不要再对我指手画脚我有自己的判断力!!!", "emotion": "angry", "intensity": "high" },
        { "text": "买的新自行车停楼下十分钟竟然就被偷了!!!", "emotion": "angry", "intensity": "high" },
        { "text": "那个司机突然变道不打转向灯简直不要命!!!", "emotion": "angry", "intensity": "high" },
        { "text": "他怎么可以一直在背后这样造谣实在过分!!!", "emotion": "angry", "intensity": "high" },
        { "text": "对你忍耐到了极限请立刻从我的房间出去!!!", "emotion": "angry", "intensity": "high" },
        { "text": "楼上邻居半夜大声放音乐到底有没有公德心!!!", "emotion": "angry", "intensity": "medium" },
        { "text": "为什么你永远只顾自己从来不考虑别人感受!!!", "emotion": "angry", "intensity": "high" },
        { "text": "这么简单的工作都能搞砸你脑子里在想什么!!!", "emotion": "angry", "intensity": "high" },
        { "text": "你这种恶劣态度让我觉得不被尊重不能接受!!!", "emotion": "angry", "intensity": "high" },
        { "text": "说好一起分摊这笔房租你却打算拍屁股走人!!!", "emotion": "angry", "intensity": "high" },
        { "text": "餐厅服务员不仅把菜上错服务态度还很恶劣!!!", "emotion": "angry", "intensity": "medium" },
        { "text": "讨厌别人未经允许随便翻抽屉严重侵犯隐私!!!", "emotion": "angry", "intensity": "high" },
        { "text": "你这种毫无责任感的自私行为迟早害死大家!!!", "emotion": "angry", "intensity": "high" },
        { "text": "别再用敷衍的语气跟我说话我又不是个傻子!!!", "emotion": "angry", "intensity": "high" },
        { "text": "快点从安全通道撤离这栋大楼火势变大了!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "站在马路正中间干什么太危险赶紧给我回来!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "抓小偷啊大家快帮忙拦住前面那个黑衣劫匪!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "篮球赛只剩下最后十秒钟大家赶紧把球投出!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "我在这里顶着大太阳等了你三个小时快点啊!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "救命啊这里突然有人晕倒失去意识快叫医生!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "你们不要再为了这件小事吵架让我安静十分!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "千万别碰那个红色的危险按钮会引发大爆炸!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "里面的人立刻放下手中的武器双手抱头蹲下!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "之前提醒过多少遍了绝对别把垃圾扔楼道里!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "危险那辆大卡车已经完全失控大家赶快躲避!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "冲锋的战斗号角已经吹响前面的兄弟跟我冲!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "里面的歹徒仔细听着你已经被所有警察包围!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "别让嫌疑犯跑了大家快点把大门死死地关上!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "前面河水水流太急了千万不要冒险下水游泳!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "听不到我说话吗赶紧把音响的音乐音量调小!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "是谁让你没有命令擅自行动马上退回到原位!!!!!", "emotion": "shouting", "intensity": "high" },
        { "text": "呃... ... 墙角爬出的那只大蟑螂太恶心了。", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... ... 下水道的脏水漫出来味道很难受。", "emotion": "disgusted", "intensity": "medium" },
        { "text": "呃... ... 这碗带泥腥味的鱼汤我喝不下去。", "emotion": "disgusted", "intensity": "low" },
        { "text": "呃... ... 他吃饭时吧唧嘴的吃相真倒胃口。", "emotion": "disgusted", "intensity": "medium" },
        { "text": "呃... ... 那大叔竟在公共场所随地吐痰啊。", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... ... 放在桌子三天的面包长满绿霉菌。", "emotion": "disgusted", "intensity": "low" },
        { "text": "呃... ... 公共厕所墙壁上的污垢让人反胃。", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... ... 恐怖电影里的变异怪物实在惊悚。", "emotion": "disgusted", "intensity": "medium" },
        { "text": "呃... ... 他身上的汗臭味熏得我睁不开眼。", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... ... 刚端上的盘子里居然有蠕动绿虫。", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... ... 那种极度虚伪讨好的嘴脸看不下。", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... ... 他说话满嘴喷口水差点弄我脸上。", "emotion": "disgusted", "intensity": "medium" },
        { "text": "呃... ... 那件满是油污没洗的衣服我绝不穿。", "emotion": "disgusted", "intensity": "medium" },
        { "text": "呃... ... 剩菜乱七八糟混在一起令人作呕。", "emotion": "disgusted", "intensity": "high" },
        { "text": "呃... ... 他当众挖鼻孔瞬间让我不想理他。", "emotion": "disgusted", "intensity": "medium" },
        { "text": "呃... ... 不小心踩到草丛里臭气熏天的狗屎。", "emotion": "disgusted", "intensity": "medium" },
        { "text": "呃... ... 这种极其油腻的搭讪方式让人无语。", "emotion": "disgusted", "intensity": "medium" },
        { "text": "啊... ... 一道巨大闪电突然劈在窗旁树上。", "emotion": "scared", "intensity": "medium" },
        { "text": "啊... ... 那个黑漆漆旧衣柜里有东西乱动。", "emotion": "scared", "intensity": "high" },
        { "text": "啊... ... 那个拿刀的恐怖面具男正朝我走。", "emotion": "scared", "intensity": "high" },
        { "text": "啊... ... 突然停电楼道黑漆漆的我不敢下。", "emotion": "scared", "intensity": "low" },
        { "text": "啊... ... 后面那只极其凶恶大黑狗在追我。", "emotion": "scared", "intensity": "high" },
        { "text": "啊... ... 晚上我一个人不敢看这血腥鬼片。", "emotion": "scared", "intensity": "medium" },
        { "text": "啊... ... 刚才看到窗外诡异飘过白色影子。", "emotion": "scared", "intensity": "high" },
        { "text": "啊... ... 运行中电梯突然停半空剧烈摇晃。", "emotion": "scared", "intensity": "high" },
        { "text": "啊... ... 我有严重恐高症不敢低头看悬崖。", "emotion": "scared", "intensity": "medium" },
        { "text": "啊... ... 那条带有剧毒青蛇吐信子靠近我。", "emotion": "scared", "intensity": "high" },
        { "text": "啊... ... 家里门锁被暴力撬开可能有小偷。", "emotion": "scared", "intensity": "high" },
        { "text": "啊... ... 黑暗森林深处传来几声凄厉狼叫。", "emotion": "scared", "intensity": "high" },
        { "text": "啊... ... 游乐园里的恐怖鬼屋布置太吓人。", "emotion": "scared", "intensity": "medium" },
        { "text": "啊... ... 手术室门打开医生摇头我好害怕。", "emotion": "scared", "intensity": "high" },
        { "text": "啊... ... 汽车刹车突然失灵现在的车速快。", "emotion": "scared", "intensity": "high" },
        { "text": "啊... ... 这座废弃破旧医院到处阴森恐怖。", "emotion": "scared", "intensity": "medium" },
        { "text": "啊... ... 悬崖边的岩石松动我快掉下去了。", "emotion": "scared", "intensity": "high" },
        { "text": "咦?! 昨天不是刚说你今天必须去北京出差吗?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 刚才还放在桌上的草莓蛋糕怎么不见了?!", "emotion": "surprised", "intensity": "medium" },
        { "text": "咦?! 办公室钥匙刚放口袋怎么现在摸不到了?!", "emotion": "surprised", "intensity": "medium" },
        { "text": "咦?! 一直是长头发怎么今天突然剪成短发了?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 平时一直很安静的鹦鹉今天突然唱歌了?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 那个向来抠门的老板竟主动提出加薪水?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 昨晚洗干净晾阳台的衣服早上竟结冰了?!", "emotion": "surprised", "intensity": "medium" },
        { "text": "咦?! 你穿的这件新款外套跟我买的竟是同款?!", "emotion": "surprised", "intensity": "medium" },
        { "text": "咦?! 从来不吃辣的你今天怎么点大碗麻辣烫?!", "emotion": "surprised", "intensity": "medium" },
        { "text": "咦?! 明明用完全一样的公式答案为什么不同?!", "emotion": "surprised", "intensity": "low" },
        { "text": "咦?! 刚才明明还站在角落里怎么转眼消失了?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 手机连接充电器充了一整晚怎么没有电?!", "emotion": "surprised", "intensity": "medium" },
        { "text": "咦?! 楼下买的大西瓜切开后里面竟没有颗籽?!", "emotion": "surprised", "intensity": "medium" },
        { "text": "咦?! 那棵枯萎整整三年的老树今年竟发芽了?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 这种天文现象真少见天上竟有两个月亮?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 记得已锁在抽屉的日记本怎么放在桌上?!", "emotion": "surprised", "intensity": "high" },
        { "text": "咦?! 这杯咖啡刚才加了三大勺糖怎么还是苦?!", "emotion": "surprised", "intensity": "medium" },
        { "text": "... 其实我一个人在心里默默喜欢你有三年了。", "emotion": "shy", "intensity": "high" },
        { "text": "... 那个如果不介意的话能给我留联系方式吗？", "emotion": "shy", "intensity": "medium" },
        { "text": "... 今天为了见你特意换上这件漂亮粉色裙子。", "emotion": "shy", "intensity": "high" },
        { "text": "... 每次跟你单独走在一起我的心跳就特别快。", "emotion": "shy", "intensity": "high" },
        { "text": "... 谢谢你刚才夸奖我漂亮弄得我有点不好意思。", "emotion": "shy", "intensity": "medium" },
        { "text": "... 你昨天送我的那盒手工巧克力我很喜欢谢谢。", "emotion": "shy", "intensity": "medium" },
        { "text": "... 刚才在走廊和你眼神对视的那一秒我脸红了。", "emotion": "shy", "intensity": "high" },
        { "text": "... 这条围巾是我亲手织的希望你能收下它。", "emotion": "shy", "intensity": "medium" },
        { "text": "... 那封情书是我趁你不在时悄悄放进课桌里的。", "emotion": "shy", "intensity": "high" },
        { "text": "... 我一直低着头不敢看你生怕被发现小心思。", "emotion": "shy", "intensity": "high" },
        { "text": "... 和你过马路牵手的时候因为太紧张手心全汗。", "emotion": "shy", "intensity": "high" },
        { "text": "... 如果周末有空能和我一起去电影院看电影吗？", "emotion": "shy", "intensity": "medium" },
        { "text": "... 大家都旁边不停起哄弄得我不知道该说什么。", "emotion": "shy", "intensity": "medium" },
        { "text": "... 虽然我很想主动和你搭话最后还是退缩了。", "emotion": "shy", "intensity": "high" },
        { "text": "... 刚才你在阳光下温柔笑起来的样子让我心动。", "emotion": "shy", "intensity": "high" },
        { "text": "... 其实我在感情方面不太会表达但我很在乎你。", "emotion": "shy", "intensity": "medium" },
        { "text": "嗯... 无论未来发生什么困难我都会永远陪着你。", "emotion": "affectionate", "intensity": "high" },
        { "text": "嗯... 亲爱的今天在公司上班辛苦了吧让我抱抱。", "emotion": "affectionate", "intensity": "medium" },
        { "text": "嗯... 只要能静静靠在你肩膀上我就感到很踏实。", "emotion": "affectionate", "intensity": "medium" },
        { "text": "嗯... 天气变冷了出门多穿衣服不然我会心疼的。", "emotion": "affectionate", "intensity": "high" },
        { "text": "嗯... 回家路上要注意安全我做好了晚饭在等你。", "emotion": "affectionate", "intensity": "medium" },
        { "text": "嗯... 看着你安静睡着的样子真想偷偷亲你一口。", "emotion": "affectionate", "intensity": "high" },
        { "text": "嗯... 不用担心未来因为只要我们拥有彼此足够。", "emotion": "affectionate", "intensity": "high" },
        { "text": "嗯... 我特意给你熬了最喜欢的鸡汤赶紧趁热喝。", "emotion": "affectionate", "intensity": "medium" },
        { "text": "嗯... 无论世界怎么改变我对你的真心永远不变。", "emotion": "affectionate", "intensity": "high" },
        { "text": "嗯... 遇见你就是最好缘分是我收到最珍贵礼物。", "emotion": "affectionate", "intensity": "high" },
        { "text": "嗯... 只要能看到你开心笑容我觉得付出都值得。", "emotion": "affectionate", "intensity": "high" },
        { "text": "嗯... 快过来让我紧紧抱抱你把烦恼全都交给我。", "emotion": "affectionate", "intensity": "medium" },
        { "text": "嗯... 只要有你一直陪伴身边多苦的日子也很甜。", "emotion": "affectionate", "intensity": "high" },
        { "text": "嗯... 你的双手怎么这么冷快放进怀里帮你捂热。", "emotion": "affectionate", "intensity": "medium" },
        { "text": "嗯... 周末天气好的话就陪你去一直想去的海边。", "emotion": "affectionate", "intensity": "low" },
        { "text": "嗯... 乖乖别难过哭了我会保证永远挡前面保护。", "emotion": "affectionate", "intensity": "high" },
        { "text": ". 公司季度的财务报表审查存在严重弄虚作假问题。", "emotion": "serious", "intensity": "high" },
        { "text": ". 必须清楚明白违反公司保密规定后果是非常严重。", "emotion": "serious", "intensity": "high" },
        { "text": ". 当前面临的市场竞争极大绝对不能有任何的松懈。", "emotion": "serious", "intensity": "high" },
        { "text": ". 这次造成重大安全事故相关责任人必须受到严惩。", "emotion": "serious", "intensity": "high" },
        { "text": ". 项目交付期限是本周五绝对不允许有任何延误。", "emotion": "serious", "intensity": "high" },
        { "text": ". 请大家集中注意力接下来的战略会议非常关键。", "emotion": "serious", "intensity": "medium" },
        { "text": ". 作为团队主要负责人你需要为这次失败承担全责。", "emotion": "serious", "intensity": "high" },
        { "text": ". 投资市场的风险极大做任何重要决定前必须深思。", "emotion": "serious", "intensity": "medium" },
        { "text": ". 底线是不接受任何妥协核心原则问题绝不退让。", "emotion": "serious", "intensity": "high" },
        { "text": ". 这份重要合同的每个条款都要经过法务仔细审核。", "emotion": "serious", "intensity": "medium" },
        { "text": ". 关系到职业生涯的严肃谈话请端正态度好好听着。", "emotion": "serious", "intensity": "high" },
        { "text": ". 为避免再次发生数据泄漏安全系统必须全面升级。", "emotion": "serious", "intensity": "high" },
        { "text": ". 昨天提交的运营方案存在明显漏洞需要重新修改。", "emotion": "serious", "intensity": "medium" },
        { "text": ". 无论前进道路困难有多大公司的核心目标不改变。", "emotion": "serious", "intensity": "high" },
        { "text": ". 实验室对比测试数据必须在今晚十二点前汇总好。", "emotion": "serious", "intensity": "medium" },
        { "text": ". 既然已经是成年人就要学会为自己说的蠢话负责。", "emotion": "serious", "intensity": "high" },
        { "text": "帮我把桌上的那份重要会议文件拿去复印三份吧。", "emotion": "neutral", "intensity": "low" },
        { "text": "午餐你是打算在食堂吃面条还是去楼下买个快餐？", "emotion": "neutral", "intensity": "low" },
        { "text": "麻烦你傍晚下班的时候顺路去超市买点新鲜蔬菜。", "emotion": "neutral", "intensity": "low" },
        { "text": "外面好像马上就要下雨出门记得带上柜里的黑伞。", "emotion": "neutral", "intensity": "low" },
        { "text": "家里这个月的宽带上网费我已提前用手机交过了。", "emotion": "neutral", "intensity": "low" },
        { "text": "他平时没有特别爱好喜欢周末去图书馆看历史书。", "emotion": "neutral", "intensity": "low" },
        { "text": "这班公交车刚开走我们还要等大概十五分钟才到。", "emotion": "neutral", "intensity": "low" },
        { "text": "刚才快递员打电话说把大包裹放在门口消防栓了。", "emotion": "neutral", "intensity": "low" },
        { "text": "会议记录仔细整理好后记得发一份到我公司邮箱。", "emotion": "neutral", "intensity": "low" },
        { "text": "如果明天天气比较晴朗一家人打算去郊外去爬山。", "emotion": "neutral", "intensity": "low" },
        { "text": "干洗店的老板告诉我他们明天上午十点正式开门。", "emotion": "neutral", "intensity": "low" },
        { "text": "昨晚睡觉的时候忘关窗户了今天早上起来觉得冷。", "emotion": "neutral", "intensity": "low" },
        { "text": "这牌子的洗衣液去污效果不错超市卖的价格合理。", "emotion": "neutral", "intensity": "low" },
        { "text": "厨房调料罐的盐快用完了记得去菜市场买包大盐。", "emotion": "neutral", "intensity": "low" },
        { "text": "现在手机电量只剩下百分之十得赶快找地方充电。", "emotion": "neutral", "intensity": "low" },
        { "text": "前面的马路正在修路所有的过往车辆都需要绕行。", "emotion": "neutral", "intensity": "low" }
    ]

    if (scripts && scripts.length > 0) {
        await synthesizeAudioAndSave(scripts);
        console.log(`\n[+] HOÀN TẤT! Dữ liệu đã lưu tại '${OUTPUT_DIR}'`);
        console.log(`[+] File cấu hình GPT-SoVITS: '${LIST_FILE}'`);
        console.log(`[+] Các file âm thanh mẫu (để làm mồi cảm xúc) nằm ở '${REFERENCE_DIR}'`);
    } else {
        console.log("\n[!] Không có kịch bản để tạo âm thanh.");
    }
}

main();
