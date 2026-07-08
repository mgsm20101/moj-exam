/** Shape of a per-attempt review returned by both the admin and candidate review endpoints. */
export interface AttemptReviewOption {
  id: string;
  text: string;
  isCorrect: boolean;
  wasSelected: boolean;
  displayOrder: number;
}

export interface AttemptReviewQuestion {
  attemptQuestionId: string;
  displayOrder: number;
  type: 'Mcq' | 'TrueFalse' | 'FillBlank';
  text: string;
  imageUrl: string | null;
  correctAnswerText: string | null;
  candidateAnswerText: string | null;
  selectedOptionId: string | null;
  wasAnswered: boolean;
  isCorrect: boolean;
  options: AttemptReviewOption[];
}

export interface AttemptReview {
  shown: boolean;
  candidateName: string;
  score: number;
  totalPoints: number;
  scorePercentage: number;
  passMarkPercentage: number;
  passed: boolean;
  questions: AttemptReviewQuestion[];
}
