import { DataSource, Repository, LessThanOrEqual } from "typeorm";
import VocabularyEntity from "../models/vocabulary.entity.js";
import VocabularyReviewEntity from "../models/vocabulary-review.entity.js";

export class VocabularyProducer {
  private reviewRepo: Repository<VocabularyReviewEntity>;

  constructor(private dataSource: DataSource) {
    // Khởi tạo repository từ TypeORM DataSource
    this.reviewRepo = this.dataSource.getRepository(VocabularyReviewEntity);
  }

  /**
   * Lấy danh sách từ vựng đã đến hạn ôn tập của một người dùng.
   * * @param userId - ID của người dùng
   * @param limit - (Tùy chọn) Giới hạn số lượng từ trả về trong 1 lần học
   * @returns Danh sách VocabularyEntity đã đến hạn
   */
  async getDueVocabularies(userId: number, limit: number = 20): Promise<VocabularyEntity[]> {
    const now = new Date();

    // Tìm các review đã đến hạn dựa trên nextReviewDate <= thời gian hiện tại
    const dueReviews = await this.reviewRepo.find({
      where: {
        userId: userId,
        nextReviewDate: LessThanOrEqual(now),
      },
      relations: {
        // Tải kèm thông tin từ vựng qua relation đã định nghĩa ở VocabularyReviewEntity
        vocabulary: true, 
      },
      order: {
        // Ưu tiên ôn tập những từ đã quá hạn lâu nhất trước
        nextReviewDate: "ASC",
      },
      take: limit, // Giới hạn số lượng trả về để tránh quá tải nếu user dồn đọng quá nhiều
    });

    // Trích xuất và chỉ trả về mảng các đối tượng VocabularyEntity
    return dueReviews.map((review) => review.vocabulary);
  }
}