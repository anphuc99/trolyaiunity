import { In, type DataSource, type Repository } from "typeorm";
import LevelEntity from "../models/level.entity.js";
import VoiceEntity, { type VoiceModel } from "../models/voice.entity.js";

const DEFAULT_LEVELS: Array<Pick<LevelEntity, "level" | "maxWords" | "descript" | "guideline" | "vocabulary">> = [
  {
    level: "HSK1",
    maxWords: 5,
    descript: "Basic phrases for familiar topics.",
    guideline: "Use HSK1 grammar only. Focus on very simple sentence patterns such as 是, 有, 在, and basic greetings/questions. Keep sentences short and avoid complex complements.",
    vocabulary: "我, 我们, 你, 他, 她, 这 (这儿), 那 (那儿), 哪, 哪儿, 谁, 什么, 多少, 几, 怎么, 怎么样, 一, 二, 三, 四, 五, 六, 七, 八, 九, 十, 个, 岁, 本, 些, 块, 不, 没, 很, 太, 都, 一点儿, 和, 在, 的, 了, 吗, 呢, 喂, 家, 学校, 饭店, 商店, 医院, 中国, 北京, 上, 下, 前面, 后面, 里面, 今天, 明天, 昨天, 上午, 中午, 下午, 年, 月, 号, 星期, 点, 分钟, 现在, 时候, 爸爸, 妈妈, 儿子, 女儿, 老师, 学生, 同学, 朋友, 医生, 先生, 小姐, 衣服, 水, 菜, 米饭, 水果, 苹果, 茶, 杯子, 钱, 飞机, 出租车, 电视, 电脑, 电影, 天气, 猫, 狗, 东西, 人, 名字, 书, 汉语, 字, 桌子, 椅子, 谢谢, 不客气, 再见, 请, 对不起, 没关系, 是, 有, 看, 听, 说, 读, 写, 看见, 叫, 来, 回, 去, 吃, 喝, 睡觉, 打电话, 做, 买, 开, 坐, 住, 学习, 工作, 下雨, 爱, 喜欢, 想, 认识, 会, 能, 好, 大, 小, 多, 少, 冷, 热, 高兴, 漂亮"
  },
  {
    level: "HSK2",
    maxWords: 7,
    descript: "Simple conversation and routine tasks.",
    guideline:
      "Use HSK2 grammar. Allowed patterns include 因为...所以..., 虽然...但是..., 一边...一边..., 先...然后..., and common 了 usage. Avoid HSK3+ abstract constructions.",
    vocabulary: "您, 它, 大家, 每, 为什么, 零, 两, 百, 千, 第一, 次, 件, 别, 非常, 也, 还, 最, 真, 正在, 已经, 一起, 再, 就, 因为……所以……, 虽然……但是……, 从, 比, 往, 离, 得, 着, 过, 吧, 机场, 教室, 房间, 路, 左边, 右边, 外, 旁边, 一下, 早上, 晚上, 小时, 时间, 去年, 日, 生日, 哥哥, 姐姐, 弟弟, 妹妹, 丈夫, 妻子, 孩子, 男人, 女人, 服务员, 鱼, 羊肉, 牛奶, 鸡蛋, 西瓜, 咖啡, 雪, 药, 手机, 手表, 眼睛, 身体, 公共汽车, 报纸, 门, 题, 课, 姓, 问题, 事情, 考试, 票, 意思, 颜色, 铅笔, 面条, 火车站, 公司, 宾馆, 说话, 卖, 问, 走, 进, 出, 跑步, 到, 穿, 洗, 给, 找, 懂, 笑, 告诉, 准备, 开始, 介绍, 帮助, 玩, 送, 等, 让, 起床, 唱歌, 跳舞, 旅游, 上班, 生病, 休息, 运动, 游泳, 踢足球, 打篮球, 完, 觉得, 知到, 希望, 可以, 要, 可能, 高, 红, 白, 黑, 忙, 快, 慢, 远, 近, 好吃, 累, 长, 新, 贵, 便宜, 晴, 阴, 错, 快乐, 对, 对"
  },
  {
    level: "HSK3",
    maxWords: 10,
    descript: "Handle everyday situations and short texts.",
    guideline:
      "Use HSK3 grammar. Allowed patterns: 把/被 sentences, 越来越..., 除了...以外..., 只要...就..., 即使...也.... Keep sentences concise and avoid HSK4+ density.",
    vocabulary: "阿姨, 啊, 矮, 爱好, 安静, 把, 班, 搬, 半, 办法, 办公室, 帮忙, 包, 饱, 北方, 被, 鼻子, 比较, 比赛, 笔记本, 必须, 变化, 别人, 冰箱, 菜单, 参加, 草, 层, 差, 超市, 衬衫, 成绩, 城市, 迟ado, 除了, 船, 春, 词典, 聪明, 打扫, 打算, 带, 担心, 蛋糕, 当然, 地, 灯, 地方, 地铁, 地图, 电梯, 电子邮件, 东, 冬, 动物, 短, 段, 锻炼, 多么, 饿, 不dan……而且……, 耳朵, 发, 发烧, 发现, 方便, 放, 放心, 分, 附近, 复习, 干净, 感兴趣, 感冒, 刚才, 个子, 跟, 根据, 更, 公斤, 公园, 故事, 刮风, 关, 关系, 关心, 关于, 国家, 过去, 过（动词）, 还是, 害怕, 黑板, 后来, 护照, 花（动词）, 花（名词）, 画, 坏, 欢迎, 还（动词）, 环境, 换, 黄河, 回答, 会议, 或者, 几乎, 机会, 极, 记得, 季节, 检查, 简单, 健康, 见面, 讲, 教, 角, 脚, 接, 街道, 结婚, 结束, 节目, 节日, 解决, 借, 经常, 经过, 经理, 久, 旧, 句子, 决定, 渴, 可爱, 刻, 客人, 空调, 口, 哭, 裤子, 筷子, 蓝, 老, 离开, 礼物, 历史, 脸, 聊天, 练习, 辆, 了解, 邻居, 留学, 楼, 绿, 马, 马上, 满意, 帽子, 米, 面包, 明白, 拿, 奶奶, 南, 难, 难过, 年级, 年轻, 鸟, 努力, 爬山, 盘子, 胖, 啤酒, 皮鞋, 瓶子, 其实, 其他, 骑, 奇怪, 起来, 起飞, 清楚, 请假, 秋, 裙子, 然后, 热情, 认为, 认真, 容易, 如果, 伞, 上网, 生气, 声音, 试, 世界, 瘦, 舒服, 叔叔, 树, 数学, 刷牙, 双, 水平, 司机, 太阳, 特別, 疼, 提高, 体育, 甜, 条, 同事, 同意, 头发, 突然, 图书馆, 腿, 完成, 碗, 万, 忘记, 为, 为了, 位, 文化, 西, 习惯, 洗手间, 洗澡, 夏, 先, 香蕉, 相信, 向, 像, 小心, 校长, 新闻, 新鲜, 信用卡, 行李箱, 熊猫, 需要, 选择, 要求, 爷爷, 一定, 一共, 一会儿, 一样, 以前, 一般, 一边, 一直, 音乐, 银行, 饮料, 应该, 影响, 用, 游戏, 有名, 又, 遇到, 元, 愿意, 月亮, 越, 站, 张, 长（动词）, 着急, 照顾, 照片, 照相机, 只（量词）, 只（副词）, 只有……才……, 中文, 中间, 终于, 种, 重要, 周末, 主要, 注意, 自己, 自行车, 总是, 嘴, 最后, 最近, 作业"
  },
  {
    level: "HSK4",
    maxWords: 12,
    descript: "Discuss abstract topics with some fluency.",
    guideline: "Use HSK4 grammar to discuss opinions and abstract topics. Prefer clear logic markers such as 不仅...而且..., 与其...不如..., 既...又.... Keep replies concise.",
    vocabulary: "爱情, 安排, 安全, 按时, 按照, 百分之, 棒, 包子, 保护, 保证, 抱, 报名, 抱歉, 倍, 杯子, 笨, 鼻子, 毕业, 必须, 边, 便, 饼干, 比较, 部队, 菜, 材料, 参加, 草, 差不多, 超市, 炒, 吵, 超过, 成熟, 成绩, 承认, 城市, 吃惊, 厨房, 除了, 春, 词典, 打扫, 带, 单位, 胆小, 当地, 道歉, 得, 低, 电池, 电台, 电梯, 掉, 调查, 丢, 冬, 动物, 堵车, 肚子, 短信, 断, 段, 对, 对象, 反对, 放假, 房间, 房租, 方法, 方面, 访问, 方向, 烦恼, 翻译, 饭馆, 法律, 发展, 分, 分析, 分钟, 丰富, 风险, 否定, 服务员, 符合, 父亲, 复习, 负责, 复杂, 改变, 改进, 干, 干净, 感动, 感觉, 感谢, 敢, 刚, 钢笔, 高兴, 告诉, 哥哥, 歌, 隔壁, 给, 根, 根据, 更, 公共汽车, 工资, 功能, 工人, 共同, 狗, 够, 购物, 估计, 顾客, 故意, 故事, 挂, 关键, 关系, 关心, 关于, 管理, 光, 逛, 规定, 过程, 过去, 过, 害怕, 害羞, 海洋, 好, 好奇, 好像, 盒子, 合格, 合适, 合作, 黑板, 恨, 猴子, 后悔, 厚, 互相, 花, 划, 画, 坏, 还, 环境, 换, 幻想, 黄, 谎话, 会议, 活动, 活泼, 火, 获得, 或者, 机会, 鸡蛋, 几乎, 积极, 技术, 继续, 记录, 纪律, 纪念, 寄, 计划, 既然, 季节, 家, 家具, 价格, 假, 假期, 坚持, 检查, 简单, 健康, 见面, 建立, 建设, 建议, 将来, 讲, 降低, 交, 交流, 交通, 骄傲, 角, 脚, 饺子, 教, 教材, 教育, 接受, 节, 结果, 结婚, 结束, 解决, 借, 介绍, 尽管, 尽量, 紧张, 进步, 进入, 进行, 禁止, 精彩, 精神, 经济, 经历, 经验, 警察, 竞争, 竟然, 镜子, 酒, 救, 就, 旧, 举, 举办, 举行, 拒绝, 俱乐部, 距离, 聚会, 卡, 开, 开玩笑, 看, 看来, 烤, 考试, 棵, 科学, 可爱, 可能, 可惜, 渴, 刻, 客人, 课本, 课堂, 空间, 空气, 空, 恐怕, 口, 哭, 苦, 裤子, 快, 块, 筷子, 宽, 困, 困难, 扩大, 拉, 辣, 来, 蓝, 懒惰, 浪费, 浪漫, 老, 老板, 老师, 了, 雷, 冷, 梨, 礼拜, 礼貌, 礼物, 理想, 理解, 厉害, 历史, 力量, 联系, 连, 凉快, 亮, 辆, 聊, 了, 零, 领导, 另外, 留, 留学, 流, 流行, 六, 楼, 路, 乱, 律师, 绿, 麻烦, 马, 马上, 骂, 买, 卖, 满意, 毛, 帽子, 没, 没关系, 妹妹, 魅力, 门, 梦, 米, 迷路, 免费, 面, 面包, 面试, 民族, 母亲, 目的, 拿, 哪, 哪儿, 男, 难, 难过, 难看, 脑袋, 内, 内容, 能, 能力, 你, 年级, 年轻, 鸟, 努力, 爬, 怕, 拍, 派, 排, 盘子, 胖, 跑, 陪, 朋友, 批评, 皮, 啤酒, 篇, 骗, 便宜, 票, 漂亮, 瓶子, 平时, 苹果, 平安, 破, 葡萄, 普通, 普遍, 齐, 骑, 奇怪, 其实, 其他, 其次, 气, 汽车, 气氛, 起来, 千, 签, 前, 钱, 墙, 强, 桥, 巧, 悄悄, 敲, 且, 亲爱, 亲戚, 清楚, 轻, 情况, 请, 穷, 秋, 区别, 取, 去, 全部, 劝, 缺点, 缺少, 却, 确实, 圈, 权, 全, 完全, 让, 热, 热情, 人, 任何, 认识, 认真, 日, 日记, 容易, 如果, 如何, 入, 软, 弱, 伞, 散步, 森林, 沙, 沙发, 傻, 杀, 晒, 山, 闪电, 闪电, 善良, 伤心, 商人, 上, 稍微, 勺子, 蛇, 舌头, 舍不得, 社会, 设备, 射击, 摄影, 身, 身份, 深, 申请, 甚至, 声, 生活, 省, 剩, 失败, 失望, 湿, 十, 实际, 实在, 食物, 适合, 适应, 市场, 试, 收, 收入, 收拾, 手, 手机, 守, 受, 售货员, 瘦, 书, 舒服, 熟悉, 叔叔, 属于, 暑假, 鼠, 树, 数学, 刷牙, 顺便, 顺利, 顺序, 说明, 硕士, 死, 速度, 塑料袋, 酸, 算, 随便, 随着, 岁, 孙子, 所有, 他, 她, 它, 台, 态度, 太, 抬, 谈, 弹钢琴, 汤, 糖, 躺, 逃, 桃, 特别, 特点, 疼, 踢足球, 提, 提供, 提前, 题, 体贴, 体育, 天, 条, 条件, 跳舞, 听, 停, 挺, 通, 通过, 通知, 同, 同事, 同学, 同意, 头发, 投资, 透明, 突然, 图书馆, 腿, 推, 推迟, 脱, 袜子, 外, 完, 完成, 完全, 玩, 晚, 碗, 万, 忘记, 危险, 喂, 味道, 温度, 温暖, 文化, 文学, 文, 文章, 问, 问题, 我, 我们, 握手, 污染, 无, 无聊, 无论, 五, 误会, 洗, 喜欢, 西, 西瓜, 吸引, 习惯, 洗手间, 帅"
  },
  {
    level: "HSK5",
    maxWords: 15,
    descript: "Understand complex texts and express ideas.",
    guideline: "Use HSK5 grammar with nuanced connectors and occasional idiomatic expressions (成语) when natural. Maintain clarity and concise sentence flow.",
    vocabulary: "策略,判断,比较,细节,表达"
  },
  {
    level: "HSK6",
    maxWords: 20,
    descript: "Near-native understanding and expression.",
    guideline: "Use HSK6 near-native Chinese with precise register control. Keep responses concise, coherent, and pedagogically useful.",
    vocabulary: "语境,隐喻,推理,辩论,连贯"
  }
];

const LEGACY_LEVEL_ALIASES: Record<string, string[]> = {
  HSK1: ["A0", "A1"],
  HSK2: ["A2"],
  HSK3: ["B1"],
  HSK4: ["B2"],
  HSK5: ["C1"],
  HSK6: ["C2"]
};

export interface SeedLevelsResult {
  inserted: number;
  updated: number;
}

export interface SeedVoicesResult {
  inserted: number;
}

const DEFAULT_VOICES: Array<Pick<VoiceEntity, "model" | "voice">> = [
  { model: "openai", voice: "alloy" },
  { model: "openai", voice: "ballad" },
  { model: "openai", voice: "coral" },
  { model: "openai", voice: "cedar" },
  { model: "openai", voice: "echo" },
  { model: "openai", voice: "fable" },
  { model: "openai", voice: "marin" },
  { model: "openai", voice: "nova" },
  { model: "openai", voice: "onyx" },

  { model: "gemini", voice: "Zephyr" },
  { model: "gemini", voice: "Puck" },
  { model: "gemini", voice: "Charon" },
  { model: "gemini", voice: "Kore" },
  { model: "gemini", voice: "Fenrir" },
  { model: "gemini", voice: "Leda" },
  { model: "gemini", voice: "Orus" },
  { model: "gemini", voice: "Aoede" },
  { model: "gemini", voice: "Callirrhoe" },
  { model: "gemini", voice: "Autonoe" },
  { model: "gemini", voice: "Enceladus" },
  { model: "gemini", voice: "Iapetus" },
  { model: "gemini", voice: "Umbriel" },
  { model: "gemini", voice: "Algieba" },
  { model: "gemini", voice: "Despina" },
  { model: "gemini", voice: "Erinome" },
  { model: "gemini", voice: "Algenib" },
  { model: "gemini", voice: "Rasalgethi" },
  { model: "gemini", voice: "Laomedeia" },
  { model: "gemini", voice: "Achernar" },
  { model: "gemini", voice: "Alnilam" },
  { model: "gemini", voice: "Schedar" },
  { model: "gemini", voice: "Gacrux" },
  { model: "gemini", voice: "Pulcherrima" },
  { model: "gemini", voice: "Achird" },
  { model: "gemini", voice: "Zubenelgenubi" },
  { model: "gemini", voice: "Vindemiatrix" },
  { model: "gemini", voice: "Sadachbia" },
  { model: "gemini", voice: "Sadaltager" },
  { model: "gemini", voice: "Sulafat" }
];

const normalizeVoiceModel = (value: string): VoiceModel => {
  const lower = value.trim().toLowerCase();
  return lower === "gemini" ? "gemini" : "openai";
};

/**
 * Ensures the default HSK levels exist and are up to date.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns Counts of inserted/updated levels.
 */
export const seedDefaultLevels = async (dataSource: DataSource): Promise<SeedLevelsResult> => {
  const repository: Repository<LevelEntity> = dataSource.getRepository(LevelEntity);
  let inserted = 0;
  let updated = 0;

  for (const entry of DEFAULT_LEVELS) {
    let existing = await repository.findOne({ where: { level: entry.level } });

    if (!existing) {
      const legacyAliases = LEGACY_LEVEL_ALIASES[entry.level] ?? [];
      if (legacyAliases.length > 0) {
        existing = await repository.findOne({
          where: {
            level: In(legacyAliases)
          }
        });
      }
    }

    if (!existing) {
      await repository.save(repository.create(entry));
      inserted += 1;
      continue;
    }

    const nextDescript = entry.descript.trim();
    const nextGuideline = entry.guideline.trim();
    const nextVocabulary = entry.vocabulary.trim();
    const shouldUpdateDescript = existing.descript.trim() !== nextDescript;
    const shouldUpdateGuideline = (existing.guideline ?? "").trim() !== nextGuideline;
    const shouldUpdateVocabulary = (existing.vocabulary ?? "").trim() !== nextVocabulary;
    const shouldUpdateMaxWords = existing.maxWords !== entry.maxWords;

    if (shouldUpdateDescript || shouldUpdateGuideline || shouldUpdateVocabulary || shouldUpdateMaxWords) {
      await repository.save({
        ...existing,
        level: entry.level,
        descript: nextDescript,
        guideline: nextGuideline,
        vocabulary: nextVocabulary,
        maxWords: entry.maxWords
      });
      updated += 1;
    }
  }

  return { inserted, updated };
};

/**
 * Ensures the default TTS voice options exist.
 * Existing rows are kept intact and only missing rows are inserted.
 *
 * @param dataSource - Initialized TypeORM data source.
 * @returns Count of inserted voices.
 */
export const seedDefaultVoices = async (dataSource: DataSource): Promise<SeedVoicesResult> => {
  const repository: Repository<VoiceEntity> = dataSource.getRepository(VoiceEntity);
  let inserted = 0;

  for (const entry of DEFAULT_VOICES) {
    const model = normalizeVoiceModel(entry.model);
    const voice = entry.voice.trim();
    if (!voice) {
      continue;
    }

    const existing = await repository.findOne({
      where: {
        model,
        voice
      }
    });

    if (existing) {
      continue;
    }

    await repository.save(
      repository.create({
        model,
        voice
      })
    );
    inserted += 1;
  }

  return { inserted };
};
