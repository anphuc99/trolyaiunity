import { DataSource, Repository, LessThanOrEqual, Not, IsNull } from "typeorm";
import VocabularyEntity from "../models/vocabulary.entity.js";

export class VocabularyProducer {
  private vocabRepo: Repository<VocabularyEntity>;

  constructor(private dataSource: DataSource) {
    this.vocabRepo = this.dataSource.getRepository(VocabularyEntity);
  }

  /**
   * Lấy danh sách từ vựng đã đến hạn ôn tập của một người dùng.
   * @param userId - ID của người dùng
   * @param limit - (Tùy chọn) Giới hạn số lượng từ trả về trong 1 lần học
   * @returns Danh sách VocabularyEntity đã đến hạn
   */
  async getDueVocabularies(userId: number, limit: number = 20): Promise<VocabularyEntity[]> {
    const now = new Date();

    // Tìm các từ vựng đã đến hạn dựa trên nextReviewDate <= thời gian hiện tại
    return this.vocabRepo.find({
      where: {
        userId,
        isIgnored: false,
        nextReviewDate: LessThanOrEqual(now) as unknown as Date,
      },
      order: {
        // Ưu tiên ôn tập những từ đã quá hạn lâu nhất trước
        nextReviewDate: "ASC",
      },
      take: limit,
    });
  }
}