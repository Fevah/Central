DECLARE @SqlString VARCHAR(8000)
DECLARE @ShellCommand VARCHAR(8000)

SET @SqlString = 'SELECT ''$Version$'' AS ""@Version"", (SELECT $ColumnList$ FROM [$DbName$].dbo.[$TableName$] WHERE [GCRecord] IS NULL FOR XML PATH(''$TableName$''), TYPE) FOR XML PATH(''$TableNamePlural$'')'
SET @ShellCommand = 'bcp "' + @SqlString + '" queryout "$FileName$" -w -r -S "$ServerName$" -U "$UserName$" -P "$Password$"'
 
EXEC xp_cmdshell @ShellCommand
