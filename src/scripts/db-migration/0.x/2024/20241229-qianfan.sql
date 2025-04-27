-- 更新 Name 和 ShortName
UPDATE ModelReference
SET Name = 'ernie-4.0-turbo-8k', ShortName = 'ERNIE-4.0-Turbo-8K'
WHERE Name = 'ERNIE-4.0-Turbo-8K';

UPDATE ModelReference
SET Name = 'completions_pro', ShortName = 'ERNIE-4.0-8K'
WHERE Name = 'ERNIE-4.0-8K';

UPDATE ModelReference
SET Name = 'completions', ShortName = 'ERNIE-3.5-8K'
WHERE Name = 'ERNIE-3.5-8K';

UPDATE ModelReference
SET Name = 'ernie-3.5-128k', ShortName = 'ERNIE-3.5-128K'
WHERE Name = 'ERNIE-3.5-128K';

UPDATE ModelReference
SET Name = 'ernie-speed-pro-128k', ShortName = 'ERNIE-Speed-Pro-128K'
WHERE Name = 'ERNIE-Speed-Pro-128K';

UPDATE ModelReference
SET Name = 'ernie-novel-8k', ShortName = 'ERNIE-Novel-8K'
WHERE Name = 'ERNIE-Novel-8K';

UPDATE ModelReference
SET Name = 'ernie-speed-128k', ShortName = 'ERNIE-Speed-128K'
WHERE Name = 'ERNIE-Speed-128K';

UPDATE ModelReference
SET Name = 'ernie_speed', ShortName = 'ERNIE-Speed-8K'
WHERE Name = 'ERNIE-Speed-8K';

UPDATE ModelReference
SET Name = 'ernie-lite-8k', ShortName = 'ERNIE-Lite-8K'
WHERE Name = 'ERNIE-Lite-8K';

UPDATE ModelReference
SET Name = 'ernie-tiny-128k', ShortName = 'ERNIE-Tiny-128K'
WHERE Name = 'ERNIE-Tiny-128K';

UPDATE ModelReference
SET Name = 'ernie-tiny-8k', ShortName = 'ERNIE-Tiny-8K'
WHERE Name = 'ERNIE-Tiny-8K';

UPDATE ModelReference
SET Name = 'ernie-char-fiction-8k', ShortName = 'ERNIE-Character-Fiction-8K'
WHERE Name = 'ERNIE-Character-Fiction-8K';

UPDATE ModelReference
SET Name = 'ernie-func-8k', ShortName = 'ERNIE-Functions-8K'
WHERE Name = 'ERNIE-Functions-8K';

UPDATE ModelReference
SET Name = 'ernie-lite-pro-128k', ShortName = 'ERNIE-Lite-Pro-128K'
WHERE Name = 'ERNIE-Lite-Pro-128K';

-- 删除指定的记录
DELETE FROM ModelReference
WHERE Name = 'ERNIE-Lite-128K';

DELETE FROM ModelReference where Name in ('ernie-func-8k', 'ernie-char-fiction-8k', 'ernie-tiny-128k');