DECLARE @Xml	XML,
		@hDoc	INT,
		@Sql	NVARCHAR(4000)

-- Create a temporary table to hold the xml data
CREATE TABLE #Xml
(
    Id		INT IDENTITY PRIMARY KEY,
    XmlData XML
)    

-- Bulk load the xml file into the temporary xml table
INSERT INTO	#Xml(XmlData)
SELECT		CONVERT(XML, BulkColumn) AS BulkColumn
FROM		OPENROWSET(BULK '$FileName$', SINGLE_BLOB) AS BulkRowset;

-- Select the xml data from the temporary xml table
SELECT	@Xml = XmlData
FROM	#Xml

-- Drop the temporary xml table
DROP TABLE #Xml

-- Get the xml data as an xml document
EXEC sp_xml_preparedocument @hDoc OUTPUT, @Xml

-- Select the new users into a temporary table
SELECT	*
INTO	#ImportedUsers
FROM OPENXML(@hDoc, '/Users/User', 2)
WITH 
(
    [OID]				uniqueidentifier,
    [UserName]			nvarchar(100),
	[DisplayName]		nvarchar(200),
	[Password]			nvarchar(100),
	[Salt]				nvarchar(100),
    [ActiveDirectoryId]	uniqueidentifier,
	[UserType]			int,
	[CreatedBy]			uniqueidentifier,
	[CreatedDate]		datetime,
	[ModifiedBy]		uniqueidentifier,
	[ModifiedDate]		datetime
)

-- Release the xml document
EXEC sp_xml_removedocument @hDoc

-- Create a temporary table to hold User references
CREATE TABLE #UserReferences
(
	[Id]			INT IDENTITY PRIMARY KEY,
	[TableName]		sysname,
	[ColumnName]	sysname
)

-- Populate the temporary reference table with all tables and columns that reference User.OID
INSERT INTO #UserReferences
SELECT	[TableName]					= pt.name,
		[ColumnName]				= pc.name

FROM	sys.foreign_keys			fk

INNER JOIN	sys.foreign_key_columns	fc
ON			fk.object_id			= fc.constraint_object_id

INNER JOIN	sys.tables				pt
ON			fk.parent_object_id		= pt.object_id

INNER JOIN	sys.tables				rt
ON			fk.referenced_object_id	= rt.object_id

INNER JOIN	sys.columns				pc
ON			fc.parent_object_id		= pc.object_id
AND			fc.parent_column_id		= pc.column_id

INNER JOIN	sys.columns				rc
ON			fc.referenced_object_id	= rc.object_id
AND			fc.referenced_column_id	= rc.column_id

WHERE		rt.name					= 'User'
AND			rc.name					= 'OID'

ORDER BY	pt.name, pc.name

-- Disable constraints on all tables
-- This is required because updating User.Oid may break referential integrity temporarily
EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'

-- Declare variables for the user cursor
DECLARE	@UserCursor				cursor,
		@New_Oid				uniqueidentifier,
		@New_UserName			nvarchar(100),
		@New_DisplayName		nvarchar(200),
		@New_Password			nvarchar(100),
		@New_Salt				nvarchar(100),
		@New_ActiveDirectoryId	uniqueidentifier,
		@New_UserType			int,
		@New_CreatedBy			uniqueidentifier,
		@New_CreatedDate		datetime,
		@New_ModifiedBy			uniqueidentifier,
		@New_ModifiedDate		datetime,
		@Old_Oid				uniqueidentifier

-- Prepare the user cursor with results from the temporary user table
SET @UserCursor = CURSOR FOR
SELECT * FROM #ImportedUsers

-- Open the user cursor and collect the first result
OPEN			@UserCursor

FETCH NEXT FROM @UserCursor
INTO			@New_Oid,
				@New_UserName,
				@New_DisplayName,
				@New_Password,
				@New_Salt,
				@New_ActiveDirectoryId,
				@New_UserType,
				@New_CreatedBy,
				@New_CreatedDate,
				@New_ModifiedBy,
				@New_ModifiedDate

-- Process each User from the temporary user table
WHILE @@FETCH_STATUS = 0
BEGIN
	-- Attempt to find an existing User with a matching UserName
	SET		@Old_Oid	= NULL

	SELECT	@Old_Oid	= [OID]
	FROM	[User]
	WHERE	[UserName]	= @New_UserName

	-- If no matching User was found, create it
	IF @Old_Oid IS NULL
	BEGIN
		INSERT INTO [User]
		(
			[OID],
			[UserName],
			[DisplayName],
			[Password],
			[Salt],
			[ActiveDirectoryId],
			[UserType],
			[CreatedBy],
			[CreatedDate],
			[ModifiedBy],
			[ModifiedDate],
			[OptimisticLockField]
		)
		VALUES
		(
			@New_Oid,
			@New_UserName,
			@New_DisplayName,
			@New_Password,
			@New_Salt,
			@New_ActiveDirectoryId,
			@New_UserType,
			@New_CreatedBy,
			@New_CreatedDate,
			@New_ModifiedBy,
			@New_ModifiedDate,
			0
		)
	END

	-- If a matching User was found, but the Oid doesn't match, update it
	IF @Old_Oid IS NOT NULL AND @Old_Oid != @New_Oid
	BEGIN
		-- Update the user
		UPDATE	[User]

		SET		[OID]			= @New_Oid,
				[CreatedBy]		= @New_CreatedBy,
				[ModifiedBy]	= @New_ModifiedBy

		WHERE	[OID]			= @Old_Oid

		-- Declare variables for the reference cursor
		DECLARE	@ReferenceCursor			cursor,
				@Ref_TableName				sysname,
				@Ref_ColumnName				sysname

		-- Prepare the reference cursor with results from the temporary reference table
		SET @ReferenceCursor = CURSOR FOR
		SELECT	[TableName],
				[ColumnName]
		FROM	#UserReferences

		-- Open the reference cursor and collect the first result
		OPEN			@ReferenceCursor

		FETCH NEXT FROM @ReferenceCursor
		INTO			@Ref_TableName,
						@Ref_ColumnName

		-- Process each reference from the temporary reference table
		WHILE @@FETCH_STATUS = 0
		BEGIN
			-- Build sql to update all references to @Old_Oid with @New_Oid
			SET @Sql =	'UPDATE ' + QUOTENAME(@Ref_TableName) +
						' SET ' + QUOTENAME(@Ref_ColumnName) + ' = ' + QUOTENAME(@New_Oid, '''') +
						' WHERE ' + QUOTENAME(@Ref_ColumnName) + ' = ' + QUOTENAME(@Old_Oid, '''')

			-- Execute the sql
			EXEC sp_executesql @Sql

			-- Collect the next result from the reference cursor
			FETCH NEXT FROM @ReferenceCursor
			INTO			@Ref_TableName,
							@Ref_ColumnName
		END

		-- Close the reference cursor
		CLOSE @ReferenceCursor
		DEALLOCATE @ReferenceCursor
	END
	
	-- Collect the next result from the user cursor
	FETCH NEXT FROM @UserCursor
	INTO			@New_Oid,
					@New_UserName,
					@New_DisplayName,
					@New_Password,
					@New_Salt,
					@New_ActiveDirectoryId,
					@New_UserType,
					@New_CreatedBy,
					@New_CreatedDate,
					@New_ModifiedBy,
					@New_ModifiedDate
END

-- Close the user cursor
CLOSE @UserCursor
DEALLOCATE @UserCursor

-- Drop the temporary tables
DROP TABLE #ImportedUsers
DROP TABLE #UserReferences

-- Re-enable constraints on all tables
EXEC sp_msforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'
