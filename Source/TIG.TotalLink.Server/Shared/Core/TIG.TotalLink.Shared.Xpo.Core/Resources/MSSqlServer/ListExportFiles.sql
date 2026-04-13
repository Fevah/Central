-- Prepare the Powershell command
DECLARE @PowershellCommand VARCHAR(8000)
SET @PowershellCommand = 'powershell -Command "Get-ChildItem -Path ''$Path$'' -Filter *.xml -File | Foreach-Object { $found = (Get-Content $_.FullName -Encoding Unicode -TotalCount 1 -Delimiter `>) -match ''Version=\x22([\d|\.]+)\x22'' ; if ($found) { Write-Output (''|{0}|{1}'' -f $_.BaseName, $matches[1]) }}"'

-- Create a temp table to hold the xp_cmdshell output
CREATE TABLE	#Output
(
	[Id]		INT IDENTITY(1,1),
	[Output]	NVARCHAR(255) NULL
)

-- Execute the command and store the results in the temp table
INSERT #Output
EXEC xp_cmdshell @PowershellCommand

-- Select the results from the temp table
SELECT	[TableName]			= SUBSTRING([Output], 2, CHARINDEX('|', [Output], 2) - 2),
		[Version]			= RIGHT([Output], LEN([Output]) - CHARINDEX('|', [Output], 2))
FROM	#Output
WHERE	[Output]			IS NOT NULL
AND		LEFT([Output], 1)	= '|'
ORDER BY [Id]

-- Select errors from the temp table
SELECT [ErrorMessage] = CONVERT(NVARCHAR(MAX),
(
	SELECT	REPLACE([Output], 'Get-ChildItem : ', '')
	FROM	#Output
	WHERE	[Id] <
		(
			SELECT	TOP 1
					[Id]
			FROM	#Output
			WHERE	LEN([Output]) > 8 AND LEFT([Output], 8) = 'At line:'
			ORDER BY [Id]
		)
	ORDER BY [Id]
	FOR XML PATH(''), TYPE
))

-- Drop the temp table
DROP TABLE #Output
