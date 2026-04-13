SELECT f.physical_name, f.size, f.type
                                    FROM sys.master_files AS f
                                    INNER JOIN sys.databases AS d ON d.database_id = f.database_id
                                    WHERE d.name = @databaseName
                                      AND f.type in (0,1)
                                    ORDER BY 1