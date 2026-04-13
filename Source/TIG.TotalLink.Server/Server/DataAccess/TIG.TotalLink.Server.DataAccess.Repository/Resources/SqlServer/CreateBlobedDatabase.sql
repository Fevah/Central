CREATE DATABASE [$DbName]
ON PRIMARY
(
    NAME = [$DbName_PRIMARY_00001],
    FILENAME = '$DbLocation\$DbName\$Container\$Folder\$DbName_PRIMARY_00001.mdf',
    SIZE = 100MB,
    MAXSIZE = $DataFileSizeLimit,
    FILEGROWTH = 100%
)
LOG ON
(
    NAME = [$DbName_00001_log],
    FILENAME = '$DbLocation\$DbName\$DbName_00001.ldf'
)
