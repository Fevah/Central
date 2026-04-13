CREATE DATABASE [$DbName]
ON PRIMARY
(
    NAME = [$DbName_PRIMARY_00001],
    FILENAME = '$DbLocation\$DbName\$Container\$Folder\$DbName_PRIMARY_00001.mdf',
    SIZE = 100MB,
    MAXSIZE = $DataFileSizeLimit,
    FILEGROWTH = 100%
),
FILEGROUP [$DbName_FileStream] CONTAINS FILESTREAM
(
    NAME = [$DbName_FileStream],
    FILENAME = '$DbLocation\$DbName\$DbName_FileStream'
)
LOG ON
(
    NAME = [$DbName_00001_log],
    FILENAME = '$DbLocation\$DbName\$DbName_00001.ldf'
)
