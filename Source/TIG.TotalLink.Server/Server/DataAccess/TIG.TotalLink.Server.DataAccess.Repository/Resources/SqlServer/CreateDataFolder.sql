SET NOCOUNT ON

DECLARE @DBName sysname
DECLARE @DataPath nvarchar(500)
DECLARE @DirTree TABLE (subdirectory nvarchar(255), depth INT)

SET @DBName = '$Folder'
SET @DataPath = '$DbLocation\$Folder'

--1 - @DataPath values
INSERT INTO @DirTree(subdirectory, depth)
EXEC master.sys.xp_dirtree @DataPath

-- 2 - Create the @DataPath directory
IF NOT EXISTS (SELECT 1 FROM @DirTree WHERE subdirectory = @DBName)
EXEC master.dbo.xp_create_subdir @DataPath

SET NOCOUNT OFF