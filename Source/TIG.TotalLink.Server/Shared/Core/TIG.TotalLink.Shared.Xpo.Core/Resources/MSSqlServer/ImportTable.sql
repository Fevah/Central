DECLARE @Xml	XML,
		@hDoc	INT

-- Create a temporary table to hold the xml data
CREATE TABLE #Xml
(
    Id		INT IDENTITY PRIMARY KEY,
    XmlData XML
)    

-- Bulk load the xml file into the temporary table
INSERT INTO	#Xml(XmlData)
SELECT		CONVERT(XML, BulkColumn) AS BulkColumn
FROM		OPENROWSET(BULK '$FileName$', SINGLE_BLOB) AS BulkRowset;

-- Select the xml data from the temporary table
SELECT	@Xml = XmlData
FROM	#Xml

-- Drop the temporary table
DROP TABLE #Xml

-- Get the xml data as an xml document
EXEC sp_xml_preparedocument @hDoc OUTPUT, @Xml

-- Insert the data from the xml document into the target table
INSERT INTO	[$TableName$]($ColumnList$,[OptimisticLockField])
SELECT		$ColumnListWithConversions$,0
FROM OPENXML(@hDoc, '/$TableNamePlural$/$TableName$', 2)
WITH ($ColumnListWithDataTypes$)

-- Release the xml document
EXEC sp_xml_removedocument @hDoc
