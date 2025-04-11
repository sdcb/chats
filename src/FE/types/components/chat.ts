import { ChatRole, ResponseContent, Role } from '../chat';

export interface PropsMessage {
  id: string;
  role: ChatRole;
  content: ResponseContent[];
  inputTokens: number;
  outputTokens: number;
  reasoningTokens: number;
  reasoningDuration: number;
  inputPrice: number;
  outputPrice: number;
  duration: number;
  firstTokenLatency: number;
}
