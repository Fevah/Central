CREATE TABLE [$DbName].[dbo].[FileData]
(
    [OID] [uniqueidentifier] ROWGUIDCOL  NOT NULL,
    [Body] VARBINARY(MAX) $FileStream NULL,
    [Size] BIGINT NOT NULL,
    [HashHex] CHAR(32) NOT NULL,
	[Description] VARCHAR(256) NULL,
    [CreatedOn] DATETIME NOT NULL,
    CONSTRAINT [PK_File] PRIMARY KEY CLUSTERED 	([OID] ASC)
)
