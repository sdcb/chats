export interface UserModelConfig {
  prompt: string | null;
  temperature: number | null;
  webSearchEnabled: boolean | null;
}

export enum DBModelProvider {
  Test = 0,
  AzureAIFoundry = 1,
  HunYuan = 2,
  LingYi = 3,
  Moonshot = 4,
  OpenAI = 5,
  QianFan = 6,
  QianWen = 7,
  Spark = 8,
  ZhiPuAI = 9,
  DeepSeek = 10,
  X_AI = 11,
  GithubModels = 12,
  GoogleAI = 13,
  Ollama = 14,
  MiniMax = 15,
  Doubao = 16,
  SiliconFlow = 17,
  OpenRouter = 18,
  TokenPony = 19,
  Anthropic = 20,
  Mimo = 21,
}

export type FEModelProvider = {
  id: number;
  name: string;
  icon: string;
  allowCodeExecute?: boolean;
  allowWebSearch?: boolean;
};

export const feModelProviders: FEModelProvider[] = [
  { id: DBModelProvider.Test, name: 'Test', icon: '/icons/logo.png' },
  {
    id: DBModelProvider.AzureAIFoundry,
    name: 'Azure AI Foundry',
    icon: '/logos/azure-ai-foundry.svg',
  },
  {
    id: DBModelProvider.HunYuan,
    name: 'Tencent Hunyuan',
    icon: '/logos/hunyuan.svg',
    allowWebSearch: true,
  },
  { id: DBModelProvider.LingYi, name: '01.ai', icon: '/logos/lingyi.svg' },
  {
    id: DBModelProvider.Moonshot,
    name: 'Moonshot',
    icon: '/logos/moonshot.svg',
  },
  { id: DBModelProvider.OpenAI, name: 'OpenAI', icon: '/logos/openai.svg' },
  {
    id: DBModelProvider.QianFan,
    name: 'Wenxin Qianfan',
    icon: '/logos/qianfan.svg',
  },
  {
    id: DBModelProvider.QianWen,
    name: 'DashScope',
    icon: '/logos/qianwen.svg',
    allowWebSearch: true,
  },
  {
    id: DBModelProvider.Spark,
    name: 'Xunfei SparkDesk',
    icon: '/logos/spark.svg',
  },
  {
    id: DBModelProvider.ZhiPuAI,
    name: 'Zhipu AI',
    icon: '/logos/zhipuai.svg',
    allowWebSearch: true,
  },
  {
    id: DBModelProvider.DeepSeek,
    name: 'DeepSeek',
    icon: '/logos/deepseek.svg',
  },
  {
    id: DBModelProvider.X_AI,
    name: 'x.ai',
    icon: '/logos/xai.svg',
    allowWebSearch: true,
  },
  {
    id: DBModelProvider.GithubModels,
    name: 'Github Models',
    icon: '/logos/github.svg',
  },
  {
    id: DBModelProvider.GoogleAI,
    name: 'Google AI',
    icon: '/logos/google-ai.svg',
    allowCodeExecute: true,
    allowWebSearch: true,
  },
  { id: DBModelProvider.Ollama, name: 'Ollama', icon: '/logos/ollama.svg' },
  { id: DBModelProvider.MiniMax, name: 'MiniMax', icon: '/logos/minimax.svg' },
  { id: DBModelProvider.Doubao, name: 'Doubao', icon: '/logos/doubao.svg' },
  {
    id: DBModelProvider.SiliconFlow,
    name: 'SiliconFlow',
    icon: '/logos/siliconflow.svg',
  },
  {
    id: DBModelProvider.OpenRouter,
    name: 'OpenRouter',
    icon: '/logos/openrouter.svg',
    allowWebSearch: true,
  },
  {
    id: DBModelProvider.TokenPony,
    name: 'Token Pony',
    icon: '/logos/tokenpony.svg',
  },
  {
    id: DBModelProvider.Anthropic,
    name: 'Anthropic',
    icon: '/logos/anthropic.svg',
    allowWebSearch: true,
  },
  {
    id: DBModelProvider.Mimo,
    name: 'Xiaomi Mimo',
    icon: 'https://platform.xiaomimimo.com/favicon.874c9507.png',
  },
];

export interface ChatModelFileConfig {
  maxSize: number;
  count: number;
}

export interface ChatModelPriceConfig {
  input: number;
  out: number;
}
