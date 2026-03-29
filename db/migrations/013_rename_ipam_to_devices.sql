-- Rename module key from 'ipam' to 'devices' in role_permissions
UPDATE role_permissions SET module = 'devices' WHERE module = 'ipam';
