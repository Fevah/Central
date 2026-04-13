-- From https://msdn.microsoft.com/en-us/library/ms190693.aspx

-- Allow advanced options to be changed
EXEC sp_configure 'show advanced options', 1

-- Update the currently configured value for advanced options
RECONFIGURE

-- Enable the xp_cmdshell feature
EXEC sp_configure 'xp_cmdshell', 1

-- Update the currently configured value for the xp_cmdshell feature
RECONFIGURE
