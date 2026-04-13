ALTER DATABASE [$DbName]
ADD FILE 
( 
	NAME = [$LogicalName], 
	FILENAME = '$DbLocation\$DbName\$Container\$Folder\$LogicalName.ndf', 
	SIZE = 100MB, 
	MAXSIZE =$DataFileSizeLimit, 
	FILEGROWTH = 100%
) 
TO FILEGROUP [PRIMARY]
