import { ChatRole, Content, Role } from '../chat';

export interface PropsMessage {
  id: string;
  role: ChatRole;
  content: Content[];
  inputTokens: number;
  outputTokens: number;
  reasoningTokens: number;
  reasoningDuration: number;
  inputPrice: number;
  outputPrice: number;
  duration: number;
  firstTokenLatency: number;
}
