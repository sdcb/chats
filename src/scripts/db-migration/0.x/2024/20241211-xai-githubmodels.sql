insert into ModelProvider values(11, 'x.ai', NULL, 'xai-yourkey');
insert into ModelProvider values(12, 'Github Models', NULL, 'ghp_yourkey');
update ModelProvider set InitialSecret = '' where id = 8;

insert into ModelReference values
(1100, 11, 'grok-beta',        'grok', 0, 0, 2, 0, 0, 1, 1, 131072, 4096, NULL, 5, 15, 'USD'),
(1101, 11, 'grok-vision-beta', 'grok', 0, 0, 2, 0, 1, 1, 1, 8192,   4096, NULL, 5, 15, 'USD'),
(1200, 12, 'AI21-Jamba-1.5-Large', 'AI21-Jamba', 0, 0, 2, 0, 0, 1, 1, 256000, 4000, NULL, 5, 15, 'USD'),
(1201, 12, 'AI21-Jamba-1.5-Mini',  'AI21-Jamba', 0, 0, 2, 0, 0, 1, 1, 256000, 4000, NULL, 1, 2, 'USD'),
(1202, 12, 'Cohere-command-r',      NULL, 0, 0, 2, 0, 0, 1, 1, 131072, 4096, NULL, 1, 2, 'USD'),
(1203, 12, 'Cohere-command-r-plus', NULL, 0, 0, 2, 0, 0, 1, 1, 131072, 4096, NULL, 0, 0, 'USD'),
(1204, 12, 'Llama-3.2-11B-Vision-Instruct', 'LLaMA', 0, 0, 2, 0, 1, 1, 1, 128000, 4000, NULL, 1,   2, 'USD'),
(1205, 12, 'Llama-3.2-90B-Vision-Instruct', 'LLaMA', 0, 0, 2, 0, 0, 1, 1, 128000, 4000, NULL, 5,   15, 'USD'),
(1206, 12, 'Meta-Llama-3.1-405B-Instruct',  'LLaMA', 0, 0, 2, 0, 0, 1, 1, 131072, 4096, NULL, 5,   15, 'USD'),
(1207, 12, 'Meta-Llama-3.1-70B-Instruct',   'LLaMA', 0, 0, 2, 0, 0, 1, 1, 131072, 4096, NULL, 2.5, 5, 'USD'),
(1208, 12, 'Meta-Llama-3.1-8B-Instruct',    'LLaMA', 0, 0, 2, 0, 0, 1, 1, 131072, 4096, NULL, 1,   2, 'USD'),
(1209, 12, 'Mistral-large',            'Mistral', 1, 0, 2, 0, 0, 1, 0, 32768,  4096,  NULL, 5, 15, 'USD'),
(1210, 12, 'Mistral-large-2407',       'Mistral', 0, 0, 2, 0, 0, 1, 0, 131072, 4096,  NULL, 5, 15, 'USD'),
(1211, 12, 'Mistral-Nemo',             'Mistral', 0, 0, 2, 0, 0, 1, 0, 131072, 4096,  NULL, 1, 2,  'USD'),
(1212, 12, 'Mistral-small',            'Mistral', 1, 0, 2, 0, 0, 1, 0, 32768,  4096,  NULL, 1, 2,  'USD'),
(1213, 12, 'gpt-4o',                   NULL,      0, 0, 2, 0, 1, 1, 1, 128000, 4096,  2, 5, 15, 'USD'),
(1214, 12, 'gpt-4o-mini',              NULL,      0, 0, 2, 0, 1, 1, 1, 128000, 4096,  2, 1, 2, 'USD'),
(1215, 12, 'Phi-3.5-MoE-instruct',    'Phi-3.5',  0, 0, 2, 0, 0, 1, 1, 131072, 4096,  NULL, 1, 2, 'USD'),
(1216, 12, 'Phi-3.5-mini-instruct',   'Phi-3.5',  0, 0, 2, 0, 0, 1, 1, 131072, 4096,  NULL, 0.5, 1, 'USD'),
(1217, 12, 'Phi-3.5-vision-instruct', 'Phi-3.5',  0, 0, 2, 0, 1, 1, 1, 131072, 4096,  NULL, 1, 2, 'USD'),
(1218, 12, 'o1-preview',              NULL,       0, 1, 1, 0, 0, 0, 0, 128000, 32768, NULL, 15, 60, 'USD'),
(1219, 12, 'o1-mini',                 NULL,       0, 1, 1, 0, 0, 0, 0, 128000, 65536, NULL, 3,  12, 'USD');
